using System.Diagnostics;
using System.Reflection;
using GameClockOverlay.AvaloniaApp.Services;
using GameClockOverlay.AvaloniaApp.Views;

namespace GameClockOverlay.Avalonia.Tests;

// v0.3.1：版本欄位一致（assembly、檔案版本、資訊版本、關於頁）。實際 EXE 與 ZIP 由 scripts/package-rc.ps1 另外核對。
internal static partial class Program
{
    private static void ReleaseCore()
    {
        var asm=typeof(SettingsWindow).Assembly;
        Check(asm.GetName().Name=="GameClockOverlay","release-assembly-name");
        Check(asm.GetName().Version==new Version(0,3,1,0),"release-assembly-version-0.3.1.0");
        Check(asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version=="0.3.1.0","release-file-version-0.3.1.0");
        Check(SettingsWindow.AppVersion=="0.3.1","release-informational-version-0.3.1");
        var exe=FileVersionInfo.GetVersionInfo(Path.Combine(AppContext.BaseDirectory,"GameClockOverlay.exe"));
        Check(exe.ProductVersion=="0.3.1" && exe.FileVersion=="0.3.1.0" && exe.ProductName=="GameClock Overlay","release-exe-version-resource");
        foreach(int lang in new[]{0,1,2})
            Check(string.Format(Strings.Values["AboutNew"][lang],SettingsWindow.AppVersion).StartsWith("v0.3.1 ",StringComparison.Ordinal),"release-about-version-"+lang);
    }
}
