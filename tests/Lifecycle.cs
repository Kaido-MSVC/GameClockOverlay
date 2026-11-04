using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GameClockOverlay;
using Forms = System.Windows.Forms;

internal static class Lifecycle
{
    private static int count;
    private static void Check(string name, bool condition)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name); count++;
    }
    private static T Field<T>(App app, string name) => (T)typeof(App).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
    public static int Run(string directory, bool reload)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "lifecycle-settings.json");
        var app = new App(path) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Exception? failure = null;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        void Guard(Action action)
        {
            try { action(); }
            catch (Exception ex) { failure = ex; timer.Stop(); app.Quit(); }
        }
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => Guard(() =>
        {
            var window = Field<SettingsWindow>(app, "settingsWindow");
            var tray = Field<Forms.NotifyIcon>(app, "tray");
            Check("Tray visible with recovery and exit commands", tray.Visible && tray.ContextMenuStrip!.Items.Count == 5);
            Check("Settings excluded from native taskbar", !window.ShowInTaskbar && (Native.GetWindowLongPtr(new WindowInteropHelper(window).Handle, Native.ExStyle).ToInt64() & 0x40000) == 0);
            if (reload)
            {
                Check("App restart reloads saved settings", app.Settings.Language == "ja" && app.Settings.ShowSeconds && app.Settings.Opacity == .55 && app.Settings.Position == "Free" && app.Settings.X == 345 && app.Settings.Y == 234);
                Check("App restart retains safe recovery state", !app.Settings.ClickThrough && app.Overlay.IsVisible);
                tray.ContextMenuStrip!.Items[4].PerformClick(); return;
            }
            foreach (var language in new[] { "zh-TW", "en", "ja" })
            {
                Strings.Use(language); app.RefreshTray();
                Field<Forms.NotifyIcon>(app, "tray").ContextMenuStrip!.Items[3].PerformClick();
                var about = Field<AboutWindow>(app, "aboutWindow");
                Check("Localized about opens from tray: " + language, about.IsVisible && about.Title.EndsWith(Strings.Get("About")) && !about.ShowInTaskbar);
                app.OpenAbout();
                Check("About reuses existing window: " + language, ReferenceEquals(about, Field<AboutWindow>(app, "aboutWindow")));
                about.UpdateLayout();
                var tabs = (TabControl)((DockPanel)about.Content).Children[2];
                for (int tab = 0; tab < 2; tab++)
                {
                    tabs.SelectedIndex = tab; about.UpdateLayout();
                    var image = new RenderTargetBitmap((int)about.ActualWidth, (int)about.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    image.Render(about);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
                    using var output = File.Create(Path.Combine(directory, $"about-{language}-{tab}.png")); png.Save(output);
                }
                if (language == "en")
                {
                    about.WindowState = WindowState.Minimized;
                    Check("About minimizes to tray", !about.IsVisible);
                    app.OpenAbout();
                    Check("About restores from tray", about.IsVisible && about.WindowState == WindowState.Normal);
                    Strings.Use("ja"); app.Changed();
                    Check("Open about follows language changes", about.Title.EndsWith(Strings.Get("About")));
                }
                about.Close();
            }
            Strings.Use(app.Settings.Language); app.RefreshTray();
            Check("Embedded MIT and runtime notices available offline", AboutWindow.Resource("LICENSE").Contains("Kaido-MSVC") && AboutWindow.Resource("LICENSE").Contains("THE SOFTWARE IS PROVIDED") && AboutWindow.Resource("licenses.dotnet-runtime-THIRD-PARTY-NOTICES.txt").Length > 10000);
            Check("About version comes from app assembly", AboutWindow.Version == typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
            window.WindowState = WindowState.Minimized;
            Check("Minimize settings hides to tray and keeps clock visible", !window.IsVisible && app.Overlay.IsVisible && tray.Visible);
            app.Settings.Position = "Free"; app.Settings.X = 345; app.Settings.Y = 234; app.Overlay.Place(true);
            app.Settings.Language = "ja"; Strings.Use("ja"); app.Settings.ShowSeconds = true; app.Settings.Opacity = .55;
            app.Settings.ClickThrough = true; app.Changed();
            Check("Click-through available before recovery", app.Overlay.HotkeyAvailable && app.Settings.ClickThrough);
            timer.Tick += (_, _) => Guard(() =>
            {
                timer.Stop();
                var saved = new SettingsStore(path).Load();
                Check("Debounced settings save while minimized", File.Exists(path) && saved.ClickThrough && saved.ShowSeconds && saved.Opacity == .55 && saved.X == 345 && saved.Y == 234);
                app.Overlay.WindowState = WindowState.Minimized;
                app.Overlay.CapturePosition();
                Check("Minimized clock hidden with coordinates intact", !app.Overlay.IsVisible && app.Settings.X == 345 && app.Settings.Y == 234);
                Field<Forms.NotifyIcon>(app, "tray").ContextMenuStrip!.Items[0].PerformClick();
                window = Field<SettingsWindow>(app, "settingsWindow");
                Check("Tray recovery restores settings and clock", window.IsVisible && window.WindowState == WindowState.Normal && !window.ShowInTaskbar && app.Overlay.IsVisible && app.Overlay.WindowState == WindowState.Normal);
                Check("Tray recovery removes click-through", !app.Settings.ClickThrough && (Native.GetWindowLongPtr(app.Overlay.Handle, Native.ExStyle).ToInt64() & Native.Transparent) == 0);
                window.Close();
                Check("Close settings keeps tray and clock running", Field<Forms.NotifyIcon>(app, "tray").Visible && app.Overlay.IsVisible);
                Field<Forms.NotifyIcon>(app, "tray").ContextMenuStrip!.Items[0].PerformClick();
                Check("Tray recreates closed settings window", Field<SettingsWindow>(app, "settingsWindow").IsVisible);
                Field<Forms.NotifyIcon>(app, "tray").ContextMenuStrip!.Items[4].PerformClick();
            });
            timer.Start();
        })));
        app.Run();
        if (failure != null) { Console.Error.WriteLine(failure); return 1; }
        var final = new SettingsStore(path).Load();
        Check("Tray exit saves final settings", final.Language == "ja" && final.ShowSeconds && final.Opacity == .55 && !final.ClickThrough && final.X == 345 && final.Y == 234);
        Check("Tray disposed on exit", !Field<Forms.NotifyIcon>(app, "tray").Visible);
        Console.WriteLine($"All {count} lifecycle checks passed."); return 0;
    }
}
