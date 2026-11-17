using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GameClockOverlay.AvaloniaApp;
using GameClockOverlay.AvaloniaApp.Models;
using GameClockOverlay.AvaloniaApp.Services;
using GameClockOverlay.AvaloniaApp.Views;
using MainProgram=GameClockOverlay.AvaloniaApp.Program;

namespace GameClockOverlay.Avalonia.Tests;

internal static partial class Program
{
    private static int passed,failed;
    private static readonly List<string> results=[];
    private static string evidence="";
    private static void Check(bool condition,string name)
    {
        results.Add((condition?"PASS ":"FAIL ")+name);
        Console.WriteLine(results[^1]);if(condition)passed++;else failed++;
    }
    [STAThread]
    private static int Main(string[] args)
    {
        if(args is ["--backdrop"])return AppBuilder.Configure<BackdropApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
        evidence=Path.GetFullPath(args.Length>0?args[0]:"evidence/test-run-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(evidence);
        UiStrings.Initialize();
        File.WriteAllLines(Path.Combine(evidence,"localization.zh-TW.txt"),Strings.Values.Values.Select(x=>x[0]));
        Core();
        if(args is [_,"--rtss-live",var span] && int.TryParse(span,out int liveSeconds) && liveSeconds is >0 and <=3600)return RtssLive(liveSeconds);
        bool special=args.Any(a=>a is "--session-end" or "--quit-fault" or "--hotkey-conflict" or "--review" or "--visual" or "--rtss-live-ui" or "--rtss-launch-ui");
        // RTSS 隔離測試只在預設 UI 模式與 --core-only 執行，其餘模式不重跑。
        if(!special){SharedOsdCore();RtssServiceCore();RtssPositionCore();ReviewFixCore();ReleaseCore();RtssLaunchCore();}
        if(args.Contains("--core-only")){WriteResults();return failed==0?0:1;}
        App.TestSettingsPath=Path.Combine(evidence,"ui-settings.json");
        // 所有 GUI 測試預設使用隨機名稱的隔離映射，絕不寫入正式 RTSS；只有 --rtss-live-ui 使用正式映射。
        App.TestRtssMap=args.Contains("--rtss-live-ui")?null:"GameClockAvalonia.UiTest."+Guid.NewGuid().ToString("N");
        App.TestRtssRetry=args.Contains("--rtss-live-ui")?null:TimeSpan.FromMilliseconds(300);
        if(args.Contains("--rtss-live-ui"))return RtssLiveUi(args);
        if(args.Contains("--session-end"))return SessionEnd();
        if(args.Contains("--quit-fault"))return QuitFault();
        if(args.Contains("--hotkey-conflict"))return HotkeyConflict();
        if(args.Contains("--rtss-launch-ui"))return RtssLaunchUi();
        // #6：以既有設定檔啟動，確認啟動本身不重寫主檔、不輪替 .bak。
        new SettingsStore(App.TestSettingsPath).Save(new ClockSettings());
        startupHash=Hash(App.TestSettingsPath);
        bool uiExecuted=false;
        App.TestReady=async app=>{uiExecuted=true;if(args.Contains("--review"))await Review(app);else if(args.Contains("--visual"))await Visual(app);else await Ui(app);};
        int exit=MainProgram.Main(["--ui-test"]);
        Check(exit==0,"desktop-lifetime-normal-exit");
        Check(uiExecuted,"test-process-owned-desktop-lifetime");
        Check(!HotkeyHeldBySomeone(),"hotkey-released-after-exit");
        if(uiFixture is not null)
        {
            Check(uiFixture.Owned()==0,"exit-clears-own-rtss-entry");
            Check(ForeignUnchanged(uiBaseline!,uiFixture.Snapshot()),"exit-preserves-foreign-rtss-entries");
            uiFixture.Dispose();
        }
        WriteResults();return failed==0?0:1;
    }
    private static string startupHash="";
    private static string Hash(string path)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
    private static Timer Watchdog(string name)=>new(_=>{Check(false,name+"-timeout");WriteResults();Environment.Exit(2);},null,20000,Timeout.Infinite);
    // #5：TryShutdown 與 WM_QUERYENDSESSION 走相同的 DoShutdown 路徑（Avalonia 12.1.6 原始碼），可在不登出的情況下驗證是否否決結束要求。
    private static int SessionEnd()
    {
        bool? accepted=null;bool savedDuringRequest=false,connected=false;int ownedAfterRequest=-1;
        using var watchdog=Watchdog("session-end");
        using var osd=new OsdFixture(App.TestRtssMap);
        App.TestReady=app=>
        {
            app.ViewModel.FontSize=44;app.ViewModel.TextColor="#85B6FF";
            app.ViewModel.GameClockEnabled=true;
            connected=WaitUntil(()=>app.Rtss.Status.Link==global::GameClockOverlay.AvaloniaApp.Services.Rtss.RtssLink.Connected && osd.Owned()==1,2000);
            accepted=((IClassicDesktopStyleApplicationLifetime)app.ApplicationLifetime!).TryShutdown();
            savedDuringRequest=File.Exists(App.TestSettingsPath!) && SettingsStore.Read(App.TestSettingsPath!) is {FontSize:44,TextColor:"#85B6FF",GameClockEnabled:true};
            ownedAfterRequest=osd.Owned();
            if(accepted!=true)_=app.QuitAsync();
        };
        int exit=MainProgram.Main(["--ui-test"]);
        Check(accepted==true,"session-end-request-not-vetoed");
        Check(savedDuringRequest,"session-end-saves-synchronously");
        Check(connected,"session-end-rtss-connected-before-request");
        Check(ownedAfterRequest==0,"session-end-clears-rtss-entry-during-request");
        Check(exit==0,"session-end-normal-exit");
        WriteResults();return failed==0?0:1;
    }
    // #3：注入保存例外，確認 QuitAsync 仍結束桌面生命週期並釋放單一執行個體。
    private static int QuitFault()
    {
        bool faultSeen=false;
        using var watchdog=Watchdog("quit-fault");
        var hook=typeof(App).GetField("TestBeforeSave",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        Check(hook is not null,"quit-fault-hook-present");
        hook?.SetValue(null,(Action)(()=>throw new InvalidOperationException("injected save fault")));
        App.TestReady=async app=>{try{await app.QuitAsync();}catch(InvalidOperationException){faultSeen=true;}};
        int exit=MainProgram.Main(["--ui-test"]);
        hook?.SetValue(null,null);
        Check(exit==0 && faultSeen,"quit-shuts-down-when-save-throws");
        using(var again=new GameClockOverlay.AvaloniaApp.Services.SingleInstance(".UiTest"))Check(again.IsFirst,"quit-fault-releases-single-instance");
        WriteResults();return failed==0?0:1;
    }
    private static TextBox ColorBox(Window window)=>window.GetVisualDescendants().OfType<TextBox>().Single(x=>global::Avalonia.Automation.AutomationProperties.GetName(x)==Strings.Get("Color"));
    private static double Luminance(Color c)
    {
        static double L(byte v){double x=v/255d;return x<=.03928?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
        return .2126*L(c.R)+.7152*L(c.G)+.0722*L(c.B);
    }
    private static double Contrast(Color a,Color b){double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
    private static double TextWidth(string text,string font,double size)=>new FormattedText(text,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(font),size,Brushes.White).Width;
    // #2：以實際桌面時鐘寬度檢查一整天所有時間字串（秒數取最寬兩位）是否都能保留邊距。
    private static double WorstTimeWidth(string font,double size,bool twelve)
    {
        string widestSeconds=Enumerable.Range(0,60).Select(x=>x.ToString("00")).MaxBy(x=>TextWidth(x,font,size))!;
        var day=new DateTime(2026,1,1);double worst=0;
        for(int minute=0;minute<1440;minute++)
        {
            string text=day.AddMinutes(minute).ToString(twelve?"hh:mm":"HH:mm",System.Globalization.CultureInfo.InvariantCulture)+":"+widestSeconds+(twelve?(minute<720?" AM":" PM"):"");
            worst=Math.Max(worst,TextWidth(text,font,size));
        }
        return worst;
    }
    private static void WriteResults(){File.WriteAllLines(Path.Combine(evidence,"checks.txt"),results);File.WriteAllText(Path.Combine(evidence,"summary.json"),JsonSerializer.Serialize(new{passed,failed},new JsonSerializerOptions{WriteIndented=true}));}
    private static async Task Review(App app)
    {
        try
        {
            var vm=app.ViewModel;var window=app.SettingsWindow;
            foreach(string theme in new[]{"Light","Dark"})
            {
                vm.Theme=theme;vm.FontSize=96;window.SelectedTab=1;await Task.Delay(200);
                var color=window.GetVisualDescendants().OfType<TextBox>().Single(x=>global::Avalonia.Automation.AutomationProperties.GetName(x)==Strings.Get("Color"));
                string saved=vm.TextColor;color.Text="oops";await Task.Delay(100);
                Check(vm.TextColor==saved,"invalid-color-keeps-last-good-"+theme);
                Capture(window,theme.ToLowerInvariant()+"-color-error.png");color.Text=saved;
            }
            vm.Theme="Light";vm.FontSize=32;vm.ShowSeconds=true;await Task.Delay(200);window.SelectedTab=0;Capture(window,"light-settings.png");
            vm.Theme="Dark";await Task.Delay(200);Capture(window,"dark-settings.png");
            window.Width=640;window.Height=520;await Task.Delay(100);Capture(window,"compact-settings.png");
            ((global::Avalonia.Controls.INativeMenuItemExporterEventsImplBridge)app.TrayMenu!.Items[0]).RaiseClicked();
            Check(!app.Clock.IsVisible,"actual-tray-exporter-hide-action");
            ((global::Avalonia.Controls.INativeMenuItemExporterEventsImplBridge)app.TrayMenu!.Items[0]).RaiseClicked();
            Check(app.Clock.IsVisible && app.Clock.HotkeyAvailable,"actual-tray-exporter-show-action");
            window.Hide();((global::Avalonia.Controls.INativeMenuItemExporterEventsImplBridge)app.TrayMenu!.Items[1]).RaiseClicked();
            Check(window.IsVisible,"actual-tray-exporter-settings-action");
        }
        catch(Exception ex){Check(false,"review-exception-"+ex);}
        finally{await app.QuitAsync();}
    }
    private static async Task Visual(App app)
    {
        using var backdrop=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--backdrop"){UseShellExecute=false,CreateNoWindow=true})!;
        try
        {
            await Task.Delay(1000);app.SettingsWindow.Hide();
            var s=app.ViewModel.Settings;s.Position="Free";s.X=330;s.Y=330;s.FontSize=48;s.ShowSeconds=true;s.ClickThrough=false;
            app.ViewModel.NotifyExternal();app.SettingsWindow.Hide();
            File.WriteAllText(Path.Combine(evidence,"visual-ready.txt"),"Clock over blue backdrop for desktop capture");
            await Task.Delay(25000);
        }
        finally{if(!backdrop.HasExited)backdrop.CloseMainWindow();await app.QuitAsync();}
    }
    private static void Core()
    {
        var s=new ClockSettings();
        Check(ClockMath.Format(new(2026,11,16,23,59,59),s)=="23:59","default-24h-minute");
        s.ShowSeconds=true;Check(ClockMath.Format(new(2026,11,16,0,0,1),s)=="00:00:01","seconds-and-midnight");
        s.TwelveHour=true;Check(ClockMath.Format(new(2026,11,16,13,0,0),s)=="01:00:00 PM","12h-format");
        Check(ClockMath.NextUpdate(new(2026,11,16,12,0,59,900),false).TotalMilliseconds==115,"minute-boundary-schedule");
        Check(ClockMath.NextUpdate(new(2026,11,16,12,0,0,900),true).TotalMilliseconds==115,"second-boundary-schedule");
        s=new(){FontSize=double.NaN,Opacity=double.PositiveInfinity,TextColor="invalid",FontFamily="",Theme="bad",Language="bad",Position="bad"};s.Normalize();
        Check(s.FontSize==32 && s.Opacity==.9 && s.TextColor=="#FFFFFF" && s.FontFamily=="Consolas" && s.Position=="TopRight","invalid-values-normalized");
        s=new();var area=new PixelRect(-1920,0,1920,1040);var size=new PixelSize(200,70);
        Check(ClockMath.Place(area,size,s,1)==new PixelPoint(-216,16),"negative-origin-monitor-corner");
        s.Position="BottomLeft";Check(ClockMath.Place(area,size,s,1.5)==new PixelPoint(-1896,946),"scaled-inset");
        s.Position="Free";s.X=9000;s.Y=-9000;Check(ClockMath.Place(area,size,s,1)==new PixelPoint(-200,0),"offscreen-position-clamped");
        Check(ClockMath.Place(new(0,0,80,40),new(200,100),s,1)==new PixelPoint(0,0),"small-screen-safe-origin");
        string path=Path.Combine(evidence,"store","settings.json");var store=new SettingsStore(path);
        Check(store.Load().FontSize==32,"missing-settings-defaults");
        Check(store.Save(new(){FontSize=44,ShowSeconds=true,TextColor="#78DBBC"}),"atomic-initial-save");
        Check(new SettingsStore(path).Load() is {FontSize:44,ShowSeconds:true,TextColor:"#78DBBC"},"settings-roundtrip");
        Check(store.Save(new(){FontSize=52}),"replacement-save");
        Check(File.Exists(path+".bak") && SettingsStore.Read(path+".bak").FontSize==44,"previous-good-backup");
        File.WriteAllText(path,"{bad json");var corrupt=new SettingsStore(path);var recovered=corrupt.Load();
        Check(corrupt.Recovered && recovered.FontSize==44,"corrupt-primary-recovers-backup");
        Check(Directory.GetFiles(Path.GetDirectoryName(path)!,"*.invalid-*").Any(f=>File.ReadAllText(f)=="{bad json"),"malformed-original-preserved");
        Check(corrupt.Save(recovered) && SettingsStore.Read(path+".bak").FontSize==44,"recovery-preserves-good-backup");
        string legacy=Path.Combine(evidence,"legacy-copy.json");
        const string old="{\"Language\":\"ja\",\"ShowSeconds\":true,\"Opacity\":0.6,\"ClickThrough\":true,\"FontSize\":48,\"MonitorDevice\":\"legacy-display\",\"UseMonitorBounds\":true,\"Position\":\"Free\",\"X\":100,\"Y\":200}";
        File.WriteAllText(legacy,old);var imported=SettingsStore.Read(legacy);
        Check(imported is {Language:"ja",ShowSeconds:true,Opacity:.6,ClickThrough:true,FontSize:48,MonitorDevice:"legacy-display",UseMonitorBounds:true,Position:"Free",X:100,Y:200,Topmost:true,ClockVisible:true},"actual-v0.2-schema-compatible");
        Check(File.ReadAllText(legacy)==old && SettingsStore.DefaultPath!=SettingsStore.LegacyPath,"legacy-read-only-and-separate-path");
        File.WriteAllText(path,"null");File.Delete(path+".bak");Check(new SettingsStore(path).Load().FontSize==32,"null-settings-fallback");
        File.WriteAllText(path,new string('x',65537));Check(new SettingsStore(path).Load().FontSize==32,"oversized-settings-fallback");
        foreach(string language in new[]{"zh-TW","en","ja"})
        {Strings.Use(language);Check(Strings.Get("Settings").Length>0,"language-"+language);}
        Check(Strings.Resolve("auto","zh-Hant-TW")=="zh-TW" && Strings.Resolve("auto","ja-JP")=="ja" && Strings.Resolve("auto","fr-FR")=="en","system-language-fallback");
        // 快捷鍵：註冊常數必須維持原組合，顯示文字由同一常數產生，三語說明都要帶入實際按鍵。
        Check(ClockWindow.HotkeyModifiers==0x4007 && ClockWindow.HotkeyKey==0x79,"hotkey-registration-constants-unchanged");
        Check(ClockWindow.HotkeyLabel=="Ctrl + Alt + Shift + F10","hotkey-label-matches-registration");
        Check(ClockWindow.Describe(1,0x70)=="Alt + F1" && ClockWindow.Describe(2|8,0x41)=="Ctrl + Win + A","hotkey-label-decodes-each-modifier");
        foreach(string language in new[]{"zh-TW","en","ja"})
        {
            Strings.Use(language);
            string failedText=string.Format(Strings.Get("HotkeyFailedNew"),ClockWindow.HotkeyLabel,1409);
            string trayText=string.Format(Strings.Get("OpenSettingsHotkey"),ClockWindow.HotkeyLabel);
            Check(failedText.Contains(ClockWindow.HotkeyLabel) && failedText.Contains("1409") && trayText.Contains(ClockWindow.HotkeyLabel) && !failedText.Contains('{') && !trayText.Contains('{')
                && new[]{"HotkeyTitle","HotkeyAction","HotkeyNotToggle","HotkeyReady"}.All(k=>Strings.Get(k).Length>0),"hotkey-strings-"+language);
        }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd,uint msg,nint wp,nint lp);
    // 以本執行緒佇列（hwnd=0）註冊同一組合：失敗且錯誤碼 1409（ERROR_HOTKEY_ALREADY_REGISTERED）代表系統上已有人持有。
    private static bool HotkeyHeldBySomeone()
    {
        bool ok=NativeWindow.RegisterHotKey(0,0x7000,ClockWindow.HotkeyModifiers,ClockWindow.HotkeyKey);
        int error=System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
        if(ok)NativeWindow.UnregisterHotKey(0,0x7000);
        return !ok && error==1409;
    }
    private static IEnumerable<string> Texts(Window window)=>window.GetVisualDescendants().OfType<TextBlock>().Select(x=>x.Text??"");
    // 先由測試程序占用快捷鍵，重現「已被其他程式使用」；確認介面不會宣稱可用、穿透被停用，且仍能從系統匣恢復。
    private static int HotkeyConflict()
    {
        using var watchdog=Watchdog("hotkey-conflict");
        new SettingsStore(App.TestSettingsPath!).Save(new ClockSettings{ClickThrough=true});
        bool held=NativeWindow.RegisterHotKey(0,0x7001,ClockWindow.HotkeyModifiers,ClockWindow.HotkeyKey);
        Check(held,"conflict-precondition-hotkey-taken");
        App.TestReady=async app=>
        {
            try
            {
                await Task.Delay(500);
                var clock=app.Clock;var window=app.SettingsWindow;window.SelectedTab=0;await Task.Delay(150);
                Check(!clock.HotkeyAvailable && clock.HotkeyError==1409,"conflict-registration-failure-detected");
                Check(!app.ViewModel.ClickThrough && (NativeWindow.GetWindowLongPtr(clock.Handle,NativeWindow.ExStyle).ToInt64()&NativeWindow.Transparent)==0,"conflict-clickthrough-forced-off");
                var passBox=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>global::Avalonia.Automation.AutomationProperties.GetName(x)==Strings.Get("ClickThrough"));
                Check(!passBox.IsEnabled,"conflict-clickthrough-checkbox-disabled");
                var texts=Texts(window).ToList();
                Check(texts.Contains(string.Format(Strings.Get("HotkeyFailedNew"),ClockWindow.HotkeyLabel,1409)) && !texts.Contains(Strings.Get("HotkeyReady")),"conflict-ui-shows-failure-not-ready");
                var items=app.TrayMenu!.Items.OfType<NativeMenuItem>().ToList();
                Check(items[1].Header==Strings.Get("OpenSettings") && !items[2].IsEnabled,"conflict-tray-omits-hotkey-and-disables-clickthrough");
                Capture(window,"hotkey-conflict-general.png");
                window.Hide();((global::Avalonia.Controls.INativeMenuItemExporterEventsImplBridge)app.TrayMenu!.Items[1]).RaiseClicked();
                Check(window.IsVisible && clock.IsVisible,"conflict-tray-recovery-still-works");
            }
            catch(Exception ex){Check(false,"conflict-exception-"+ex);}
            finally{await app.QuitAsync();}
        };
        int exit=MainProgram.Main(["--ui-test"]);
        if(held)NativeWindow.UnregisterHotKey(0,0x7001);
        Check(exit==0,"conflict-normal-exit");
        WriteResults();return failed==0?0:1;
    }
    private static async Task Ui(App app)
    {
        Process? backdrop=null;
        try
        {
            await Task.Delay(1000);
            var vm=app.ViewModel;var clock=app.Clock;var window=app.SettingsWindow;
            Check(Hash(App.TestSettingsPath!)==startupHash && !File.Exists(App.TestSettingsPath+".bak"),"startup-does-not-rewrite-settings");
            Check(window.IsVisible && clock.IsVisible,"settings-and-clock-startup");
            Check(app.TrayCreated,"native-tray-created");
            Check(clock.HotkeyAvailable && HotkeyHeldBySomeone(),"hotkey-registered-with-os");
            window.SelectedTab=0;await Task.Delay(150);
            var shown=Texts(window).ToList();
            Check(shown.Contains(ClockWindow.HotkeyLabel) && shown.Contains(Strings.Get("HotkeyReady")) && shown.Contains(Strings.Get("HotkeyAction")) && !shown.Contains(string.Format(Strings.Get("HotkeyFailedNew"),ClockWindow.HotkeyLabel,clock.HotkeyError)),"settings-shows-actual-hotkey-and-ready-status");
            Check(app.TrayMenu?.Items.OfType<NativeMenuItem>().ElementAt(1).Header==string.Format(Strings.Get("OpenSettingsHotkey"),ClockWindow.HotkeyLabel),"tray-open-settings-shows-hotkey");
            Capture(window,"hotkey-general.png");
            await RtssUi(app);
            Check(clock.ActualTransparencyLevel==WindowTransparencyLevel.Transparent && clock.WindowDecorations==WindowDecorations.None,"transparent-borderless-window");
            vm.ShowSeconds=true;string before=clock.TimeText;await Task.Delay(1200);Check(clock.TimeText!=before,"live-second-transition");
            vm.FontFamily="Arial";vm.FontSize=48;vm.TextColor="#78DBBC";
            var digits=((Border)clock.Content!).Child as TextBlock;
            Check(digits?.FontFamily.Name=="Arial" && digits.FontSize==48 && digits.Foreground?.ToString()=="#ff78dbbc","font-size-color-applied");
            // #2：先找出本機字型中數字寬度差異最大者，再以實際視窗寬度驗證全日時間字串。
            var spreads=FontManager.Current.SystemFonts.Select(f=>f.Name).Distinct()
                .Select(f=>{var w=Enumerable.Range(0,10).Select(d=>TextWidth(d.ToString(),f,96)).ToArray();return(font:f,spread:w.Max()-w.Min(),meridiem:Math.Abs(TextWidth("AM",f,96)-TextWidth("PM",f,96)));})
                .OrderByDescending(x=>x.spread*6+x.meridiem).ToList();
            var fitFonts=spreads.Take(3).Select(x=>x.font).Append("Segoe UI").Distinct().ToList();
            var fit=new List<object>();
            foreach(string font in fitFonts)
            foreach(bool twelve in new[]{false,true})
            {
                vm.FontFamily=font;vm.FontSize=96;vm.ShowSeconds=true;vm.TwelveHour=twelve;
                double worst=WorstTimeWidth(font,96,twelve),now=TextWidth(clock.TimeText,font,96);
                fit.Add(new{font,twelve,windowWidth=clock.Width,worstText=worst,currentText=now,baselineWouldClip=worst>Math.Ceiling(now)+24});
                Check(worst<=clock.Width-24+.5,"proportional-font-fits-all-times-"+font+(twelve?"-12h":"-24h"));
            }
            File.WriteAllText(Path.Combine(evidence,"font-fit.json"),JsonSerializer.Serialize(new{top=spreads.Take(8).Select(x=>new{x.font,x.spread,x.meridiem}),fit},new JsonSerializerOptions{WriteIndented=true}));
            vm.TwelveHour=false;vm.FontFamily="Arial";vm.FontSize=48;
            // #4：近黑色文字在預覽框上仍需可辨識（以 WCAG 3:1 非文字／大字對比為下限）；白字維持原深色底。
            var frame=window.GetVisualDescendants().OfType<Border>().First(b=>b.Child is TextBlock && b.MinWidth==260);
            var previewText=(TextBlock)frame.Child!;
            foreach(string hex in new[]{"#161B22","#FFFFFF"})
            {
                vm.TextColor=hex;window.UpdatePreview();
                double ratio=Contrast(((ISolidColorBrush)frame.Background!).Color,((ISolidColorBrush)previewText.Foreground!).Color);
                Check(ratio>=3,"preview-contrast-"+hex+"-"+ratio.ToString("0.0"));
            }
            Check(((ISolidColorBrush)frame.Background!).Color==Color.Parse("#18232E"),"preview-default-background-kept");
            vm.TextColor="#78DBBC";
            vm.Position="BottomLeft";vm.OffsetX=24;vm.OffsetY=32;await Task.Delay(200);
            var screen=clock.Screens.ScreenFromWindow(clock)!;
            Check(clock.Position.X>=screen.WorkingArea.X && clock.Position.Y>screen.WorkingArea.Y,"placement-bottom-left");
            vm.MonitorDevice="disconnected-test-monitor";await Task.Delay(150);
            Check(clock.Screens.All.Any(s=>s.WorkingArea.Contains(clock.Position)),"disconnected-monitor-fallback");
            File.WriteAllText(Path.Combine(evidence,"screens.json"),JsonSerializer.Serialize(clock.Screens.All.Select(s=>new{s.DisplayName,s.Scaling,bounds=s.Bounds.ToString(),working=s.WorkingArea.ToString()})));
            vm.Topmost=false;Check((NativeWindow.GetWindowLongPtr(clock.Handle,NativeWindow.ExStyle).ToInt64()&8)==0,"native-topmost-off");
            vm.Topmost=true;Check((NativeWindow.GetWindowLongPtr(clock.Handle,NativeWindow.ExStyle).ToInt64()&8)!=0,"native-topmost-on");
            vm.ClickThrough=false;vm.Settings.Position="Free";vm.Settings.X=300;vm.Settings.Y=300;clock.Place();await Task.Delay(200);
            var point=new NativeWindow.Point{X=clock.Position.X+(int)(clock.Width*clock.RenderScaling/2),Y=clock.Position.Y+(int)(clock.Height*clock.RenderScaling/2)};
            Check(NativeWindow.WindowFromPoint(point)==clock.Handle,"hit-test-clock-before-pass-through");
            vm.ClickThrough=true;await Task.Delay(100);
            Check(clock.HotkeyAvailable && (NativeWindow.GetWindowLongPtr(clock.Handle,NativeWindow.ExStyle).ToInt64()&NativeWindow.Transparent)!=0,"clickthrough-style-on");
            nint behind=NativeWindow.WindowFromPoint(point);NativeWindow.GetWindowThreadProcessId(behind,out uint behindPid);
            Check(behind!=clock.Handle && behindPid!=(uint)Environment.ProcessId,"os-hit-test-passes-to-other-process");
            app.Recover();await Task.Delay(100);
            Check(!vm.ClickThrough && NativeWindow.WindowFromPoint(point)==clock.Handle,"recover-restores-hit-test");
            vm.ClockVisible=false;int ticks=clock.TickCount;await Task.Delay(1100);
            Check(!clock.IsVisible && !clock.TimerRunning && clock.TickCount==ticks,"hidden-clock-stops-timer");
            Check(HotkeyHeldBySomeone(),"hotkey-held-by-os-while-clock-hidden");
            app.ToggleClock();await Task.Delay(100);Check(clock.IsVisible && clock.TimerRunning,"tray-toggle-action-restores-clock");
            Check(clock.HotkeyAvailable,"hotkey-survives-hide-and-reshow");
            // #1：穿透開啟時隱藏再顯示，必須讀回原生樣式並以作業系統命中測試確認。
            vm.ClickThrough=true;await Task.Delay(100);vm.ClockVisible=false;await Task.Delay(150);vm.ClockVisible=true;await Task.Delay(250);
            point=new NativeWindow.Point{X=clock.Position.X+(int)(clock.Width*clock.RenderScaling/2),Y=clock.Position.Y+(int)(clock.Height*clock.RenderScaling/2)};
            Check(vm.ClickThrough && clock.IsVisible && (NativeWindow.GetWindowLongPtr(clock.Handle,NativeWindow.ExStyle).ToInt64()&NativeWindow.Transparent)!=0,"clickthrough-native-style-after-reshow");
            Check(NativeWindow.WindowFromPoint(point)!=clock.Handle,"clickthrough-hit-test-after-reshow");
            app.Recover();await Task.Delay(100);
            Check(NativeWindow.WindowFromPoint(point)==clock.Handle,"recover-after-reshow-restores-hit-test");
            // #7：恢復操作不得重建設定畫面，正在編輯的色碼與分頁需保留，穿透核取方塊需同步。
            window.SelectedTab=0;await Task.Delay(150);
            var passBox=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>global::Avalonia.Automation.AutomationProperties.GetName(x)==Strings.Get("ClickThrough"));
            passBox.IsChecked=true;await Task.Delay(50);app.Recover();await Task.Delay(150);
            var passAfter=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>global::Avalonia.Automation.AutomationProperties.GetName(x)==Strings.Get("ClickThrough"));
            Check(ReferenceEquals(passBox,passAfter) && passAfter.IsChecked==false && !vm.ClickThrough,"recover-syncs-checkbox-without-rebuild");
            window.SelectedTab=1;await Task.Delay(150);
            var editing=ColorBox(window);string goodColor=vm.TextColor;editing.Text="#12";editing.Focus();await Task.Delay(50);
            app.Recover();await Task.Delay(150);
            var editingAfter=ColorBox(window);
            Check(ReferenceEquals(editing,editingAfter) && editingAfter.Text=="#12" && window.SelectedTab==1 && vm.TextColor==goodColor,"recover-keeps-in-progress-edit");
            editingAfter.Text=goodColor;window.SelectedTab=0;
            Check(app.TrayMenu?.Items.Count==6 && app.TrayMenu.Items.OfType<NativeMenuItem>().All(item=>!string.IsNullOrEmpty(item.Header)),"tray-menu-actions-present");
            window.Close();Check(!window.IsVisible && clock.IsVisible,"close-settings-keeps-clock");
            Check(clock.HotkeyAvailable && HotkeyHeldBySomeone(),"hotkey-held-by-os-after-settings-closed");
            // 模擬作業系統送出 WM_HOTKEY（id=1）：設定已關閉、時鐘隱藏且穿透時，應恢復並開啟設定。實體按鍵仍須人工確認。
            vm.ClickThrough=true;vm.ClockVisible=false;await Task.Delay(150);
            Check(PostMessage(clock.Handle,0x312,1,(nint)((ClockWindow.HotkeyKey<<16)|(ClockWindow.HotkeyModifiers&0xF))),"wm-hotkey-posted");
            await Task.Delay(300);
            Check(window.IsVisible && clock.IsVisible && !vm.ClickThrough,"wm-hotkey-recovers-after-settings-closed-and-clock-hidden");
            window.Hide();
            int count=app.ActivationCount;
            using(var second=Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"GameClockOverlay.exe"),"--ui-test"){UseShellExecute=false,CreateNoWindow=true})!)
            {await second.WaitForExitAsync();Check(second.ExitCode==0,"second-process-exits");}
            await Task.Delay(250);Check(app.ActivationCount==count+1 && window.IsVisible,"second-instance-activates-settings");
            vm.ShowSeconds=true;vm.FontFamily="Consolas";vm.FontSize=32;vm.TextColor="#FFFFFF";vm.MonitorDevice="";vm.Position="TopRight";vm.Language="zh-TW";
            Check(await app.SaveAsync(),"async-settings-save");
            Check(SettingsStore.Read(App.TestSettingsPath!) is {ShowSeconds:true,FontSize:32,TextColor:"#FFFFFF"},"ui-settings-reloaded");
            foreach(var theme in new[]{"Light","Dark"})
            {
                vm.Theme=theme;window.Width=850;window.Height=740;await Task.Delay(250);
                Capture(window,theme.ToLowerInvariant()+"-settings.png");Check(window.ActualThemeVariant.Key.ToString()==theme,"theme-"+theme);
            }
            vm.Theme="Light";window.Width=640;window.Height=520;await Task.Delay(200);Capture(window,"compact-settings.png");
            foreach(string language in new[]{"en","ja"}){vm.Language=language;await Task.Delay(150);Check(window.Title!.Contains(Strings.Get("Settings")),"ui-language-"+language);}
            vm.Language="zh-TW";window.Width=850;window.Height=740;
            for(int tab=0;tab<5;tab++){window.SelectedTab=tab;await Task.Delay(100);Capture(window,"tab-"+tab+".png");}
            window.SelectedTab=0;
            await ResourceSample(window,"settings-visible");window.Hide();await ResourceSample(window,"settings-hidden");
            vm.ClockVisible=false;await ResourceSample(window,"all-hidden");vm.ClockVisible=true;
            // 以真實牆鐘邊界確認分鐘更新；只等待接下來一次分鐘切換。
            vm.ShowSeconds=false;string minute=clock.TimeText;
            int wait=(int)ClockMath.NextUpdate(DateTime.Now,false).TotalMilliseconds+150;
            Console.WriteLine("WAIT minute boundary "+wait+" ms");await Task.Delay(wait);
            Check(clock.TimeText!=minute,"live-minute-transition");
            Check(await app.SaveAsync(),"final-save-before-shutdown");
        }
        catch(Exception ex){Check(false,"ui-exception-"+ex);}
        finally
        {
            if(backdrop is not null && !backdrop.HasExited)backdrop.CloseMainWindow();
            WriteResults();await app.QuitAsync();
        }
    }
    private static void Capture(Window window,string name)
    {
        var size=new PixelSize((int)Math.Ceiling(window.Bounds.Width),(int)Math.Ceiling(window.Bounds.Height));
        using var bitmap=new RenderTargetBitmap(size,new Vector(96,96));bitmap.Render(window);bitmap.Save(Path.Combine(evidence,name),PngBitmapEncoderOptions.Default);
    }
    private static async Task ResourceSample(Window window,string state)
    {
        await Task.Delay(1500);
        using var p=Process.GetCurrentProcess();p.Refresh();var cpu=p.TotalProcessorTime;var sw=Stopwatch.StartNew();await Task.Delay(5000);p.Refresh();
        var sample=new{state,elapsedSeconds=sw.Elapsed.TotalSeconds,cpuSeconds=(p.TotalProcessorTime-cpu).TotalSeconds,cpuPercentOneCore=(p.TotalProcessorTime-cpu).TotalSeconds/sw.Elapsed.TotalSeconds*100,workingSetMiB=p.WorkingSet64/1048576d,privateMiB=p.PrivateMemorySize64/1048576d,logicalProcessors=Environment.ProcessorCount};
        File.AppendAllText(Path.Combine(evidence,"resources.jsonl"),JsonSerializer.Serialize(sample)+Environment.NewLine);
    }
}
internal sealed class BackdropApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)desktop.MainWindow=new Window{Title="Clock input test backdrop",Position=new(280,280),Width=500,Height=300,Background=Brushes.DarkBlue};
        base.OnFrameworkInitializationCompleted();
    }
}
