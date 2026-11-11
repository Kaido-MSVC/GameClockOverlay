using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GameClockOverlay;
using Forms = System.Windows.Forms;

internal static class FixChecks
{
    private static T Field<T>(object item, string name) => (T)item.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(item)!;
    internal static string Exe => Path.Combine(AppContext.BaseDirectory, "GameClockOverlay.exe");
    internal static async Task LaunchSecond()
    {
        using var child = Process.Start(new ProcessStartInfo(Exe) { UseShellExecute = false, CreateNoWindow = true })!;
        var exit = child.WaitForExitAsync();
        if (await Task.WhenAny(exit, Task.Delay(5000)) != exit && !child.HasExited)
        { child.Kill(); throw new Exception("Second EXE did not exit in 5 seconds"); }
        if (!child.HasExited) await child.WaitForExitAsync();
        if (child.ExitCode != 0) throw new Exception("Second EXE failed: " + child.ExitCode);
    }
    public static int Instances()
    {
        int count = 0;
        void Check(string name, bool value) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); count++; }
        try
        {
            using (var first = new SingleInstance())
            {
                Check("First mutex owner acquired", first.IsFirst);
                using var second = new SingleInstance();
                Check("Second mutex does not become primary", !second.IsFirst);
                var elapsed = Stopwatch.StartNew();
                LaunchSecond().GetAwaiter().GetResult();
                Check("Real second EXE exits while primary does not pump or listen", elapsed.Elapsed < TimeSpan.FromSeconds(5));
                using var signal = new ManualResetEvent(false);
                first.Listen(() => signal.Set());
                Check("Signal before listener registration is retained", signal.WaitOne(3000));
                signal.Reset(); second.RequestRecovery();
                Check("Subsequent recovery signal received", signal.WaitOne(3000));
            }
            using (var restarted = new SingleInstance()) Check("Primary ownership released and reacquired after exit", restarted.IsFirst);
            Console.WriteLine($"All {count} instance checks passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.GetFullPath(Path.Combine(directory, "fix-settings.json"));
        var app = new App(path) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int count = 0;
        void Check(string name, bool value) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); count++; }
        Exception? failure = null;
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
        {
            try
            {
                var w = Field<SettingsWindow>(app, "settingsWindow");
                var menu = Field<Forms.NotifyIcon>(app, "tray").ContextMenuStrip!;
                app.Settings.Position = "BottomRight";
                app.Settings.MonitorDevice = Forms.Screen.AllScreens.Length > 1 ? Forms.Screen.AllScreens[1].DeviceName : @"\\.\DISPLAY2-unavailable-test";
                app.Changed(); await Task.Delay(650);
                var initial = File.ReadAllText(path);
                var originalMonitor = app.Settings.MonitorDevice;
                app.Overlay.BeginPointer(new Point(100, 100), 1); app.Overlay.EndPointer();
                Check("Single click retains BottomRight", app.Settings.Position == "BottomRight");
                Check("Single click retains selected second monitor name", app.Settings.MonitorDevice == originalMonitor);
                await Task.Delay(550);
                Check("Single click does not save changed positioning", File.ReadAllText(path) == initial && !Field<DispatcherTimer>(app, "saveTimer").IsEnabled);
                app.Overlay.BeginPointer(new Point(100, 100), 1); app.Overlay.EndPointer();
                app.Overlay.BeginPointer(new Point(100, 100), 2); app.Overlay.EndPointer();
                Check("Double click opens existing settings without altering anchors", w.IsVisible && app.Settings.Position == "BottomRight" && app.Settings.MonitorDevice == originalMonitor);
                Check("Subpixel movement is below threshold", !OverlayWindow.ExceedsDragThreshold(.01, .01, new DpiScale(1, 1)));
                Check("Tiny movement is below threshold", !OverlayWindow.ExceedsDragThreshold(2, 2, new DpiScale(1, 1)));
                Check("High DPI threshold scales in physical pixels", !OverlayWindow.ExceedsDragThreshold(5, 5, new DpiScale(2, 2)));
                Check("Nonfinite movement rejected", !OverlayWindow.ExceedsDragThreshold(double.NaN, 100, new DpiScale(1, 1)));
                Check("Substantial movement exceeds threshold", OverlayWindow.ExceedsDragThreshold(80, 0, new DpiScale(1, 1)));
                Native.GetWindowRect(app.Overlay.Handle, out var before);
                var after = before; after.Left -= 80; after.Top -= 40;
                Native.SetWindowPos(app.Overlay.Handle, (nint)(-1), after.Left, after.Top, 0, 0, Native.NoSize | Native.NoActivateFlag);
                Check("Cancelled move does not commit", !app.Overlay.FinishDrag(before, after, false, new DpiScale(1, 1)));
                Native.GetWindowRect(app.Overlay.Handle, out var restored);
                Check("Cancelled move restores physical origin and settings", restored.Left == before.Left && restored.Top == before.Top && app.Settings.Position == "BottomRight" && app.Settings.MonitorDevice == originalMonitor);
                var tiny = before; tiny.Left += 1;
                Check("Invalid small completed move does not commit", !app.Overlay.FinishDrag(before, tiny, true, new DpiScale(1, 1)) && app.Settings.MonitorDevice == originalMonitor);
                var scrollBefore = Field<ScrollViewer>(w, "scroll");
                var bodyBefore = (StackPanel)scrollBefore.Content;
                Slider? fontEditor = null;
                foreach (var child in bodyBefore.Children) if (child is Slider slider) { fontEditor = slider; break; }
                fontEditor!.Value = 44;
                var content = w.Content;
                var scroll = Field<ScrollViewer>(w, "scroll"); w.UpdateLayout(); scroll.ScrollToEnd(); w.UpdateLayout();
                double offset = scroll.VerticalOffset;
                Native.SetWindowPos(app.Overlay.Handle, (nint)(-1), after.Left, after.Top, 0, 0, Native.NoSize | Native.NoActivateFlag);
                Check("Completed substantial drag commits", app.Overlay.FinishDrag(before, after, true, new DpiScale(1, 1)));
                Check("Drag switches to Free and clears monitor only on completion", app.Settings.Position == "Free" && app.Settings.MonitorDevice == "" && app.Settings.X == after.Left && app.Settings.Y == after.Top);
                Check("External position and monitor sync to open controls", Field<ComboBox>(w, "positionBox").SelectedIndex == 0 && Field<ComboBox>(w, "monitorBox").SelectedIndex == 0);
                Check("External sync retains unrelated font editor value", fontEditor.Value == 44 && app.Settings.FontSize == 44);
                Check("External sync preserves content and scroll", ReferenceEquals(content, w.Content) && scroll.VerticalOffset == offset);
                for (int i = 0; i < 4; i++) app.Recover();
                Check("Repeated recovery reuses settings window and content", ReferenceEquals(w, Field<SettingsWindow>(app, "settingsWindow")) && ReferenceEquals(content, w.Content));
                w.UpdateLayout(); Check("Repeated recovery preserves scroll", scroll.VerticalOffset == offset);
                Check("Recovery does not duplicate settings windows", CountSettings(app) == 1);
                await Task.Delay(650);
                var saved = new SettingsStore(path).Load();
                Check("Committed drag position persists to disk", saved.Position == "Free" && saved.MonitorDevice == "" && saved.X == after.Left && saved.Y == after.Top);
                w.SyncExternal();
                Check("Sync does not start another save", !Field<DispatcherTimer>(app, "saveTimer").IsEnabled);
                var pass = Field<CheckBox>(w, "clickThroughBox");
                for (int i = 0; i < 6; i++)
                {
                    menu.Items[1].PerformClick();
                    Check("Tray toggle uses live menu " + i, !menu.IsDisposed && ReferenceEquals(menu, Field<Forms.NotifyIcon>(app, "tray").ContextMenuStrip) && pass.IsChecked == app.Settings.ClickThrough);
                }
                for (int i = 0; i < 3; i++) { menu.Show(new System.Drawing.Point(40, 40)); menu.Close(); }
                Check("Menu survives repeated open and close", !menu.IsDisposed && menu.Items.Count == 5);
                int observed = 0; app.ErrorObserver = _ => observed++;
                app.RunTrayAction(() => throw new IOException("Injected tray failure"));
                typeof(App).GetMethod("FormsException", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, [app, new ThreadExceptionEventArgs(new InvalidOperationException("Injected WinForms failure"))]);
                Check("Tray and WinForms failures reach central handler", observed == 2 && app.ErrorCount == 2 && app.Overlay.IsVisible);
                Check("Error diagnostics retain exception details", File.ReadAllText(Path.Combine(directory, "errors.log")).Contains("Injected tray failure") && File.ReadAllText(Path.Combine(directory, "errors.log")).Contains("Injected WinForms failure"));
                foreach (var title in new[] { "Other ordinary window", "GameClock Overlay" })
                {
                    var spoof = new Window { Title = title, Width = 300, Height = 120 }; spoof.Show();
                    app.Settings.ClickThrough = true; app.Changed(); w.Hide();
                    await LaunchSecond(); await WaitRecovery(app);
                    Check("Real duplicate EXE recovers with window: " + title, !app.Settings.ClickThrough && w.IsVisible);
                    spoof.Close();
                }
                app.Settings.ClickThrough = true; app.Changed();
                await LaunchSecond(); await WaitRecovery(app);
                Check("Real duplicate EXE recovers with settings already open", w.IsVisible && !app.Settings.ClickThrough);
                var dialog = new Window { Title = "GameClock Overlay", Content = new TextBlock { Text = "Simulated settings read error dialog" }, Width = 400, Height = 150 };
                Exception? dialogFailure = null;
                dialog.Loaded += async (_, _) =>
                {
                    try { app.Settings.ClickThrough = true; app.Changed(); await LaunchSecond(); await WaitRecovery(app); }
                    catch (Exception ex) { dialogFailure = ex; }
                    finally { dialog.Close(); }
                };
                dialog.ShowDialog();
                Check("Duplicate EXE reaches primary while same-title modal error dialog is open", dialogFailure == null && !app.Settings.ClickThrough);
                w.Close(); app.Recover();
                Check("Closed settings window recreates normally", Field<SettingsWindow>(app, "settingsWindow").IsVisible && CountSettings(app) == 1);
                // Save known state for a separate process to verify the drag survives restart.
                app.Settings.Position = "Free"; app.Settings.X = after.Left; app.Settings.Y = after.Top; app.Overlay.Place(true);
                File.WriteAllText(Path.Combine(directory, "drag-expected.txt"), $"{after.Left},{after.Top}");
                menu.Items[4].PerformClick();
            }
            catch (Exception ex) { failure = ex; app.Quit(); }
        }));
        app.Run();
        if (failure != null) { Console.Error.WriteLine(failure); return 1; }
        Check("Owned tray menu disposed on exit", Field<Forms.ContextMenuStrip>(app, "trayMenu").IsDisposed);
        Console.WriteLine($"All {count} fix checks passed."); return 0;
    }
    private static int CountSettings(App app)
    { int count = 0; foreach (Window w in app.Windows) if (w is SettingsWindow) count++; return count; }
    private static async Task WaitRecovery(App app)
    {
        var end = DateTime.UtcNow.AddSeconds(3);
        while (app.Settings.ClickThrough && DateTime.UtcNow < end) await Task.Delay(25);
    }
    public static int Restart(string directory)
    {
        var path = Path.GetFullPath(Path.Combine(directory, "fix-settings.json"));
        var expected = File.ReadAllText(Path.Combine(directory, "drag-expected.txt")).Split(',');
        var app = new App(path) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        bool okay = false;
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            okay = app.Settings.Position == "Free" && app.Settings.MonitorDevice == "" && app.Settings.X == int.Parse(expected[0]) && app.Settings.Y == int.Parse(expected[1]);
            app.Quit();
        }));
        app.Run(); Console.WriteLine((okay ? "PASS" : "FAIL") + ": Drag settings restored after new process startup"); return okay ? 0 : 1;
    }
}
