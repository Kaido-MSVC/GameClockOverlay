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
    public App() { }
    // Isolate lifecycle regression tests from the user's settings.
    internal App(string settingsPath) => this.settingsPath = settingsPath;
    private Mutex? instance;
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
        app.DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show("GameClock Overlay\n" + e.Exception.Message, "GameClock Overlay", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; app.Shutdown(1);
        };
        app.Run();
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, @"Local\GameClockOverlay.v0.1", out bool first);
        if (!first)
        {
            var hwnd = Native.FindWindow(null, "GameClock Overlay");
            if (hwnd != 0) Native.PostMessage(hwnd, Native.RecoveryMessage, 0, 0);
            else { Strings.Use("auto"); MessageBox.Show(Strings.Get("AlreadyRunning"), "GameClock Overlay"); }
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
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(Recover);
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
    public void RefreshTray()
    {
        if (tray == null) return;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Strings.Get("Unlock"), null, (_, _) => Dispatcher.Invoke(Recover));
        var pass = new Forms.ToolStripMenuItem(Strings.Get("ClickThrough")) { Checked = Settings.ClickThrough, Enabled = Overlay.HotkeyAvailable };
        pass.Click += (_, _) => Dispatcher.Invoke(() => { Settings.ClickThrough = !Settings.ClickThrough; Changed(); settingsWindow?.Refresh(); });
        menu.Items.Add(pass); menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Strings.Get("About"), null, (_, _) => Dispatcher.Invoke(OpenAbout));
        menu.Items.Add(Strings.Get("Exit"), null, (_, _) => Dispatcher.Invoke(Quit));
        var previous = tray.ContextMenuStrip; tray.ContextMenuStrip = menu; previous?.Dispose();
    }
    public void Changed()
    {
        if (!Overlay.HotkeyAvailable) Settings.ClickThrough = false;
        Overlay.Apply(); RefreshTray(); aboutWindow?.Refresh(); saveTimer.Stop(); saveTimer.Start();
    }
    private void Save()
    {
        Overlay.CapturePosition(); Store.Save(Settings); settingsWindow?.UpdateStatus();
    }
    public void Recover()
    {
        Settings.ClickThrough = false; Overlay.Restore(); Changed(); settingsWindow?.Refresh(); OpenSettings();
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
        if (saveTimer != null) { saveTimer.Stop(); Save(); }
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        icon?.Dispose(); instance?.Dispose(); base.OnExit(e);
    }
}
