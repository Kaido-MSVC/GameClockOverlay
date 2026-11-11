using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GameClockOverlay;

internal static class UiSession
{
    // A real hit-testing target for manual / desktop automation. Never uses the user's settings.
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var app = new App(Path.GetFullPath(Path.Combine(directory, "ui-settings.json"))) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromMinutes(8) };
        timeout.Tick += (_, _) => { timeout.Stop(); app.Quit(); };
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var settingsWindow = (Window)typeof(App).GetField("settingsWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
            settingsWindow.Left = 30; settingsWindow.Top = 40;
            var host = new Window { Title = "GameClock physical click target", Left = 900, Top = 80, Width = 850, Height = 650 };
            int clicks = 0;
            var button = new Button { Content = "Physical clicks: 0", FontSize = 30 };
            button.Click += (_, _) =>
            {
                button.Content = $"Physical clicks: {++clicks}";
                File.WriteAllText(Path.Combine(directory, "physical-clicks.txt"), clicks.ToString());
            };
            host.Content = button; host.Show();
            app.Settings.Position = "Free"; app.Settings.MonitorDevice = "";
            app.Settings.ShowSeconds = true;
            app.Settings.ClickThrough = true; app.Changed();
            app.Settings.X = 1200; app.Settings.Y = 200; app.Overlay.Place(true);
            ((SettingsWindow)settingsWindow).Refresh();
            timeout.Start();
        }));
        app.Run(); timeout.Stop(); return 0;
    }
}
