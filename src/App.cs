using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace GameClockOverlay;

public sealed class App : Application
{
    private readonly string? settingsPath;
    public App()
    {
        DispatcherUnhandledException += (_, e) => { e.Handled = true; ReportError(e.Exception); Shutdown(1); };
        Forms.Application.SetUnhandledExceptionMode(Forms.UnhandledExceptionMode.CatchException);
        Forms.Application.ThreadException += FormsException;
    }
    // Isolate lifecycle regression tests from the user's settings.
    internal App(string settingsPath) : this() => this.settingsPath = settingsPath;
    private SingleInstance? instance;
    private volatile bool exiting;
    private int recoveryQueued;
    private bool recovering;
    private Forms.ContextMenuStrip? trayMenu;
    internal Action<Exception>? ErrorObserver { get; set; }
    internal int ErrorCount { get; private set; }
    private Forms.NotifyIcon? tray;
    private Icon? icon;
    private SettingsWindow? settingsWindow;
    private AboutWindow? aboutWindow;
    private DispatcherTimer saveTimer = null!;
    public ClockSettings Settings { get; private set; } = null!;
    public SettingsStore Store { get; private set; } = null!;
    public OverlayWindow Overlay { get; private set; } = null!;

    [STAThread]
    public static void Main()
    {
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Run();
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new SingleInstance();
        if (!instance.IsFirst)
        {
            try { instance.RequestRecovery(); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Recovery signal failed: {0}", ex); Shutdown(1); return; }
            Shutdown(); return;
        }
        Store = new SettingsStore(settingsPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameClockOverlay", "settings.json"));
        Settings = Store.Load(); Strings.Use(Settings.Language);
        string? loadError = Store.Error;
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        Overlay = new OverlayWindow(Settings, Changed, Recover);
        MainWindow = Overlay;
        // Create the tray recovery path before showing a potentially click-through overlay.
        icon = CreateIcon();
        tray = new Forms.NotifyIcon { Icon = icon, Text = "GameClock Overlay", Visible = true };
        tray.DoubleClick += (_, _) => RunTrayAction(Recover);
        instance.Listen(QueueRecovery);
        RefreshTray(); Overlay.Show();
        if (loadError != null) MessageBox.Show(Strings.Get("LoadFailed"), "GameClock Overlay", MessageBoxButton.OK, MessageBoxImage.Information);
        OpenSettings();
    }
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var face = new SolidBrush(Color.FromArgb(26, 48, 68)); graphics.FillEllipse(face, 1, 1, 30, 30);
        using var pen = new Pen(Color.White, 2.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        graphics.DrawEllipse(pen, 4, 4, 24, 24); graphics.DrawLine(pen, 16, 16, 16, 9); graphics.DrawLine(pen, 16, 16, 22, 19);
        var handle = bitmap.GetHicon();
        try { using var borrowed = Icon.FromHandle(handle); return (Icon)borrowed.Clone(); }
        finally { Native.DestroyIcon(handle); }
    }
    private void QueueRecovery()
    {
        if (exiting || Dispatcher.HasShutdownStarted || Interlocked.Exchange(ref recoveryQueued, 1) != 0) return;
        // BeginInvoke never waits for a busy or modal UI. Shutdown races leave only a no-op.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Interlocked.Exchange(ref recoveryQueued, 0);
            if (!exiting) RunTrayAction(Recover);
        }));
    }
    private void FormsException(object sender, ThreadExceptionEventArgs e) => ReportError(e.Exception);
    internal void RunTrayAction(Action action)
    {
        if (exiting) return;
        try { if (Dispatcher.CheckAccess()) action(); else Dispatcher.Invoke(action); }
        catch (Exception ex) { ReportError(ex); }
    }
    private void ReportError(Exception error)
    {
        ErrorCount++;
        System.Diagnostics.Trace.TraceError("{0}", error);
        try
        {
            var path = Path.Combine(Path.GetDirectoryName(settingsPath ?? Store?.Path ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameClockOverlay", "settings.json"))!, "errors.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, DateTimeOffset.Now.ToString("O") + " " + error + Environment.NewLine);
        }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException)
        { System.Diagnostics.Trace.TraceError("Error log unavailable: {0}", logError); }
        if (ErrorObserver != null) { ErrorObserver(error); return; }
        MessageBox.Show(Strings.Get("OperationFailed"), "GameClock Overlay", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    public void RefreshTray()
    {
        if (tray == null || exiting) return;
        if (trayMenu == null)
        {
            trayMenu = new Forms.ContextMenuStrip();
            trayMenu.Items.Add("", null, (_, _) => RunTrayAction(Recover));
            var pass = new Forms.ToolStripMenuItem();
            pass.Click += (_, _) => RunTrayAction(() => { Settings.ClickThrough = !Settings.ClickThrough; Changed(); });
            trayMenu.Items.Add(pass); trayMenu.Items.Add(new Forms.ToolStripSeparator());
            trayMenu.Items.Add("", null, (_, _) => RunTrayAction(OpenAbout));
            // Leave the Click event stack before disposing its owning menu on shutdown.
            trayMenu.Items.Add("", null, (_, _) => RunTrayAction(() => Dispatcher.BeginInvoke(new Action(Quit))));
            tray.ContextMenuStrip = trayMenu;
        }
        trayMenu.Items[0].Text = Strings.Get("Unlock");
        var toggle = (Forms.ToolStripMenuItem)trayMenu.Items[1];
        toggle.Text = Strings.Get("ClickThrough"); toggle.Checked = Settings.ClickThrough; toggle.Enabled = Overlay.HotkeyAvailable;
        trayMenu.Items[3].Text = Strings.Get("About"); trayMenu.Items[4].Text = Strings.Get("Exit");
    }
    public void Changed()
    {
        if (!Overlay.HotkeyAvailable) Settings.ClickThrough = false;
        Overlay.Apply(); RefreshTray(); aboutWindow?.Refresh(); settingsWindow?.SyncExternal(); saveTimer.Stop(); saveTimer.Start();
    }
    private void Save()
    {
        Overlay.CapturePosition(); Store.Save(Settings); settingsWindow?.UpdateStatus();
    }
    public void Recover()
    {
        if (recovering || exiting) return;
        recovering = true;
        try { Settings.ClickThrough = false; Overlay.Restore(); Changed(); OpenSettings(); }
        finally { recovering = false; }
    }
    private void OpenSettings()
    {
        if (settingsWindow == null)
        {
            settingsWindow = new SettingsWindow(this);
            settingsWindow.Closed += (_, _) => { Save(); settingsWindow = null; };
            settingsWindow.Show();
        }
        if (settingsWindow.WindowState == WindowState.Minimized) settingsWindow.WindowState = WindowState.Normal;
        if (!settingsWindow.IsVisible) settingsWindow.Show();
        settingsWindow.Activate();
    }
    public void OpenAbout()
    {
        if (aboutWindow == null)
        {
            aboutWindow = new AboutWindow();
            aboutWindow.Closed += (_, _) => aboutWindow = null;
        }
        if (aboutWindow.WindowState == WindowState.Minimized) aboutWindow.WindowState = WindowState.Normal;
        if (!aboutWindow.IsVisible) aboutWindow.Show();
        aboutWindow.Activate();
    }
    public void Quit() => Shutdown();
    protected override void OnExit(ExitEventArgs e)
    {
        exiting = true;
        instance?.StopListening();
        Forms.Application.ThreadException -= FormsException;
        if (saveTimer != null) { saveTimer.Stop(); Save(); }
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        trayMenu?.Dispose(); icon?.Dispose(); instance?.Dispose(); instance = null; base.OnExit(e);
    }
}
