using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GameClockOverlay;
using Forms = System.Windows.Forms;

internal static class DisplayChecks
{
    public static int Run()
    {
        int count = 0;
        void Check(string name, bool value)
        {
            if (!value) throw new Exception("FAIL: " + name);
            Console.WriteLine("PASS: " + name); count++;
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var settings = new ClockSettings { ShowSeconds = true };
        var overlay = new OverlayWindow(settings, () => { }, () => { });
        Exception? failure = null;
        overlay.Show();
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
        {
            try
            {
                var text = (TextBlock)((Border)overlay.Content).Child;
                string before = text.Text;
                await Task.Delay(1400);
                Check("Live seconds advance with dispatcher timer", text.Text != before && text.Text.Length == 8);
                var now = new DateTime(2026, 11, 11, 12, 34, 30, 500);
                Check("Minute update sleeps until next minute", OverlayWindow.NextUpdate(now, false) == TimeSpan.FromMilliseconds(29520));
                Check("Seconds update sleeps until next second", OverlayWindow.NextUpdate(now, true) == TimeSpan.FromMilliseconds(520));
                foreach (double size in new[] { 16d, 32d, 96d })
                {
                    settings.FontSize = size; overlay.Apply(); overlay.UpdateLayout();
                    Check("Font and window size " + size, text.FontSize == size && overlay.Width == 230 * size / 32 && overlay.Height == 60 * size / 32);
                    text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Check("Digits fit overlay " + size, text.DesiredSize.Width <= overlay.Width && text.DesiredSize.Height <= overlay.Height);
                }
                settings.FontSize = 32; settings.Position = "BottomRight";
                foreach (var screen in Forms.Screen.AllScreens)
                {
                    settings.MonitorDevice = screen.DeviceName; settings.UseMonitorBounds = true; overlay.Apply();
                    await Task.Delay(150); overlay.UpdateLayout(); overlay.Place();
                    Native.GetWindowRect(overlay.Handle, out var rect);
                    Check("Full bounds placement " + screen.DeviceName, rect.Right == screen.Bounds.Right - 16 && rect.Bottom == screen.Bounds.Bottom - 16);
                    Console.WriteLine($"ENV: {screen.DeviceName} bounds={screen.Bounds} work={screen.WorkingArea} dpi={VisualTreeHelper.GetDpi(overlay).PixelsPerInchX}");
                    settings.UseMonitorBounds = false; overlay.Apply();
                    Native.GetWindowRect(overlay.Handle, out rect);
                    Check("Working area placement " + screen.DeviceName, rect.Right == screen.WorkingArea.Right - 16 && rect.Bottom == screen.WorkingArea.Bottom - 16);
                }
                settings.MonitorDevice = "disconnected-test-monitor"; overlay.Apply();
                Native.GetWindowRect(overlay.Handle, out var fallback);
                Check("Disconnected monitor falls back visibly", Array.Exists(Forms.Screen.AllScreens, s => s.WorkingArea.Contains(fallback.Left, fallback.Top)));
                overlay.Close();
                Check("Clock timer stops on close", !overlay.TimerRunning);
                var target = new Window { Title = "Hotkey release test" }; target.Show();
                var handle = new System.Windows.Interop.WindowInteropHelper(target).Handle;
                Check("Hotkey can be registered after close", Native.RegisterHotKey(handle, 8, 0x4007, 0x79));
                Native.UnregisterHotKey(handle, 8); target.Close();
            }
            catch (Exception ex) { failure = ex; }
            finally { if (overlay.IsVisible) overlay.Close(); app.Shutdown(); }
        }));
        app.Run();
        if (failure != null) { Console.Error.WriteLine(failure); return 1; }
        Console.WriteLine($"All {count} display checks passed."); return 0;
    }
}
