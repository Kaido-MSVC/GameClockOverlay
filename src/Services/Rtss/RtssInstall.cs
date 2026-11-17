using System.Diagnostics;
using Microsoft.Win32;

namespace GameClockOverlay.AvaloniaApp.Services.Rtss;

public enum RtssInstallSource { None, Settings, Registry, CommonPath }

// Directory／Version／ExecutableFound 沿用 Avalonia-2 的欄位；ExecutablePath 只有在檔案確實存在時才有值。
// ConfiguredPathInvalid：設定檔中有手動指定的路徑，但已失效（檔案不存在或不是 RTSS.exe），本次改用自動偵測結果。
public sealed record RtssInstallInfo(string? Directory,string? Version,bool ExecutableFound,
    string? ExecutablePath=null,RtssInstallSource Source=RtssInstallSource.None,bool ConfiguredPathInvalid=false);

public sealed record RtssCandidate(string Path,string? Version,RtssInstallSource Source);

// 唯讀偵測 RTSS 安裝位置：設定值 → 登錄（32／64 位元檢視）→ 常見安裝位置。不寫入登錄、不修改 RTSS 設定。
public static class RtssInstall
{
    public const string ExeName="RTSS.exe";
    private const string Uninstall=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string Unwinder=@"SOFTWARE\Unwinder\RTSS";
    private const string FolderName="RivaTuner Statistics Server";

    public static RtssInstallInfo Find(string? configured=null)=>Find(configured,RegistryCandidates,CommonCandidates,File.Exists,FileVersion);

    public static RtssInstallInfo Find(string? configured,Func<IEnumerable<RtssCandidate>> registry,Func<IEnumerable<RtssCandidate>> common,Func<string,bool> exists,Func<string,string?>? version=null)
    {
        version??=_=>null;
        bool configuredInvalid=false;
        if(!string.IsNullOrWhiteSpace(configured))
        {
            if(Validate(configured,exists) is string path)return Found(path,version(path),RtssInstallSource.Settings,false);
            configuredInvalid=true;
        }
        // 登錄中有安裝紀錄但執行檔不存在時，記下目錄；若常見位置也找不到，回報「有紀錄但缺少 RTSS.exe」。
        string? staleDirectory=null;string? staleVersion=null;
        foreach(var source in new[]{registry,common})
        {
            IEnumerable<RtssCandidate> list;
            try{list=source().ToList();}
            catch(Exception ex) when(IsProbeError(ex)){continue;}
            foreach(var c in list)
            {
                if(Validate(c.Path,exists) is string path)return Found(path,c.Version??version(path),c.Source,configuredInvalid);
                if(c.Source==RtssInstallSource.Registry && staleDirectory is null && Normalize(c.Path) is string bad){staleDirectory=Path.GetDirectoryName(bad);staleVersion=c.Version;}
            }
        }
        return new(staleDirectory,staleVersion,false,null,staleDirectory is null?RtssInstallSource.None:RtssInstallSource.Registry,configuredInvalid);
    }

    private static RtssInstallInfo Found(string path,string? version,RtssInstallSource source,bool configuredInvalid)=>
        new(Path.GetDirectoryName(path),version,true,path,source,configuredInvalid);

    // 只接受絕對路徑、檔名必須是 RTSS.exe 且檔案確實存在；避免把任意執行檔當成 RTSS 自動啟動。
    public static string? Validate(string? raw,Func<string,bool>? exists=null)
    {
        exists??=File.Exists;
        if(Normalize(raw) is not string path)return null;
        if(!string.Equals(Path.GetFileName(path),ExeName,StringComparison.OrdinalIgnoreCase))return null;
        try{return exists(path)?path:null;}
        catch(Exception ex) when(IsProbeError(ex)){return null;}
    }

    internal static string? Normalize(string? raw)
    {
        if(string.IsNullOrWhiteSpace(raw))return null;
        string value=Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"').Trim());
        if(value.Length is 0 or >1024 || !Path.IsPathFullyQualified(value))return null;
        try{return Path.GetFullPath(value);}
        catch(Exception ex) when(ex is ArgumentException or NotSupportedException or PathTooLongException){return null;}
    }

    // RTSS 安裝程式寫入 HKLM\SOFTWARE\Unwinder\RTSS（InstallPath／InstallDir，32 位元程式位於 WOW6432Node）
    // 以及「解除安裝」清單。兩種登錄檢視與目前使用者都檢查；任一項讀取失敗只略過該項。
    public static IEnumerable<RtssCandidate> RegistryCandidates()
    {
        var found=new List<RtssCandidate>();
        foreach(var (hive,view) in new[]{(RegistryHive.LocalMachine,RegistryView.Registry32),(RegistryHive.LocalMachine,RegistryView.Registry64),(RegistryHive.CurrentUser,RegistryView.Registry32),(RegistryHive.CurrentUser,RegistryView.Registry64)})
        {
            try
            {
                using var root=RegistryKey.OpenBaseKey(hive,view);
                using(var key=root.OpenSubKey(Unwinder))
                {
                    if(key is not null)
                    {
                        string? ver=key.GetValue("ProductVersion") as string;
                        if(key.GetValue("InstallPath") is string exe && exe.Length>0)found.Add(new(exe,ver,RtssInstallSource.Registry));
                        if(key.GetValue("InstallDir") is string dir && dir.Length>0)found.Add(new(Path.Combine(dir.Trim().Trim('"'),ExeName),ver,RtssInstallSource.Registry));
                    }
                }
                using var list=root.OpenSubKey(Uninstall);
                if(list is null)continue;
                foreach(string name in list.GetSubKeyNames())
                {
                    using var key=list.OpenSubKey(name);
                    if(key?.GetValue("DisplayName") is not string display || !display.Contains(FolderName,StringComparison.OrdinalIgnoreCase))continue;
                    string? ver=key.GetValue("DisplayVersion") as string;
                    string? dir=key.GetValue("InstallLocation") as string;
                    if(string.IsNullOrWhiteSpace(dir) && key.GetValue("UninstallString") is string uninstall)
                        dir=DirectoryOf(uninstall);
                    if(!string.IsNullOrWhiteSpace(dir))found.Add(new(Path.Combine(dir.Trim().Trim('"'),ExeName),ver,RtssInstallSource.Registry));
                }
            }
            catch(Exception ex) when(IsProbeError(ex)){}
        }
        return found;
    }

    // 解除安裝字串可能帶引號或參數："E:\…\uninstall.exe" /S。
    internal static string? DirectoryOf(string command)
    {
        string s=command.Trim();
        if(s.StartsWith('"')){int end=s.IndexOf('"',1);s=end>1?s[1..end]:s.Trim('"');}
        else{int exe=s.IndexOf(".exe",StringComparison.OrdinalIgnoreCase);if(exe>0)s=s[..(exe+4)];}
        try{return Path.GetDirectoryName(s);}catch(ArgumentException){return null;}
    }

    // 常見安裝位置：Program Files (x86)／Program Files，以及其他固定磁碟上的同名資料夾（安裝在非系統磁碟時）。
    public static IEnumerable<RtssCandidate> CommonCandidates()
    {
        var dirs=new List<string>();
        foreach(var folder in new[]{Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.ProgramFiles})
        {
            string root=Environment.GetFolderPath(folder);
            if(root.Length>0)dirs.Add(Path.Combine(root,FolderName));
        }
        try
        {
            foreach(var drive in DriveInfo.GetDrives())
            {
                if(drive.DriveType!=DriveType.Fixed)continue;
                dirs.Add(Path.Combine(drive.RootDirectory.FullName,"Program Files (x86)",FolderName));
                dirs.Add(Path.Combine(drive.RootDirectory.FullName,"Program Files",FolderName));
            }
        }
        catch(Exception ex) when(IsProbeError(ex)){}
        return dirs.Distinct(StringComparer.OrdinalIgnoreCase).Select(d=>new RtssCandidate(Path.Combine(d,ExeName),null,RtssInstallSource.CommonPath)).ToList();
    }

    private static string? FileVersion(string path)
    {
        try{return FileVersionInfo.GetVersionInfo(path).ProductVersion;}
        catch(Exception ex) when(IsProbeError(ex)){return null;}
    }

    private static bool IsProbeError(Exception ex)=>ex is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException or FileNotFoundException;
}
