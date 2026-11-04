using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace GameClockOverlay;

public sealed class OverlayWindow : Window
{
    private readonly ClockSettings settings;
    private readonly Action changed;
    private readonly Action recover;
    private readonly TextBlock time;
    private readonly DispatcherTimer timer;
    private HwndSource? source;
    private bool positionReady;
    public nint Handle { get; private set; }
    public bool HotkeyAvailable { get; private set; }
    public OverlayWindow(ClockSettings settings, Action changed, Action recover)
    {
        this.settings = settings; this.changed = changed; this.recover = recover;
        Title = "GameClock Overlay";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
        ShowInTaskbar = false; ShowActivated = false; Width = 174; Height = 60;
        time = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 32,
            Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1.5, Opacity = 1 } };
        Content = new Border { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), Child = time };
        MouseLeftButtonDown += (_, e) =>
        {
            if (settings.ClickThrough) return;
            if (e.ClickCount == 2) { recover(); return; }
            settings.Position = "Free";
            try { DragMove(); } catch (InvalidOperationException) { }
            Place(); CapturePosition(); changed();
        };
        MouseRightButtonUp += (_, _) => recover();
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(Handle); source.AddHook(Messages);
            HotkeyAvailable = Native.RegisterHotKey(Handle, 1, 0x4000 | 1 | 2 | 4, 0x79); // Ctrl+Alt+Shift+F10, no keyboard hook.
            if (!HotkeyAvailable) settings.ClickThrough = false;
            Apply();
        };
        ContentRendered += (_, _) => { if (!positionReady) { Place(true); positionReady = true; } };
        SizeChanged += (_, _) => { if (positionReady) Dispatcher.BeginInvoke(() => Place()); };
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => UpdateTime(); timer.Start();
        Closed += (_, _) => { timer.Stop(); if (HotkeyAvailable) Native.UnregisterHotKey(Handle, 1); source?.RemoveHook(Messages); };
    }
    private nint Messages(nint hwnd, int msg, nint wp, nint lp, ref bool handled)
    {
        if ((msg == 0x0312 && wp == 1) || (uint)msg == Native.RecoveryMessage)
        { Dispatcher.BeginInvoke(recover); handled = true; }
        if (msg == 0x007E || msg == 0x001A) Dispatcher.BeginInvoke(() => Place()); // display/work-area changes
        return 0;
    }
    public void Apply()
    {
        Opacity = settings.Opacity; Width = settings.ShowSeconds ? 230 : 174;
        if (Handle != 0) Native.SetClickThrough(Handle, settings.ClickThrough && HotkeyAvailable);
        UpdateTime(); if (positionReady) Place();
    }
    private void UpdateTime()
    {
        var value = DateTime.Now.ToString(settings.ShowSeconds ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture);
        if (time.Text != value) time.Text = value;
    }
    public void Restore()
    {
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        if (!IsVisible) Show();
        Place(true);
    }
    public void CapturePosition()
    {
        if (IsVisible && WindowState == WindowState.Normal && Handle != 0 && Native.GetWindowRect(Handle, out var r)) { settings.X = r.Left; settings.Y = r.Top; }
    }
    public void Place(bool restore = false)
    {
        if (!IsVisible || WindowState != WindowState.Normal || Handle == 0 || !Native.GetWindowRect(Handle, out var r)) return;
        var screen = restore && settings.X.HasValue && settings.Y.HasValue
            ? Forms.Screen.FromPoint(new System.Drawing.Point(settings.X.Value, settings.Y.Value)) : Forms.Screen.FromHandle(Handle);
        var area = screen.WorkingArea;
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        int x = restore ? settings.X ?? area.Right - w - 16 : r.Left;
        int y = restore ? settings.Y ?? area.Top + 16 : r.Top;
        if (settings.Position != "Free")
        {
            x = settings.Position.EndsWith("Right", StringComparison.Ordinal) ? area.Right - w - 16 : area.Left + 16;
            y = settings.Position.StartsWith("Bottom", StringComparison.Ordinal) ? area.Bottom - h - 16 : area.Top + 16;
        }
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - w));
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - h));
        Native.SetWindowPos(Handle, (nint)(-1), x, y, 0, 0, Native.NoSize | Native.NoActivateFlag);
        CapturePosition();
    }
}
