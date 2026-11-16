using Avalonia;
using GameClockOverlay.AvaloniaApp.Services;

namespace GameClockOverlay.AvaloniaApp;

internal static class Program
{
    internal static string[] Arguments=[];
    internal static SingleInstance? Instance;
    [STAThread]
    public static int Main(string[] args)
    {
        Arguments=args;
        bool test=args.Contains("--ui-test");
        using var instance=new SingleInstance(test?".UiTest":"");Instance=instance;
        switch(Decide(instance.IsFirst,test,App.TestSettingsPath is not null))
        {
            case Startup.Activate:instance.Activate();return 0;
            case Startup.Refuse:return RefusedExit;
        }
        try{return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);}
        catch(Exception ex)
        {
            try {Directory.CreateDirectory(Path.GetDirectoryName(SettingsStore.DefaultPath)!);File.AppendAllText(Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath)!,"errors.log"),DateTimeOffset.Now+" "+ex+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}
            return 1;
        }
    }
    // GCO-03：--ui-test 會改用測試專用的單一執行個體鎖。只有測試 runner 在同一程序內先設定好隔離的設定檔
    //（App.TestSettingsPath）後才可以完整啟動；從命令列直接帶 --ui-test 啟動時，只能喚醒已存在的測試實體，
    // 否則立即結束，不會以測試鎖另開一個共用真實設定與正式 RTSS Owner 的產品實體。
    internal enum Startup { Run, Activate, Refuse }
    internal const int RefusedExit=2;
    internal static Startup Decide(bool isFirst,bool testMode,bool isolated)=>!isFirst?Startup.Activate:testMode && !isolated?Startup.Refuse:Startup.Run;
    public static AppBuilder BuildAvaloniaApp()=>AppBuilder.Configure<App>().UsePlatformDetect()
        .With(new Win32PlatformOptions{RenderingMode=[Win32RenderingMode.Software],CompositionMode=[Win32CompositionMode.RedirectionSurface]})
        .LogToTrace();
}
