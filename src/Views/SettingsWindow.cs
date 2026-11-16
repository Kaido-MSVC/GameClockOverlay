using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using GameClockOverlay.AvaloniaApp.Services;
using GameClockOverlay.AvaloniaApp.Services.Rtss;
using GameClockOverlay.AvaloniaApp.ViewModels;

namespace GameClockOverlay.AvaloniaApp.Views;

public sealed class SettingsWindow : Window
{
    private readonly App app;
    private SettingsViewModel Vm=>app.ViewModel;
    private TextBlock preview=null!,status=null!;
    private TextBlock? colorError,hotkeyStatus;
    private TextBlock? rtssLink;
    private TextBlock rtssProcess=null!,rtssInstall=null!,rtssError=null!,rtssPosition=null!;
    private CheckBox gameBox=null!;
    private Border previewFrame=null!;
    private CheckBox visibleBox=null!,clickBox=null!;
    private static readonly ISolidColorBrush PreviewDark=new ImmutableSolidColorBrush(Color.Parse("#18232E")),PreviewLight=new ImmutableSolidColorBrush(Color.Parse("#E6EBF0"));
    private TabControl tabs=null!;
    private readonly DispatcherTimer previewTimer=new();
    public int SelectedTab {get=>tabs.SelectedIndex;set=>tabs.SelectedIndex=value;}
    public SettingsWindow(App app)
    {
        this.app=app;Width=850;Height=740;MinWidth=640;MinHeight=520;
        FontFamily=new("Microsoft JhengHei UI");FontSize=14;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Icon=new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://GameClockOverlay.Avalonia/clock.ico")));
        Closing+=(_,e)=>{if(!app.Exiting){e.Cancel=true;Hide();}};
        PropertyChanged+=(_,e)=>
        {
            if(e.Property==IsVisibleProperty){if(IsVisible){UpdatePreview();UpdateRtss();}else previewTimer.Stop();}
            if(e.Property==WindowStateProperty && WindowState==WindowState.Minimized)Hide();
            if(e.Property==ActualThemeVariantProperty)ApplyPalette();
        };
        previewTimer.Tick+=(_,_)=>UpdatePreview();
        Closed+=(_,_)=>previewTimer.Stop();
        Build();
        ApplyPalette();
    }
    // 關於頁的版本取自 assembly 的資訊版本（csproj 的 InformationalVersion），不在字串中寫死。
    internal static string AppVersion=>typeof(SettingsWindow).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion??"?";
    private static string L(string key)=>Strings.Get(key);
    private static TextBlock Text(string value,double size=14)=>new(){Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap};
    private static void AccessibleName(Control control,string value){AutomationProperties.SetName(control,value);}
    private static StackPanel Stack(double spacing=12)=>new(){Spacing=spacing};
    private static Control Scroll(Control child)=>new ScrollViewer{Content=child,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(4,18,12,12)};
    private static void Row(Panel body,string key,Control control,string? hint=null)
    {
        AccessibleName(control,L(key));var row=new Grid{ColumnDefinitions=new("*,280"),Margin=new(0,8,0,8)};
        var label=Text(L(key));label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);
        Grid.SetColumn(control,1);control.HorizontalAlignment=HorizontalAlignment.Stretch;row.Children.Add(control);body.Children.Add(row);
        if(hint is not null){var note=Text(L(hint),12);note.Opacity=.8;body.Children.Add(note);}
    }
    private static CheckBox Check(string key,bool value,Action<bool> change)
    {
        var box=new CheckBox{Content=L(key),IsChecked=value,MinHeight=36};AccessibleName(box,L(key));
        box.IsCheckedChanged+=(_,_)=>change(box.IsChecked==true);return box;
    }
    private static ComboBox Choice(string[] keys,string selected,Action<string> change,Func<string,string>? labels=null)
    {
        labels??=L;var box=new ComboBox{ItemsSource=keys.Select(labels).ToArray(),SelectedIndex=Array.IndexOf(keys,selected),MinHeight=36};
        box.SelectionChanged+=(_,_)=>{if(box.SelectedIndex>=0)change(keys[box.SelectedIndex]);};return box;
    }
    private static NumericUpDown Number(decimal value,decimal min,decimal max,Action<decimal> change)
    {
        var box=new NumericUpDown{Value=value,Minimum=min,Maximum=max,Increment=1,FormatString="0",MinHeight=36};
        box.ValueChanged+=(_,_)=>{if(box.Value is decimal n)change(n);};return box;
    }
    public void Build()
    {
        int selected=tabs?.SelectedIndex??0;
        Title="GameClock Overlay — "+L("Settings");
        var root=new DockPanel{Margin=new Thickness(28,24,28,20)};
        var header=Stack(6);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        header.Children.Add(new TextBlock{Text=L("Settings"),FontSize=28,FontWeight=FontWeight.SemiBold});
        var hint=Text(L("DesktopHint"),13);hint.Opacity=.8;header.Children.Add(hint);
        var band=new Grid{ColumnDefinitions=new("*,Auto"),Margin=new(0,20,0,20)};
        var left=Stack(4);left.VerticalAlignment=VerticalAlignment.Center;left.Children.Add(Text(L("Preview"),16));
        var ph=Text(L("PreviewHint"),12);ph.MaxWidth=280;ph.HorizontalAlignment=HorizontalAlignment.Left;ph.Opacity=.8;left.Children.Add(ph);band.Children.Add(left);
        preview=new TextBlock{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
        var frame=new Border{Background=PreviewDark,CornerRadius=new(10),Padding=new(16,12),MinWidth=260,MaxWidth=400,Child=preview};previewFrame=frame;
        Grid.SetColumn(frame,1);band.Children.Add(frame);header.Children.Add(band);
        var footer=new Grid{ColumnDefinitions=new("*,Auto,Auto"),Margin=new(0,14,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        status=Text(app.Status,12);status.VerticalAlignment=VerticalAlignment.Center;status.HorizontalAlignment=HorizontalAlignment.Left;status.MaxWidth=450;footer.Children.Add(status);
        var close=new Button{Content=L("Close"),Padding=new(16,9),Margin=new(8,0)};close.Click+=(_,_)=>Hide();Grid.SetColumn(close,1);footer.Children.Add(close);
        var exit=new Button{Content=L("Exit"),Padding=new(16,9)};exit.Click+=async(_,_)=>await app.QuitAsync();Grid.SetColumn(exit,2);footer.Children.Add(exit);
        tabs=new TabControl();root.Children.Add(tabs);
        var general=Stack();
        visibleBox=Check("Visible",Vm.ClockVisible,x=>Vm.ClockVisible=x);general.Children.Add(visibleBox);
        general.Children.Add(Check("TwelveHour",Vm.TwelveHour,x=>Vm.TwelveHour=x));
        general.Children.Add(Check("Seconds",Vm.ShowSeconds,x=>Vm.ShowSeconds=x));
        general.Children.Add(new Separator{Margin=new(0,6)});
        general.Children.Add(Check("Topmost",Vm.Topmost,x=>Vm.Topmost=x));
        var click=Check("ClickThrough",Vm.ClickThrough,x=>Vm.ClickThrough=x);click.IsEnabled=app.Clock.HotkeyAvailable;general.Children.Add(click);clickBox=click;
        general.Children.Add(HotkeyPanel());
        Row(general,"Theme",Choice(["System","Light","Dark"],Vm.Theme,x=>Vm.Theme=x));
        Row(general,"Language",Choice(["auto","zh-TW","en","ja"],Vm.Language,x=>Vm.Language=x,x=>x switch{"auto"=>L("Auto"),"zh-TW"=>"繁體中文","en"=>"English",_=>"日本語"}));
        var import=new Button{Content=L("Import")};import.Click+=async(_,_)=>await app.ImportAsync();general.Children.Add(import);general.Children.Add(Text(L("ImportHint"),12));
        AddTab("General",general);
        var appearance=Stack();
        string[] fonts=FontManager.Current.SystemFonts.Select(f=>f.Name).Append(Vm.FontFamily).Distinct().OrderBy(x=>x).ToArray();
        Row(appearance,"Font",Choice(fonts,Vm.FontFamily,x=>Vm.FontFamily=x,x=>x));
        Row(appearance,"FontSize",Number((decimal)Vm.FontSize,16,96,x=>Vm.FontSize=(double)x));
        Row(appearance,"Opacity",Number((decimal)(Vm.Opacity*100),25,100,x=>Vm.Opacity=(double)x/100));
        var error=Text("",12);colorError=error;error.Foreground=ErrorBrush();
        var color=new TextBox{Text=Vm.TextColor,MaxLength=7};
        color.TextChanged+=(_,_)=>
        {
            if(color.Text is string value && System.Text.RegularExpressions.Regex.IsMatch(value,"^#[0-9A-Fa-f]{6}$")){error.Text="";if(!string.Equals(value,Vm.TextColor,StringComparison.OrdinalIgnoreCase))Vm.TextColor=value;}
            else error.Text=L("InvalidColor");
        };
        Row(appearance,"Color",color,"ColorHint");appearance.Children.Add(error);
        var colors=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
        foreach(string hex in new[]{"#FFFFFF","#FFD166","#78DBBC","#85B6FF","#F391BC","#161B22"})
        {
            var button=new Button{Width=38,Height=36,Background=Brush.Parse(hex),Content="",BorderThickness=new(1),BorderBrush=Brushes.Gray};AccessibleName(button,hex);
            button.Click+=(_,_)=>color.Text=hex;colors.Children.Add(button);
        }
        appearance.Children.Add(colors);AddTab("AppearanceTab",appearance);
        var placement=Stack();List<string> screenNames=[with(capacity:app.Clock.Screens.All.Count+2),""];screenNames.AddRange(app.Clock.Screens.All.Select(ClockWindow.DisplayId));
        if(!screenNames.Contains(Vm.MonitorDevice))screenNames.Add(Vm.MonitorDevice);
        Row(placement,"Monitor",Choice(screenNames.ToArray(),Vm.MonitorDevice,x=>Vm.MonitorDevice=x,x=>x==""?L("CurrentMonitor"):app.Clock.Screens.All.FirstOrDefault(s=>ClockWindow.DisplayId(s)==x) is {} screen?$"{screen.DisplayName} · {x}":L("DisconnectedMonitor")+x));
        Row(placement,"Position",Choice(["Free","TopLeft","TopRight","BottomLeft","BottomRight"],Vm.Position,x=>Vm.Position=x));
        Row(placement,"OffsetX",Number(Vm.OffsetX,-10000,10000,x=>Vm.OffsetX=(int)x));
        Row(placement,"OffsetY",Number(Vm.OffsetY,-10000,10000,x=>Vm.OffsetY=(int)x));
        placement.Children.Add(Text(L("OffsetHint"),12));placement.Children.Add(Check("MonitorBounds",Vm.UseMonitorBounds,x=>Vm.UseMonitorBounds=x));
        var refresh=new Button{Content=L("RefreshDisplays")};refresh.Click+=(_,_)=>{app.Clock.Place();Build();};placement.Children.Add(refresh);AddTab("PlacementTab",placement);
        AddTab("GameTab",GamePanel());
        var about=Stack(16);about.Children.Add(Text("GameClock Overlay",22));about.Children.Add(Text(string.Format(L("AboutNew"),AppVersion),13));
        var legal=new TextBox{Text=ReadLegal(),IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=250,FontSize=12};AccessibleName(legal,L("ThirdParty"));about.Children.Add(legal);AddTab("About",about);
        tabs.SelectedIndex=Math.Clamp(selected,0,4);Content=root;UpdatePreview();
    }
    // 按鍵文字取自 ClockWindow 的註冊常數；狀態依實際 RegisterHotKey 結果顯示，失敗時不宣稱可用。
    private Control HotkeyPanel()
    {
        var panel=Stack(6);panel.Margin=new(0,4,0,4);
        var head=new Grid{ColumnDefinitions=new("*,Auto")};
        var title=Text(L("HotkeyTitle"));title.FontWeight=FontWeight.SemiBold;title.VerticalAlignment=VerticalAlignment.Center;head.Children.Add(title);
        var keys=new Border{BorderThickness=new(1),BorderBrush=Brushes.Gray,CornerRadius=new(4),Padding=new(10,4),
            Child=new TextBlock{Text=ClockWindow.HotkeyLabel,FontFamily=new("Consolas"),FontSize=14,FontWeight=FontWeight.SemiBold}};
        AccessibleName(keys,L("HotkeyTitle")+" "+ClockWindow.HotkeyLabel);Grid.SetColumn(keys,1);head.Children.Add(keys);
        panel.Children.Add(head);
        panel.Children.Add(Text(L("HotkeyAction"),13));
        var note=Text(L("HotkeyNotToggle"),12);note.Opacity=.8;panel.Children.Add(note);
        hotkeyStatus=Text(app.Clock.HotkeyAvailable?L("HotkeyReady"):string.Format(L("HotkeyFailedNew"),ClockWindow.HotkeyLabel,app.Clock.HotkeyError),12);
        AccessibleName(hotkeyStatus,L("HotkeyTitle"));
        if(app.Clock.HotkeyAvailable)hotkeyStatus.Opacity=.8;else hotkeyStatus.Foreground=ErrorBrush();
        panel.Children.Add(hotkeyStatus);
        return panel;
    }
    // 遊戲內時鐘（RTSS）。狀態一律讀取後端的實際快照；只有實際寫入成功才顯示「已連線」。
    private Control GamePanel()
    {
        var game=Stack(14);
        gameBox=Check("GameEnable",Vm.GameClockEnabled,x=>Vm.GameClockEnabled=x);game.Children.Add(gameBox);
        // 只列出已在正式 RTSS 實測可運作的位置（左上即 RTSS 預設位置）；與桌面時鐘的「位置」分頁互相獨立。
        Row(game,"GamePosition",Choice(["TopRight","BottomRight","BottomLeft","Rtss"],Vm.GameClockPosition,x=>Vm.GameClockPosition=x,x=>x=="Rtss"?L("GamePosRtss"):L(x)),"GamePositionHint");
        var shared=Text(L("GameShared"),12);shared.Opacity=.8;game.Children.Add(shared);
        var independent=Text(L("GameIndependent"),12);independent.Opacity=.8;game.Children.Add(independent);
        game.Children.Add(new Separator{Margin=new(0,4)});
        var title=Text(L("RtssStatusTitle"),16);title.FontWeight=FontWeight.SemiBold;game.Children.Add(title);
        var grid=new Grid{ColumnDefinitions=new("Auto,*"),RowDefinitions=new("Auto,Auto,Auto,Auto,Auto")};
        TextBlock Value(int row,string key)
        {
            var label=Text(L(key),13);label.Opacity=.8;label.Margin=new(0,4,20,4);Grid.SetRow(label,row);grid.Children.Add(label);
            var value=Text("",13);value.Margin=new(0,4);Grid.SetRow(value,row);Grid.SetColumn(value,1);AccessibleName(value,L(key));grid.Children.Add(value);
            return value;
        }
        rtssLink=Value(0,"RtssLinkLabel");rtssPosition=Value(1,"RtssPositionLabel");rtssProcess=Value(2,"RtssProcessLabel");rtssInstall=Value(3,"RtssInstallLabel");rtssError=Value(4,"RtssErrorLabel");
        game.Children.Add(grid);
        game.Children.Add(new Separator{Margin=new(0,4)});
        var help=Text(L("RtssHelpTitle"),16);help.FontWeight=FontWeight.SemiBold;game.Children.Add(help);
        game.Children.Add(Text(L("RtssHelp"),13));
        var limit=Text(L("Limit"),12);limit.Opacity=.8;game.Children.Add(limit);
        UpdateRtss();
        return game;
    }
    public void UpdateRtss()
    {
        if(rtssLink is null)return;
        var s=app.Rtss.Status;var install=app.RtssInstallation;
        rtssLink.Text=s.Link switch
        {
            RtssLink.Connected=>string.Format(L("LinkConnected"),$"{s.Version>>16}.{s.Version&0xFFFF}",s.Entry,s.Text),
            RtssLink.WaitingForRtss=>string.Format(L(s.ProcessRunning==true?"LinkWaitingRunning":"LinkWaiting"),app.Rtss.Retry.TotalSeconds),
            RtssLink.Busy=>s.Code=="claim-conflict"?L("LinkConflict"):L("LinkBusy"),
            RtssLink.Error=>string.Format(L("LinkError"),ErrorText(s.Code),app.Rtss.ErrorRetry.TotalSeconds),
            RtssLink.Cleaning=>L("LinkCleaning"),
            _=>L("LinkDisabled")
        };
        // 實際位置取自最後一次寫入的結果（是否真的帶了位置標籤），不是只看設定值。
        rtssPosition.Text=s.Link!=RtssLink.Connected?L("PosNotConnected")
            :s.Placement==OsdPlacement.Rtss?L("PosRtss")
            :s.Positioned?string.Format(L("PosApplied"),L(s.Placement.ToString()))
            :string.Format(L("PosFallback"),L(s.Placement.ToString()));
        rtssProcess.Text=s.ProcessRunning switch{true=>L("RtssRunning"),false=>L("RtssNotRunning"),_=>Vm.GameClockEnabled?L("RtssChecking"):L("RtssNotChecked")};
        rtssInstall.Text=install is null?L("RtssChecking"):install.Directory is null?L("RtssNotInstalled")
            :install.ExecutableFound?string.Format(L("RtssInstalled"),install.Version??"",install.Directory):string.Format(L("RtssExeMissing"),install.Directory);
        rtssError.Text=s.LastError is null?L("ErrorNone"):$"{s.LastErrorAt:HH:mm:ss}　{ErrorText(s.LastError)}";
        if(gameBox.IsChecked!=Vm.GameClockEnabled)gameBox.IsChecked=Vm.GameClockEnabled;
    }
    internal static string ErrorText(string code)=>code switch
    {
        "not-running"=>L("ErrNotRunning"),
        "signature-invalid-or-dead"=>L("ErrDead"),
        "no-free-entry"=>L("ErrFull"),
        "claim-conflict"=>L("ErrConflict"),
        "access-denied"=>L("ErrDenied"),
        "busy"=>L("ErrCleanup"),
        "header-truncated" or "osd-array-out-of-bounds" or "app-array-invalid-or-overlaps-osd" or "layout-changed" or "mapping-too-large" or "mapping-null"=>L("ErrLayout")+"（"+code+"）",
        _ when code.StartsWith("unsupported-version",StringComparison.Ordinal)=>L("ErrVersion")+"（"+code+"）",
        _=>code
    };
    private static string ReadLegal()
    {
        try{return File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"LICENSE"))+"\n\n"+File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"THIRD-PARTY-NOTICES.md"))+"\n\n"+string.Join("\n\n",Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"licenses")).Select(File.ReadAllText));}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){return ex.Message;}
    }
    private void AddTab(string key,Control child)=>tabs.Items.Add(new TabItem{Header=L(key),FontSize=14,Content=Scroll(child)});
    public void UpdateStatus(){status.Text=app.Status;}
    // 恢復、系統匣切換只改顯示與穿透；同步這兩個核取方塊，不重建畫面，保留使用者正在編輯的欄位與焦點。
    public void SyncState(){visibleBox.IsChecked=Vm.ClockVisible;clickBox.IsChecked=Vm.ClickThrough;clickBox.IsEnabled=app.Clock.HotkeyAvailable;}
    private void ApplyPalette()
    {
        bool dark=ActualThemeVariant==Avalonia.Styling.ThemeVariant.Dark;
        Background=Brush.Parse(dark?"#1B1D21":"#F5F6F8");Foreground=Brush.Parse(dark?"#F2F3F5":"#20242C");
        if(colorError is not null)colorError.Foreground=ErrorBrush();
        if(hotkeyStatus is not null && !app.Clock.HotkeyAvailable)hotkeyStatus.Foreground=ErrorBrush();
    }
    private IBrush ErrorBrush()=>Brush.Parse(ActualThemeVariant==Avalonia.Styling.ThemeVariant.Dark?"#FF929C":"#BA2636");
    public void UpdatePreview()
    {
        previewTimer.Stop();var s=Vm.Settings;
        preview.Text=ClockMath.Format(DateTime.Now,s);preview.FontFamily=new(s.FontFamily);preview.FontSize=Math.Min(s.FontSize,56);preview.Foreground=Brush.Parse(s.TextColor);preview.Opacity=s.Opacity;
        // 深色文字在預設深底上幾乎看不見；對比不足 3:1 時改用淺底，其餘維持共用深底。
        previewFrame.Background=ClockMath.ContrastRatio(Color.Parse(s.TextColor),PreviewDark.Color)>=3?PreviewDark:PreviewLight;
        if(IsVisible && !app.Clock.IsVisible){previewTimer.Interval=ClockMath.NextUpdate(DateTime.Now,s.ShowSeconds);previewTimer.Start();}
    }
}
