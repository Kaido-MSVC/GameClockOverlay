using Microsoft.Win32;

namespace GameClockOverlay.AvaloniaApp.Services.Rtss;

public sealed record RtssInstallInfo(string? Directory,string? Version,bool ExecutableFound);

// 只讀取 Windows「解除安裝」登錄資訊來顯示安裝狀態；不寫死路徑、不啟動或修改 RTSS。
public static class RtssInstall
{
    private const string Uninstall=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    public static RtssInstallInfo Find()
    {
        foreach(var (hive,view) in new[]{(RegistryHive.LocalMachine,RegistryView.Registry32),(RegistryHive.LocalMachine,RegistryView.Registry64),(RegistryHive.CurrentUser,RegistryView.Default)})
        {
            try
            {
                using var root=RegistryKey.OpenBaseKey(hive,view);
                using var list=root.OpenSubKey(Uninstall);
                if(list is null)continue;
                foreach(string name in list.GetSubKeyNames())
                {
                    using var key=list.OpenSubKey(name);
                    if(key?.GetValue("DisplayName") is not string display || !display.Contains("RivaTuner Statistics Server",StringComparison.OrdinalIgnoreCase))continue;
                    string? dir=key.GetValue("InstallLocation") as string;
                    if(string.IsNullOrWhiteSpace(dir) && key.GetValue("UninstallString") is string uninstall)
                        dir=Path.GetDirectoryName(uninstall.Trim().Trim('"'));
                    dir=string.IsNullOrWhiteSpace(dir)?null:dir.Trim().Trim('"');
                    bool exe=dir is not null && File.Exists(Path.Combine(dir,"RTSS.exe"));
                    return new(dir,key.GetValue("DisplayVersion") as string,exe);
                }
            }
            catch(Exception ex) when(ex is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException){}
        }
        return new(null,null,false);
    }
}
