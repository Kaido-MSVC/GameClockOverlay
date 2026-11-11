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
    private bool closed;
    private bool placementQueued;
    private Point? pointerDown;
    private DpiScale dragDpi;
    private bool dragging;
    private bool dragCancelled;
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
        MouseLeftButtonDown += (_, e) => { BeginPointer(PointToScreen(e.GetPosition(this)), e.ClickCount); e.Handled = true; };
        MouseLeftButtonUp += (_, _) => EndPointer();
        LostMouseCapture += (_, _) => pointerDown = null;
        MouseMove += (_, e) =>
        {
            if (pointerDown is not Point start || dragging) return;
            if (e.LeftButton != MouseButtonState.Pressed || settings.ClickThrough) { CancelPendingDrag(); return; }
            var current = PointToScreen(e.GetPosition(this));
            if (!ExceedsDragThreshold(current.X - start.X, current.Y - start.Y, dragDpi)) return;
            CancelPendingDrag();
            if (!Native.GetWindowRect(Handle, out var before)) return;
            dragging = true; dragCancelled = false;
            bool completed = false;
            try
            {
                DragMove(); // Windows owns move-loop completion / Escape cancellation.
                completed = !dragCancelled && Mouse.LeftButton == MouseButtonState.Released;
            }
            catch (InvalidOperationException ex) { System.Diagnostics.Trace.TraceWarning("Clock drag aborted: {0}", ex); }
            finally
            {
                dragging = false;
                if (Native.GetWindowRect(Handle, out var after)) FinishDrag(before, after, completed, dragDpi);
            }
        };
        MouseRightButtonUp += (_, _) => { CancelPendingDrag(); recover(); };
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
        SizeChanged += (_, _) => { if (positionReady) QueuePlacement(); };
        DpiChanged += (_, _) => QueuePlacement(); // Let WPF apply the suggested DPI rectangle first.
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => UpdateTime();
        Closed += (_, _) => { closed = true; timer.Stop(); if (HotkeyAvailable) Native.UnregisterHotKey(Handle, 1); source?.RemoveHook(Messages); };
    }
    private nint Messages(nint hwnd, int msg, nint wp, nint lp, ref bool handled)
    {
        if ((msg == 0x0312 && wp == 1) || (uint)msg == Native.RecoveryMessage)
        { Dispatcher.BeginInvoke(recover); handled = true; }
        if (dragging && (msg == 0x001F || msg == 0x007E)) dragCancelled = true;
        if (msg == 0x007E || msg == 0x001A) QueuePlacement(); // display/work-area changes
        if (msg == 0x001E || msg == 0x0218) Dispatcher.BeginInvoke(() => { if (!closed) UpdateTime(); }); // time change / power resume
        return 0;
    }
    internal void BeginPointer(Point screenPoint, int clickCount)
    {
        if (settings.ClickThrough || closed) return;
        CancelPendingDrag();
        if (clickCount == 2) { recover(); return; }
        pointerDown = screenPoint; dragDpi = VisualTreeHelper.GetDpi(this);
        if (!CaptureMouse()) pointerDown = null;
    }
    internal void EndPointer() => CancelPendingDrag();
    private void CancelPendingDrag()
    {
        pointerDown = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }
    internal static bool ExceedsDragThreshold(double dx, double dy, DpiScale dpi) =>
        double.IsFinite(dx) && double.IsFinite(dy) &&
        (Math.Abs(dx) >= Math.Max(4, SystemParameters.MinimumHorizontalDragDistance) * dpi.DpiScaleX ||
         Math.Abs(dy) >= Math.Max(4, SystemParameters.MinimumVerticalDragDistance) * dpi.DpiScaleY);

    // Shared commit boundary: settings remain untouched until a completed, substantial move.
    internal bool FinishDrag(Native.Rect before, Native.Rect after, bool completed, DpiScale dpi)
    {
        if (closed) return false;
        if (!completed || settings.ClickThrough ||
            !ExceedsDragThreshold((double)after.Left - before.Left, (double)after.Top - before.Top, dpi))
        {
            Native.SetWindowPos(Handle, (nint)(-1), before.Left, before.Top, 0, 0, Native.NoSize | Native.NoActivateFlag);
            return false;
        }
        settings.Position = "Free"; settings.MonitorDevice = "";
        settings.X = after.Left; settings.Y = after.Top;
        Place(true); changed();
        return true;
    }
    private void QueuePlacement()
    {
        if (closed || placementQueued) return;
        placementQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            placementQueued = false;
            if (!closed) Place();
        }));
    }
    public void Apply()
    {
        if (settings.ClickThrough) { CancelPendingDrag(); if (dragging) dragCancelled = true; }
        settings.Normalize();
        Opacity = settings.Opacity; time.FontSize = settings.FontSize;
        Width = (settings.ShowSeconds ? 230 : 174) * settings.FontSize / 32;
        Height = 60 * settings.FontSize / 32;
        if (Handle != 0) Native.SetClickThrough(Handle, settings.ClickThrough && HotkeyAvailable);
        UpdateTime(); if (positionReady) Place();
    }
    private void UpdateTime()
    {
        if (closed) return;
        var now = DateTime.Now;
        var value = now.ToString(settings.ShowSeconds ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture);
        if (time.Text != value) time.Text = value;
        timer.Interval = NextUpdate(now, settings.ShowSeconds);
        timer.Start();
    }
    internal static TimeSpan NextUpdate(DateTime now, bool seconds)
    {
        long unit = seconds ? TimeSpan.TicksPerSecond : TimeSpan.TicksPerMinute;
        return TimeSpan.FromTicks(unit - now.Ticks % unit + TimeSpan.TicksPerMillisecond * 20);
    }
    internal bool TimerRunning => timer.IsEnabled;
    public void Restore()
    {
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        if (!IsVisible) Show();
        Place(true);
    }
    public void CapturePosition()
    {
        if (!dragging && IsVisible && WindowState == WindowState.Normal && Handle != 0 && Native.GetWindowRect(Handle, out var r)) { settings.X = r.Left; settings.Y = r.Top; }
    }
    public void Place(bool restore = false)
    {
        if (dragging || !IsVisible || WindowState != WindowState.Normal || Handle == 0 || !Native.GetWindowRect(Handle, out var r)) return;
        var screen = Array.Find(Forms.Screen.AllScreens, s => s.DeviceName == settings.MonitorDevice)
            ?? (restore && settings.X.HasValue && settings.Y.HasValue
                ? Forms.Screen.FromPoint(new System.Drawing.Point(settings.X.Value, settings.Y.Value)) : Forms.Screen.FromHandle(Handle));
        var area = settings.UseMonitorBounds ? screen.Bounds : screen.WorkingArea;
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
