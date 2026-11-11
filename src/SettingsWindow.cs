using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameClockOverlay;

public sealed class SettingsWindow : Window
{
    private readonly App app;
    private TextBlock status = null!;
    private ComboBox positionBox = null!, monitorBox = null!;
    private CheckBox clickThroughBox = null!;
    private ScrollViewer scroll = null!;
    private readonly string[] places = ["Free", "TopLeft", "TopRight", "BottomLeft", "BottomRight"];
    private readonly System.Collections.Generic.List<string> devices = new();
    private bool syncing;
    private string? displayLanguage;
    public SettingsWindow(App app)
    {
        this.app = app;
        Width = 560; Height = 700; MinWidth = 480; MinHeight = 480;
        ShowInTaskbar = false;
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(248, 249, 251));
        FontFamily = new FontFamily("Segoe UI, Microsoft JhengHei UI, Yu Gothic UI"); FontSize = 14;
        Build();
    }
    private static TextBlock Text(string text, double size = 14) => new()
    { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), Foreground = new SolidColorBrush(Color.FromRgb(32, 42, 55)) };
    private static void Heading(Panel parent, string key)
    {
        var heading = Text(Strings.Get(key), 17); heading.FontWeight = FontWeights.SemiBold;
        heading.Margin = new Thickness(0, 22, 0, 12); parent.Children.Add(heading);
    }
    private void Build()
    {
        displayLanguage = Strings.Language;
        Title = "GameClock Overlay — " + Strings.Get("Settings");
        var root = new DockPanel { Margin = new Thickness(28, 20, 28, 20) };
        var footer = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        status = Text(""); status.FontSize = 12; footer.Children.Add(status); UpdateStatus();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var about = new Button { Content = Strings.Get("About"), Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 10, 0) };
        about.Click += (_, _) => app.OpenAbout(); buttons.Children.Add(about);
        var exit = new Button { Content = Strings.Get("Exit"), Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 10, 0) };
        exit.Click += (_, _) => app.Quit(); buttons.Children.Add(exit);
        var close = new Button { Content = Strings.Get("Close"), Padding = new Thickness(14, 7, 14, 7), IsCancel = true };
        close.Click += (_, _) => Close(); buttons.Children.Add(close); footer.Children.Add(buttons);
        var body = new StackPanel();
        scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body };
        root.Children.Add(scroll);
        var title = Text("GameClock Overlay", 26); title.FontWeight = FontWeights.SemiBold; body.Children.Add(title);
        body.Children.Add(Text("v" + AboutWindow.Version + " · " + Strings.Get("Subtitle")));
        body.Children.Add(Text(Strings.Get("Hint"), 13));
        Heading(body, "Appearance");
        var seconds = new CheckBox { Content = Strings.Get("Seconds"), IsChecked = app.Settings.ShowSeconds, Margin = new Thickness(0, 0, 0, 14) };
        seconds.Click += (_, _) => { app.Settings.ShowSeconds = seconds.IsChecked == true; app.Changed(); }; body.Children.Add(seconds);
        var fontLabel = Text(Strings.Get("FontSize") + $"  {app.Settings.FontSize:0} DIP"); body.Children.Add(fontLabel);
        var font = new Slider { Minimum = 16, Maximum = 96, Value = app.Settings.FontSize, TickFrequency = 2,
            SmallChange = 2, LargeChange = 8, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 4, 14) };
        System.Windows.Automation.AutomationProperties.SetName(font, Strings.Get("FontSize"));
        font.ValueChanged += (_, _) => { app.Settings.FontSize = font.Value; fontLabel.Text = Strings.Get("FontSize") + $"  {font.Value:0} DIP"; app.Changed(); };
        body.Children.Add(font);
        var opacityLabel = Text(Strings.Get("Opacity") + $"  {app.Settings.Opacity:P0}"); body.Children.Add(opacityLabel);
        var opacity = new Slider { Minimum = 25, Maximum = 100, Value = app.Settings.Opacity * 100, TickFrequency = 5,
            SmallChange = 5, LargeChange = 10, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 4, 14) };
        System.Windows.Automation.AutomationProperties.SetName(opacity, Strings.Get("Opacity"));
        opacity.ValueChanged += (_, _) => { app.Settings.Opacity = opacity.Value / 100; opacityLabel.Text = Strings.Get("Opacity") + $"  {app.Settings.Opacity:P0}"; app.Changed(); }; body.Children.Add(opacity);
        body.Children.Add(Text(Strings.Get("Position")));
        var position = new ComboBox { Margin = new Thickness(0, 0, 0, 0), Padding = new Thickness(6), HorizontalContentAlignment = HorizontalAlignment.Left };
        positionBox = position;
        foreach (var key in places) position.Items.Add(Strings.Get(key));
        position.SelectedIndex = Array.IndexOf(places, app.Settings.Position);
        System.Windows.Automation.AutomationProperties.SetName(position, Strings.Get("Position"));
        position.SelectionChanged += (_, _) => { if (!syncing && position.SelectedIndex >= 0) { app.Settings.Position = places[position.SelectedIndex]; app.Changed(); } };
        body.Children.Add(position);
        var monitorLabel = Text(Strings.Get("Monitor")); monitorLabel.Margin = new Thickness(0, 14, 0, 10); body.Children.Add(monitorLabel);
        var monitor = new ComboBox { Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 14) };
        monitorBox = monitor;
        PopulateMonitors();
        System.Windows.Automation.AutomationProperties.SetName(monitor, Strings.Get("Monitor"));
        monitor.SelectionChanged += (_, _) =>
        {
            if (syncing || monitor.SelectedIndex < 0) return;
            app.Settings.MonitorDevice = devices[monitor.SelectedIndex]; app.Changed();
        };
        body.Children.Add(monitor);
        var bounds = new CheckBox { Content = Strings.Get("MonitorBounds"), IsChecked = app.Settings.UseMonitorBounds };
        bounds.Click += (_, _) => { app.Settings.UseMonitorBounds = bounds.IsChecked == true; app.Changed(); }; body.Children.Add(bounds);
        Heading(body, "Interaction");
        var pass = new CheckBox { Content = Strings.Get("ClickThrough"), IsChecked = app.Settings.ClickThrough, IsEnabled = app.Overlay.HotkeyAvailable, Margin = new Thickness(0, 0, 0, 10) };
        clickThroughBox = pass;
        pass.Click += (_, _) => { app.Settings.ClickThrough = pass.IsChecked == true; app.Changed(); }; body.Children.Add(pass);
        body.Children.Add(Text(Strings.Get(app.Overlay.HotkeyAvailable ? "Recovery" : "HotkeyFailed"), 13));
        Heading(body, "Language");
        var language = new ComboBox { Padding = new Thickness(6) };
        string[] languages = ["auto", "zh-TW", "en", "ja"];
        foreach (var name in new[] { Strings.Get("Auto"), "繁體中文", "English", "日本語" }) language.Items.Add(name);
        language.SelectedIndex = Array.IndexOf(languages, app.Settings.Language);
        System.Windows.Automation.AutomationProperties.SetName(language, Strings.Get("Language"));
        language.SelectionChanged += (_, _) =>
        {
            if (language.SelectedIndex < 0) return;
            app.Settings.Language = languages[language.SelectedIndex]; Strings.Use(app.Settings.Language);
            app.Changed(); Refresh();
        };
        body.Children.Add(language);
        var limit = Text(Strings.Get("Limit"), 12); limit.Margin = new Thickness(0, 22, 0, 0); body.Children.Add(limit);
        Content = root;
    }
    private void PopulateMonitors()
    {
        devices.Clear(); monitorBox.Items.Clear(); devices.Add("");
        monitorBox.Items.Add(Strings.Get("CurrentMonitor"));
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            devices.Add(screen.DeviceName);
            monitorBox.Items.Add($"{screen.DeviceName} ({screen.Bounds.Width} × {screen.Bounds.Height})");
        }
        if (!devices.Contains(app.Settings.MonitorDevice))
        {
            devices.Add(app.Settings.MonitorDevice);
            monitorBox.Items.Add(Strings.Get("DisconnectedMonitor") + " " + app.Settings.MonitorDevice);
        }
        monitorBox.SelectedIndex = devices.IndexOf(app.Settings.MonitorDevice);
    }
    public void SyncExternal()
    {
        if (syncing) return;
        syncing = true;
        try
        {
            positionBox.SelectedIndex = Array.IndexOf(places, app.Settings.Position);
            if (!devices.Contains(app.Settings.MonitorDevice)) PopulateMonitors();
            else monitorBox.SelectedIndex = devices.IndexOf(app.Settings.MonitorDevice);
            clickThroughBox.IsChecked = app.Settings.ClickThrough;
            clickThroughBox.IsEnabled = app.Overlay.HotkeyAvailable;
        }
        finally { syncing = false; }
    }
    public void Refresh()
    {
        if (displayLanguage == Strings.Language) { SyncExternal(); return; }
        double offset = scroll.VerticalOffset;
        Build(); UpdateLayout(); scroll.ScrollToVerticalOffset(offset);
    }
    public void UpdateStatus()
    {
        status.Text = Strings.Get(app.Store.Error == null ? "Saved" : "SaveFailed");
        status.Foreground = app.Store.Error == null ? new SolidColorBrush(Color.FromRgb(58, 78, 65)) : Brushes.Firebrick;
        status.ToolTip = app.Store.Error;
    }
}
