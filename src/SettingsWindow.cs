using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameClockOverlay;

public sealed class SettingsWindow : Window
{
    private readonly App app;
    private TextBlock status = null!;
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
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body });
        var title = Text("GameClock Overlay", 26); title.FontWeight = FontWeights.SemiBold; body.Children.Add(title);
        body.Children.Add(Text("v" + AboutWindow.Version + " · " + Strings.Get("Subtitle")));
        body.Children.Add(Text(Strings.Get("Hint"), 13));
        Heading(body, "Appearance");
        var seconds = new CheckBox { Content = Strings.Get("Seconds"), IsChecked = app.Settings.ShowSeconds, Margin = new Thickness(0, 0, 0, 14) };
        seconds.Click += (_, _) => { app.Settings.ShowSeconds = seconds.IsChecked == true; app.Changed(); }; body.Children.Add(seconds);
        var opacityLabel = Text(Strings.Get("Opacity") + $"  {app.Settings.Opacity:P0}"); body.Children.Add(opacityLabel);
        var opacity = new Slider { Minimum = 25, Maximum = 100, Value = app.Settings.Opacity * 100, TickFrequency = 5,
            SmallChange = 5, LargeChange = 10, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 4, 14) };
        System.Windows.Automation.AutomationProperties.SetName(opacity, Strings.Get("Opacity"));
        opacity.ValueChanged += (_, _) => { app.Settings.Opacity = opacity.Value / 100; opacityLabel.Text = Strings.Get("Opacity") + $"  {app.Settings.Opacity:P0}"; app.Changed(); }; body.Children.Add(opacity);
        body.Children.Add(Text(Strings.Get("Position")));
        var position = new ComboBox { Margin = new Thickness(0, 0, 0, 0), Padding = new Thickness(6), HorizontalContentAlignment = HorizontalAlignment.Left };
        string[] places = ["Free", "TopLeft", "TopRight", "BottomLeft", "BottomRight"];
        foreach (var key in places) position.Items.Add(Strings.Get(key));
        position.SelectedIndex = Array.IndexOf(places, app.Settings.Position);
        System.Windows.Automation.AutomationProperties.SetName(position, Strings.Get("Position"));
        position.SelectionChanged += (_, _) => { if (position.SelectedIndex >= 0) { app.Settings.Position = places[position.SelectedIndex]; app.Changed(); } };
        body.Children.Add(position);
        Heading(body, "Interaction");
        var pass = new CheckBox { Content = Strings.Get("ClickThrough"), IsChecked = app.Settings.ClickThrough, IsEnabled = app.Overlay.HotkeyAvailable, Margin = new Thickness(0, 0, 0, 10) };
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
            app.Changed(); app.RefreshTray(); Build();
        };
        body.Children.Add(language);
        var limit = Text(Strings.Get("Limit"), 12); limit.Margin = new Thickness(0, 22, 0, 0); body.Children.Add(limit);
        Content = root;
    }
    public void Refresh() => Build();
    public void UpdateStatus()
    {
        status.Text = Strings.Get(app.Store.Error == null ? "Saved" : "SaveFailed");
        status.Foreground = app.Store.Error == null ? new SolidColorBrush(Color.FromRgb(58, 78, 65)) : Brushes.Firebrick;
        status.ToolTip = app.Store.Error;
    }
}
