using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using GameClockOverlay.AvaloniaApp.Services;
using GameClockOverlay.AvaloniaApp.Services.Rtss;
using GameClockOverlay.AvaloniaApp.ViewModels;
using GameClockOverlay.AvaloniaApp.Views;

namespace GameClockOverlay.AvaloniaApp;

public sealed class App : Application
{
    internal static string? TestSettingsPath;
    internal static Action<App>? TestReady;
    internal static Action? TestBeforeSave;
    internal static string? TestRtssMap;
    internal static TimeSpan? TestRtssRetry;
    private IClassicDesktopStyleApplicationLifetime desktop=null!;
    private SettingsStore store=null!;
    private readonly SemaphoreSlim saveGate=new(1,1);
    private readonly DispatcherTimer saveTimer=new(){Interval=TimeSpan.FromMilliseconds(450)};
    private TrayIcon? tray;
    private SettingsWindow? window;
    public SettingsViewModel ViewModel {get;private set;}=null!;
    public ClockWindow Clock {get;private set;}=null!;
    public RtssClockService Rtss {get;private set;}=null!;
    public RtssInstallInfo? RtssInstallation {get;private set;}
    public string Status {get;private set;}="";
    public string? LoadWarning {get;private set;}
    public bool Exiting {get;private set;}
    public int ActivationCount {get;private set;}
    public bool TrayCreated=>tray is not null;
    internal NativeMenu? TrayMenu=>tray?.Menu;
    public SettingsWindow SettingsWindow=>window??=new(this);
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        UiStrings.Initialize();
        Dispatcher.UIThread.UnhandledException+=Unhandled;
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)throw new InvalidOperationException("Desktop lifetime required.");
        desktop=lifetime;desktop.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        store=new(TestSettingsPath??SettingsStore.DefaultPath);
        ViewModel=new(store.Load());Strings.Use(ViewModel.Language);
        LoadWarning=store.Error is null?null:Strings.Get("LoadFailed");
        Status=LoadWarning??Strings.Get("Saved");
        // RTSS 後端與桌面時鐘彼此獨立：背景執行緒寫入共享記憶體，狀態變更再投遞到 UI 執行緒顯示。
        Rtss=new(TestRtssMap??SharedOsd.MapName,TestRtssRetry,TestRtssRetry);
        Rtss.StatusChanged+=_=>Dispatcher.UIThread.Post(()=>{if(!Exiting)window?.UpdateRtss();});
        ConfigureRtss();
        _=Task.Run(()=>{var found=RtssInstall.Find();Dispatcher.UIThread.Post(()=>{RtssInstallation=found;if(!Exiting)window?.UpdateRtss();});});
        Clock=new(()=>ViewModel.Settings,Changed,Recover);Clock.Show();
        ApplyTheme();
        ViewModel.Changed+=ViewModelChanged;
        Clock.DisplaysChanged+=DisplaysChanged;
        Clock.TimeChanged+=ClockTimeChanged;
        tray=new TrayIcon
        {
            Icon=new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://GameClockOverlay.Avalonia/clock.ico"))),
            ToolTipText="GameClock Overlay · Avalonia",IsVisible=true
        };
        tray.Clicked+=(_,_)=>Recover();RefreshTray();
        TrayIcon.SetIcons(this,[tray]);
        saveTimer.Tick+=SaveTimerTick;
        desktop.Exit+=(_,_)=>Cleanup();
        desktop.ShutdownRequested+=SessionEnding;
        Program.Instance?.Listen(()=>Dispatcher.UIThread.Post(()=>{if(!Exiting){ActivationCount++;Recover();}}));
        OpenSettings();
        // 啟動不主動保存；只有實際變更（Changed）才啟動防彈跳計時器，避免每次啟動都輪替 .bak。
        if(TestReady is not null)Dispatcher.UIThread.Post(()=>TestReady(this),DispatcherPriority.Background);
        base.OnFrameworkInitializationCompleted();
    }
    private void ClockTimeChanged(string text){if(window?.IsVisible==true)window.UpdatePreview();}
    private void DisplaysChanged(){if(window?.IsVisible==true)window.Build();}
    private async void SaveTimerTick(object? sender,EventArgs e){saveTimer.Stop();await SaveAsync();}
    private void ViewModelChanged(string name)
    {
        if(Exiting)return;
        Strings.Use(ViewModel.Language);ApplyTheme();Changed();
        if(name is "Language" or "Import" or "External")window?.Build();
    }
    public void Changed()
    {
        if(Exiting)return;
        Clock.Apply();ConfigureRtss();RefreshTray();window?.UpdatePreview();
        Status=LoadWarning??Strings.Get("Pending");window?.UpdateStatus();
        saveTimer.Stop();saveTimer.Start();
    }
    private void ConfigureRtss(){var s=ViewModel.Settings;Rtss.Configure(s.GameClockEnabled,s.ShowSeconds,s.TwelveHour,Enum.TryParse<OsdPlacement>(s.GameClockPosition,out var p)?p:OsdPlacement.Rtss);}
    private void ApplyTheme()=>RequestedThemeVariant=ViewModel.Theme switch{"Dark"=>ThemeVariant.Dark,"Light"=>ThemeVariant.Light,_=>ThemeVariant.Default};
    public void OpenSettings()
    {
        if(Exiting)return;
        var target=SettingsWindow;
        if(target.WindowState==WindowState.Minimized)target.WindowState=WindowState.Normal;
        if(!target.IsVisible)target.Show();target.Activate();target.UpdatePreview();
    }
    public void Recover()
    {
        if(Exiting)return;
        ViewModel.Recover();window?.SyncState();OpenSettings();
    }
    public void ToggleClock(){ViewModel.ClockVisible=!ViewModel.ClockVisible;window?.SyncState();}
    private void RefreshTray()
    {
        if(tray is null)return;
        var menu=new NativeMenu();
        var toggle=new NativeMenuItem(Strings.Get("ToggleClock")){IsChecked=ViewModel.ClockVisible};toggle.Click+=(_,_)=>ToggleClock();menu.Items.Add(toggle);
        // 「開啟設定」與快捷鍵同樣呼叫 Recover；只在快捷鍵確實註冊成功時標示按鍵。
        var settings=new NativeMenuItem(Clock.HotkeyAvailable?string.Format(Strings.Get("OpenSettingsHotkey"),ClockWindow.HotkeyLabel):Strings.Get("OpenSettings"));settings.Click+=(_,_)=>Recover();menu.Items.Add(settings);
        var pass=new NativeMenuItem(Strings.Get("ClickThrough")){IsChecked=ViewModel.ClickThrough,IsEnabled=Clock.HotkeyAvailable};
        pass.Click+=(_,_)=>{ViewModel.ClickThrough=!ViewModel.ClickThrough;window?.SyncState();};menu.Items.Add(pass);
        var about=new NativeMenuItem(Strings.Get("About"));about.Click+=(_,_)=>{OpenSettings();SettingsWindow.SelectedTab=4;};menu.Items.Add(about);
        menu.Items.Add(new NativeMenuItemSeparator());
        var quit=new NativeMenuItem(Strings.Get("Exit"));quit.Click+=async(_,_)=>await QuitAsync();menu.Items.Add(quit);tray.Menu=menu;
    }
    public async Task ImportAsync()
    {
        try {var value=await Task.Run(()=>SettingsStore.Read(SettingsStore.LegacyPath));ViewModel.Replace(value);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {Status=Strings.Get("ImportFailed");window?.UpdateStatus();}
    }
    public async Task<bool> SaveAsync()
    {
        var snapshot=ViewModel.Settings.Copy();
        await saveGate.WaitAsync();
        try
        {
            bool success=await Task.Run(()=>{TestBeforeSave?.Invoke();return store.Save(snapshot);});
            Status=success?(LoadWarning??Strings.Get("Saved")):Strings.Get("SaveFailed");
            window?.UpdateStatus();return success;
        }
        finally{saveGate.Release();}
    }
    public async Task QuitAsync()
    {
        if(Exiting)return;Exiting=true;saveTimer.Stop();
        // 保存出現未預期例外時仍須結束，否則程序停在 Exiting 並持有單一執行個體 Mutex。
        try{await SaveAsync();}
        finally{desktop.Shutdown();}
    }
    // Windows 登出／關機（WM_QUERYENDSESSION）與 TryShutdown 都走這裡。不取消要求：同步保存最後狀態，
    // 並先設定 Exiting，讓設定視窗的 Closing 不再改為隱藏，否則 Avalonia 仍會回報取消而阻止結束工作階段。
    private void SessionEnding(object? sender,ShutdownRequestedEventArgs e)
    {
        if(Exiting)return;
        Exiting=true;saveTimer.Stop();
        Status=store.SaveFinal(ViewModel.Settings.Copy())?(LoadWarning??Strings.Get("Saved")):Strings.Get("SaveFailed");
        // 同步清除自己的 OSD 槽；等待有上限（約 2.5 秒），不會否決結束要求。
        Rtss.Shutdown();
    }
    private void Cleanup()
    {
        Exiting=true;saveTimer.Stop();
        ViewModel.Changed-=ViewModelChanged;Clock.DisplaysChanged-=DisplaysChanged;Clock.TimeChanged-=ClockTimeChanged;
        tray?.Dispose();tray=null;
        Rtss.Dispose();
        Dispatcher.UIThread.UnhandledException-=Unhandled;
    }
    private void Unhandled(object? sender,DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled=true;
        try{Directory.CreateDirectory(Path.GetDirectoryName(store.Path)!);File.AppendAllText(Path.Combine(Path.GetDirectoryName(store.Path)!,"errors.log"),DateTimeOffset.Now+" "+e.Exception+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}
        Status=Strings.Get("OperationFailed");window?.UpdateStatus();
        // 未知 UI 例外後正常保存並結束，避免留下部分更新的視窗。
        _=QuitAsync();
    }
}
