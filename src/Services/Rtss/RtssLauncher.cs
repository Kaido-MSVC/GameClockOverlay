using System.ComponentModel;
using System.Diagnostics;

namespace GameClockOverlay.AvaloniaApp.Services.Rtss;

// RTSS 程序狀態。與共享記憶體連線（RtssClockService）分開：程序存在不代表 OSD 已連線。
public enum RtssRunState { Checking, NotInstalled, NotRunning, Starting, Running, StartFailed }

// StartedByUs：本次執行期間由本程式啟動、且仍持有該程序控制代碼（PID 不會被重複使用）。
public sealed record RtssLaunchStatus(RtssRunState State,RtssInstallInfo? Install,string? Error=null,bool StartedByUs=false,DateTimeOffset? At=null);

public interface IRtssProcess : IDisposable { bool HasExited { get; } }

// 可替換的系統操作，讓測試不必真的啟動 RTSS。
public interface IRtssSystem
{
    RtssInstallInfo Find(string? configured);
    bool IsRunning();
    IRtssProcess? Start(string executable);
}

public sealed class RtssSystem : IRtssSystem
{
    public RtssInstallInfo Find(string? configured)=>RtssInstall.Find(configured);
    public bool IsRunning()=>RtssClockService.IsRtssProcessRunning();
    // RTSS.exe 的資訊清單要求系統管理員權限（requireAdministrator）。以 ShellExecute 直接開啟已驗證的完整路徑，
    // 不經 cmd、不帶參數；Windows 會視需要顯示 UAC 提示，本程式自身維持一般權限。
    // 啟動前以 FileShare.Read 持有檔案，直到 Windows 已開啟執行檔：驗證過簽章的檔案在這段期間不能被改寫、改名或刪除。
    // SEC-02：持有檔案只會鎖住真實路徑上的上層資料夾，路徑中的 junction／符號連結仍可被刪除後重建、指向別處。
    // 因此改以控制代碼解析出的最終路徑（不含任何重新剖析點）驗證並啟動，不再使用原本的路徑字串。
    public IRtssProcess? Start(string executable)
    {
        using var hold=new FileStream(executable,FileMode.Open,FileAccess.Read,FileShare.Read);
        string target=FinalPath(hold.SafeFileHandle)??throw new RtssTrustException("unsupported-path");
        if(RtssTrust.Check(target,hold.SafeFileHandle) is string code)throw new RtssTrustException(code);
        var process=Process.Start(new ProcessStartInfo(target){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(target)!});
        return process is null?null:new Wrapped(process);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll",EntryPoint="GetFinalPathNameByHandleW",ExactSpelling=true,SetLastError=true)]
    private static extern unsafe uint GetFinalPathNameByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file,char* buffer,uint length,uint flags);

    // 回傳本機磁碟機代號形式的最終路徑；網路路徑或無法解析時回傳 null（不啟動）。
    internal static unsafe string? FinalPath(Microsoft.Win32.SafeHandles.SafeFileHandle file)
    {
        const int Max=32768;
        char* buffer=stackalloc char[Max];
        uint length=GetFinalPathNameByHandle(file,buffer,Max,0/*FILE_NAME_NORMALIZED|VOLUME_NAME_DOS*/);
        if(length is 0 or >=Max)return null;
        string path=new(buffer,0,(int)length);
        if(path.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))return null;
        if(path.StartsWith(@"\\?\",StringComparison.Ordinal))path=path[4..];
        return path.Length>=3 && char.IsAsciiLetter(path[0]) && path[1]==':' && path[2]=='\\'?path:null;
    }
    private sealed class Wrapped(Process process) : IRtssProcess
    {
        public bool HasExited{get{try{return process.HasExited;}catch(Exception ex) when(ex is InvalidOperationException or Win32Exception){return false;}}}
        public void Dispose()=>process.Dispose();
    }
}

// GUI 測試的預設值：偵測照常（唯讀），但絕不啟動正式 RTSS。
public sealed class BlockedRtssSystem : IRtssSystem
{
    private readonly RtssSystem real=new();
    public RtssInstallInfo Find(string? configured)=>real.Find(configured);
    public bool IsRunning()=>real.IsRunning();
    public IRtssProcess? Start(string executable)=>throw new InvalidOperationException("RTSS start is blocked in test mode.");
}

// RTSS 安裝偵測與連動啟動的唯一入口。所有偵測與啟動都在背景執行緒，呼叫端（UI）只接收狀態快照。
// 以 SemaphoreSlim 序列化：重複點選或啟動流程與手動啟動同時發生時，不會重複啟動。
// 不關閉 RTSS：RTSS 以系統管理員權限執行，一般權限的本程式無法送出正常結束要求（UIPI），也不使用強制結束。
public sealed class RtssLauncher : IDisposable
{
    private readonly IRtssSystem system;
    private readonly TimeSpan startWait,poll;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly Lock state=new();
    private RtssLaunchStatus status=new(RtssRunState.Checking,null);
    private IRtssProcess? owned;
    private bool disposed;
    public event Action<RtssLaunchStatus>? StatusChanged;
    public long StartAttempts=>Interlocked.Read(ref attempts);
    private long attempts;
    public RtssLaunchStatus Status{get{lock(state)return status;}}

    public RtssLauncher(IRtssSystem? system=null,TimeSpan? startWait=null,TimeSpan? poll=null)
    {
        this.system=system??new RtssSystem();this.startWait=startWait??TimeSpan.FromSeconds(8);this.poll=poll??TimeSpan.FromMilliseconds(250);
    }

    // 程式啟動時呼叫一次：偵測安裝與執行狀態；autoStart 為 true 且未執行、找到有效 RTSS.exe 時才啟動。
    public Task<RtssLaunchStatus> StartupAsync(string? configured,bool autoStart)=>Run(configured,autoStart);
    // 手動「立即啟動 RTSS」。
    public Task<RtssLaunchStatus> StartAsync(string? configured)=>Run(configured,true);
    // 重新偵測安裝位置與執行狀態（路徑變更、視窗開啟時）；不啟動。
    public Task<RtssLaunchStatus> RefreshAsync(string? configured)=>Run(configured,false);

    // 只更新執行狀態，不重新掃描安裝位置；供設定視窗定期刷新。啟動中不打擾。
    public Task<RtssLaunchStatus> RefreshRunningAsync()=>Task.Run(async()=>
    {
        if(!await gate.WaitAsync(0))return Status;
        try
        {
            var current=Status;
            if(disposed || current.Install is null)return current;
            bool running=SafeRunning();
            bool mine=OwnedAlive();
            var next=Classify(current.Install,running||mine,mine,current.State==RtssRunState.StartFailed && !running?current.Error:null);
            if(next.State==current.State && next.StartedByUs==current.StartedByUs && next.Error==current.Error)return current;
            return Publish(next);
        }
        finally{gate.Release();}
    });

    private Task<RtssLaunchStatus> Run(string? configured,bool start)=>Task.Run(async()=>
    {
        await gate.WaitAsync();
        try
        {
            if(disposed)return Status;
            RtssInstallInfo install;
            try{install=system.Find(configured);}
            catch(Exception ex){install=new(null,null,false);Trace.WriteLine("RTSS detection failed: "+ex.GetType().Name);}
            bool running=SafeRunning(),mine=OwnedAlive();
            if(running||mine||!start||install.ExecutablePath is null)return Publish(Classify(install,running||mine,mine,null));
            return await Launch(install);
        }
        catch(Exception ex)
        {
            // 任何未預期的例外都轉成狀態，不讓背景工作中斷主程式。
            return Publish(new(RtssRunState.StartFailed,Status.Install,"exception-"+ex.GetType().Name,false,DateTimeOffset.Now));
        }
        finally{gate.Release();}
    });

    private async Task<RtssLaunchStatus> Launch(RtssInstallInfo install)
    {
        // 偵測後到啟動前檔案可能被移除：啟動前再驗證一次。
        string? exe=RtssInstall.Validate(install.ExecutablePath);
        if(exe is null)return Publish(new(RtssRunState.StartFailed,install with{ExecutableFound=false},"file-missing",false,DateTimeOffset.Now));
        Publish(new(RtssRunState.Starting,install,null,false,DateTimeOffset.Now));
        Interlocked.Increment(ref attempts);
        IRtssProcess? process;
        try{process=system.Start(exe);}
        catch(RtssTrustException ex){return Publish(new(RtssRunState.StartFailed,install,ex.Code,false,DateTimeOffset.Now));}
        catch(Win32Exception ex){return Publish(new(RtssRunState.StartFailed,install,ErrorCode(ex.NativeErrorCode),false,DateTimeOffset.Now));}
        catch(Exception ex) when(ex is InvalidOperationException or FileNotFoundException or UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {return Publish(new(RtssRunState.StartFailed,install,"start-"+ex.GetType().Name,false,DateTimeOffset.Now));}
        // 等候 RTSS 出現；提前結束時判斷是否已有其他 RTSS（例如同時由 Afterburner 啟動）。
        bool noProcess=process is null;
        var watch=Stopwatch.StartNew();
        TimeSpan? exitedAt=noProcess?TimeSpan.Zero:null;
        while(true)
        {
            if(exitedAt is null && process!.HasExited){exitedAt=watch.Elapsed;process.Dispose();process=null;}
            bool running=SafeRunning();
            if(process is not null && running)return Own(process,install);
            // 已結束但有其他 RTSS 在執行（例如同時由 Afterburner 啟動，或 RTSS 自身的單一執行個體機制）：不是本程式擁有。
            if(exitedAt is not null && running)return Publish(new(RtssRunState.Running,install,null,false,DateTimeOffset.Now));
            // 提前結束後保留短暫寬限，避免與另一個 RTSS 程序出現之間的空檔被誤判為失敗。
            if(exitedAt is TimeSpan at && (watch.Elapsed-at>=ExitGrace || watch.Elapsed>=startWait))
                return Publish(new(RtssRunState.StartFailed,install,noProcess?"no-process":"exited-early",false,DateTimeOffset.Now));
            // 逾時但本程式啟動的程序仍存活：視為執行中；共享記憶體是否可用另由 RtssClockService 判斷。
            if(process is not null && watch.Elapsed>=startWait)return Own(process,install);
            await Task.Delay(poll);
        }
    }
    private static readonly TimeSpan ExitGrace=TimeSpan.FromSeconds(2);

    private RtssLaunchStatus Own(IRtssProcess process,RtssInstallInfo install)
    {
        lock(state)
        {
            if(disposed){process.Dispose();return status;}
            owned?.Dispose();owned=process;
        }
        return Publish(new(RtssRunState.Running,install,null,true,DateTimeOffset.Now));
    }

    internal static string ErrorCode(int native)=>native switch
    {
        1223=>"canceled",
        2 or 3=>"file-missing",
        5=>"access-denied",
        740=>"elevation-required",
        193 or 216=>"not-executable",
        _=>"win32-"+native
    };

    private static RtssLaunchStatus Classify(RtssInstallInfo install,bool running,bool mine,string? keepError)=>
        running?new(RtssRunState.Running,install,null,mine,DateTimeOffset.Now)
        :keepError is not null?new(RtssRunState.StartFailed,install,keepError,false,DateTimeOffset.Now)
        :install.ExecutableFound?new(RtssRunState.NotRunning,install,null,false,DateTimeOffset.Now)
        :new(RtssRunState.NotInstalled,install,null,false,DateTimeOffset.Now);

    private bool SafeRunning()
    {
        try{return system.IsRunning();}
        catch(Exception ex) when(ex is InvalidOperationException or Win32Exception or NotSupportedException or UnauthorizedAccessException){return false;}
    }

    private bool OwnedAlive()
    {
        lock(state)
        {
            if(owned is null)return false;
            if(!owned.HasExited)return true;
            owned.Dispose();owned=null;return false;
        }
    }

    private RtssLaunchStatus Publish(RtssLaunchStatus next)
    {
        lock(state){if(disposed)return status;status=next;}
        try{StatusChanged?.Invoke(next);}
        catch(Exception ex){Trace.WriteLine("RTSS status handler failed: "+ex.GetType().Name);}
        return next;
    }

    // 只釋放控制代碼，絕不結束 RTSS。
    public void Dispose()
    {
        lock(state){if(disposed)return;disposed=true;owned?.Dispose();owned=null;}
    }
}
