using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using GameClockOverlay;

internal static class Program
{
    private static readonly List<string> passed = new();
    private static void Check(string name, bool condition)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        passed.Add(name); Console.WriteLine("PASS: " + name);
    }
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--lifecycle") return Lifecycle.Run(args[1], args.Length > 2);
        if (args.Length > 0 && args[0] == "--interactive")
        {
            var desktop = new Application();
            var target = new Window { Title = "GameClock windowed input test", WindowState = WindowState.Maximized };
            int clicks = 0;
            var button = new Button { Content = "Clicks: 0 — click through the clock digits", FontSize = 28 };
            button.Click += (_, _) => button.Content = $"Clicks: {++clicks} — input received by underlying window";
            target.Content = button; desktop.Run(target); return 0;
        }
        string dir = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "GameClockOverlay-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var test in new[] { ("zh-TW", "zh-TW"), ("zh-CN", "zh-TW"), ("ja-JP", "ja"), ("en-US", "en"), ("de-DE", "en") })
                Check("System language " + test.Item1, Strings.Resolve("auto", test.Item1) == test.Item2);
            Check("Manual language takes priority", Strings.Resolve("ja", "zh-TW") == "ja");
            Check("Every label has three nonempty translations", Strings.Values.Values.All(x => x.Length == 3 && x.All(s => !string.IsNullOrWhiteSpace(s))));
            var path = Path.Combine(dir, "settings.json"); var store = new SettingsStore(path);
            var settings = store.Load(); Check("First launch defaults", settings.Position == "TopRight" && !settings.ClickThrough && !settings.ShowSeconds);
            settings.Language = "ja"; settings.ShowSeconds = true; settings.ClickThrough = true; settings.Opacity = .55; settings.Position = "Free"; settings.X = 345; settings.Y = 234;
            Check("Initial save", store.Save(settings));
            var reloaded = store.Load();
            Check("Settings round trip", reloaded.Language == "ja" && reloaded.ShowSeconds && reloaded.ClickThrough && reloaded.Opacity == .55 && reloaded.Position == "Free" && reloaded.X == 345 && reloaded.Y == 234);
            settings.Opacity = .65; Check("Atomic replacement and backup", store.Save(settings) && File.Exists(path + ".bak") && !File.Exists(path + ".tmp"));
            File.WriteAllText(path, "{broken"); var recovered = store.Load();
            Check("Malformed settings recover backup", store.Recovered && recovered.Opacity == .55);
            Check("Malformed original preserved", Directory.GetFiles(dir, "settings.json.invalid-*").Length == 1);
            var invalid = new ClockSettings { Language = "xx", Position = "bad", Opacity = -100 }; invalid.Normalize();
            Check("Invalid fields normalized", invalid.Language == "auto" && invalid.Position == "TopRight" && invalid.Opacity == .25);
            invalid.Opacity = double.NaN; invalid.Normalize(); Check("Nonfinite opacity normalized", invalid.Opacity == .9);
            var blocked = new SettingsStore(dir); Check("Unwritable target reports save failure", !blocked.Save(new()) && blocked.Error != null);
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Exception? guiFailure = null;
            var live = new ClockSettings { Position = "Free", X = 345, Y = 234 };
            var overlay = new OverlayWindow(live, () => { }, () => { });
            var host = new Window { Title = "GameClock windowed test host", Width = 800, Height = 500 };
            host.Content = new TextBlock { Text = "Windowed rendering test host", FontSize = 28 };
            host.Show(); overlay.Show();
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    Check("WPF topmost transparent window", overlay.Topmost && overlay.AllowsTransparency && !overlay.ShowActivated && !overlay.ShowInTaskbar);
                    Check("Recovery hotkey registered", overlay.HotkeyAvailable);
                    Native.GetWindowRect(overlay.Handle, out var r);
                    Check("Free position restored on first render", r.Left == 345 && r.Top == 234);
                    long style = Native.GetWindowLongPtr(overlay.Handle, Native.ExStyle).ToInt64();
                    Check("Native layered + no-activation styles", (style & 0x80000) != 0 && (style & Native.NoActivate) != 0);
                    live.ClickThrough = true; overlay.Apply();
                    Check("Native click-through enabled", (Native.GetWindowLongPtr(overlay.Handle, Native.ExStyle).ToInt64() & Native.Transparent) != 0);
                    live.ClickThrough = false; overlay.Apply();
                    Check("Native click-through removed", (Native.GetWindowLongPtr(overlay.Handle, Native.ExStyle).ToInt64() & Native.Transparent) == 0);
                    live.ShowSeconds = true; live.Opacity = .45; overlay.Apply();
                    Check("Seconds and opacity applied", overlay.Width == 230 && overlay.Opacity == .45 && ((TextBlock)((Border)overlay.Content).Child).Text.Length == 8);
                    live.ShowSeconds = false; overlay.Apply();
                    Check("HH:mm default format", ((TextBlock)((Border)overlay.Content).Child).Text.Length == 5);
                    foreach (var corner in new[] { "TopLeft", "TopRight", "BottomLeft", "BottomRight" })
                    {
                        live.Position = corner; overlay.Place(); Native.GetWindowRect(overlay.Handle, out r);
                        var area = System.Windows.Forms.Screen.FromHandle(overlay.Handle).WorkingArea;
                        Check("Corner " + corner, (corner.EndsWith("Left", StringComparison.Ordinal) ? r.Left == area.Left + 16 : r.Right == area.Right - 16) && (corner.StartsWith("Top", StringComparison.Ordinal) ? r.Top == area.Top + 16 : r.Bottom == area.Bottom - 16));
                    }
                    live.Position = "Free"; live.X = -99999; live.Y = -99999; overlay.Place(true); Native.GetWindowRect(overlay.Handle, out r);
                    Check("Offscreen position recovered", System.Windows.Forms.Screen.AllScreens.Any(s => s.WorkingArea.Contains(r.Left, r.Top)));
                    live.Position = "Free"; live.X = 345; live.Y = 234; overlay.Place(true);
                    overlay.WindowState = WindowState.Minimized;
                    overlay.CapturePosition(); overlay.Apply();
                    Check("Minimized overlay hidden without taskbar button", !overlay.IsVisible && !overlay.ShowInTaskbar);
                    Check("Minimizing preserves saved coordinates", live.X == 345 && live.Y == 234);
                    overlay.Restore(); Native.GetWindowRect(overlay.Handle, out r);
                    Check("Overlay restored visible at saved position", overlay.IsVisible && overlay.WindowState == WindowState.Normal && r.Left == 345 && r.Top == 234);
                    overlay.Close();
                    var hostHandle = new WindowInteropHelper(host).Handle;
                    Check("Hotkey released on close", Native.RegisterHotKey(hostHandle, 2, 0x4007, 0x79));
                    var conflictSettings = new ClockSettings { ClickThrough = true };
                    var conflict = new OverlayWindow(conflictSettings, () => { }, () => { }); conflict.Show();
                    Check("Hotkey conflict disables click-through", !conflict.HotkeyAvailable && !conflictSettings.ClickThrough);
                    conflict.Close(); Native.UnregisterHotKey(hostHandle, 2);
                }
                catch (Exception ex) { guiFailure = ex; }
                finally { host.Close(); app.Shutdown(); }
            }));
            app.Run();
            if (guiFailure != null) throw guiFailure;
            File.WriteAllText(Path.Combine(dir, "result.json"), JsonSerializer.Serialize(new { Passed = passed.Count, Checks = passed }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"All {passed.Count} checks passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
