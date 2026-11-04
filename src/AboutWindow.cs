using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameClockOverlay;

public sealed class AboutWindow : Window
{
    private string? language;
    public static string Version => typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    internal static string Resource(string name)
    {
        using var stream = typeof(App).Assembly.GetManifestResourceStream("GameClockOverlay." + name)
            ?? throw new InvalidOperationException("Missing license resource: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public AboutWindow()
    {
        Width = 680; Height = 640; MinWidth = 460; MinHeight = 420;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(248, 249, 251));
        Foreground = new SolidColorBrush(Color.FromRgb(32, 42, 55));
        FontFamily = new FontFamily("Segoe UI, Microsoft JhengHei UI, Yu Gothic UI"); FontSize = 14;
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };
        Refresh();
    }
    private static TextBlock Text(string text, double size = 14) => new()
    { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
    private static TabItem LicenseTab(string title, string content)
    {
        var text = new TextBox
        {
            Text = content, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(14), FontSize = 13
        };
        System.Windows.Automation.AutomationProperties.SetName(text, title);
        return new TabItem { Header = title, Content = text };
    }
    public void Refresh()
    {
        if (language == Strings.Language) return;
        language = Strings.Language;
        Title = "GameClock Overlay — " + Strings.Get("About");
        var root = new DockPanel { Margin = new Thickness(28, 20, 28, 20) };
        var close = new Button { Content = Strings.Get("AboutClose"), Padding = new Thickness(14, 7, 14, 7),
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0), IsCancel = true };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(close);
        var header = new StackPanel();
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        header.Children.Add(new TextBlock { Text = "GameClock Overlay", FontSize = 26, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        header.Children.Add(Text(Strings.Get("Version") + " " + Version + "  ·  " + Strings.Get("Author") + " Kaido-MSVC"));
        header.Children.Add(Text("Copyright (c) 2026 Kaido-MSVC; GameClock Overlay contributors", 12));
        header.Children.Add(Text(Strings.Get("LicenseSummary"), 13));
        var tabs = new TabControl();
        tabs.Items.Add(LicenseTab(Strings.Get("MitLicense"), Resource("LICENSE")));
        tabs.Items.Add(LicenseTab(Strings.Get("ThirdParty"), Strings.Get("DependencySummary") + "\n\n" +
            Resource("THIRD-PARTY-NOTICES") + "\n\n.NET runtime / apphost — LICENSE\n\n" +
            Resource("licenses.dotnet-runtime-LICENSE.txt") + "\n\n.NET — THIRD-PARTY-NOTICES\n\n" +
            Resource("licenses.dotnet-runtime-THIRD-PARTY-NOTICES.txt")));
        root.Children.Add(tabs); Content = root;
    }
}
