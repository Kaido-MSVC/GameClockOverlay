using System.Diagnostics;

namespace GameClockOverlay.AvaloniaApp.Services.Rtss;

public enum RtssLink { Disabled, Cleaning, WaitingForRtss, Connected, Busy, Error }

// 不可變的狀態快照；只由背景工作執行緒產生，GUI 讀取後顯示。不寫入設定檔。
public sealed record RtssStatus(
    RtssLink Link, string Code, int Entry = -1, uint Version = 0, string Text = "",
    DateTimeOffset? LastWrite = null, string? LastError = null, DateTimeOffset? LastErrorAt = null,
    bool? ProcessRunning = null, bool OwnsEntry = false,
    OsdPlacement Placement = OsdPlacement.Rtss, bool Positioned = false);

// RTSS 遊戲內時鐘後端。單一背景執行緒依秒／分鐘邊界寫入；未連線時以固定間隔重試。
// 所有共享記憶體存取都在 gate 內進行，並在鎖內重新檢查啟用狀態：
// Configure(false) 或 Shutdown() 返回之後，不可能再有任何寫入。
public sealed class RtssClockService : IDisposable
{
    private static readonly TimeSpan BusyRetry=TimeSpan.FromMilliseconds(250),CleanRetry=TimeSpan.FromMilliseconds(50);
    private const int MaxCleanAttempts=40;
    private readonly string map;
    private readonly TimeSpan retry,errorRetry;
    private readonly Func<DateTime> now;
    private readonly Func<bool> rtssProcess;
    private readonly Lock gate=new();
    private readonly AutoResetEvent wake=new(false);
    private Thread? worker;
    private bool enabled,showSeconds,twelveHour,disposed;
    private OsdPlacement placement=OsdPlacement.Rtss;
    // 前次異常結束可能留下自己的槽；啟動後第一輪若未啟用，會先清除一次。
    private bool mayOwnEntry=true;
    private int cleanAttempts;
    private DateTime nextWrite=DateTime.MinValue;
    private RtssStatus status=new(RtssLink.Disabled,"disabled");
    private long accessCount,writeCount;
    public event Action<RtssStatus>? StatusChanged;
    public string MapName=>map;
    public TimeSpan Retry=>retry;
    public TimeSpan ErrorRetry=>errorRetry;
    public long AccessCount=>Interlocked.Read(ref accessCount);
    public long WriteCount=>Interlocked.Read(ref writeCount);
    public bool WorkerAlive=>worker?.IsAlive==true;
    public RtssStatus Status{get{lock(gate)return status;}}

    public RtssClockService(string map=SharedOsd.MapName,TimeSpan? retry=null,TimeSpan? errorRetry=null,Func<DateTime>? now=null,Func<bool>? rtssProcess=null)
    {
        this.map=map;this.retry=retry??TimeSpan.FromSeconds(3);this.errorRetry=errorRetry??TimeSpan.FromSeconds(10);
        this.now=now??(()=>DateTime.Now);this.rtssProcess=rtssProcess??IsRtssProcessRunning;
    }

    public static bool IsRtssProcessRunning()
    {
        var found=Process.GetProcessesByName("RTSS");
        foreach(var p in found)p.Dispose();
        return found.Length>0;
    }

    // 可在 UI 執行緒重複呼叫；只有內容改變才喚醒工作執行緒，不會排入額外寫入。
    // place 預設 Rtss（不加位置標籤），與 Avalonia-2 的行為相同。
    public void Configure(bool enable,bool seconds,bool twelve,OsdPlacement place=OsdPlacement.Rtss)
    {
        lock(gate)
        {
            if(disposed)return;
            bool changed=enable!=enabled||seconds!=showSeconds||twelve!=twelveHour||place!=placement||worker is null;
            enabled=enable;showSeconds=seconds;twelveHour=twelve;placement=place;
            if(changed)nextWrite=DateTime.MinValue;
            if(enable)cleanAttempts=0;
            if(worker is null){worker=new Thread(Run){IsBackground=true,Name="GameClock RTSS"};worker.Start();}
            if(!changed)return;
        }
        wake.Set();
    }

    // RTSS 剛啟動時呼叫：等待中的工作執行緒立即重試連線，不必等到下一次重試間隔。未啟用時不做任何事。
    public void Nudge()
    {
        lock(gate){if(disposed || worker is null || !enabled || status.Link==RtssLink.Connected)return;wake.Set();}
    }

    private void Run()
    {
        while(true)
        {
            TimeSpan wait;bool probe=false;RtssStatus? report=null;
            lock(gate)
            {
                try
                {
                    if(!enabled||disposed)
                    {
                        if(mayOwnEntry)
                        {
                            var r=Access(null,clear:true);
                            if(r.State=="busy" && ++cleanAttempts<MaxCleanAttempts){report=Set(RtssLink.Cleaning,r);wait=CleanRetry;}
                            else
                            {
                                // 已清除、本來就沒有、或 RTSS 已不存在（映射消失即槽消失）都算完成。
                                // 其他結果（持續忙碌、權限、結構錯誤）無法確認已清除：保留 mayOwnEntry，停止重試，下次狀態變更或下次啟動再清。
                                bool done=r.State is "cleared" or "already-clear" or "not-running" or "signature-invalid-or-dead";
                                mayOwnEntry=!done;cleanAttempts=0;
                                report=Set(RtssLink.Disabled,r,error:!done);
                                if(disposed)return;
                                wait=Timeout.InfiniteTimeSpan;
                            }
                        }
                        else
                        {
                            if(disposed)return;
                            if(status.Link!=RtssLink.Disabled)report=Set(RtssLink.Disabled,new("disabled"));
                            wait=Timeout.InfiniteTimeSpan;
                        }
                    }
                    else
                    {
                        var at=now();
                        // 分鐘模式兩次寫入相隔最長 60 秒；期間以唯讀驗證確認 RTSS 仍在，避免 RTSS 關閉後仍顯示「已連線」。
                        bool probeOnly=status.Link==RtssLink.Connected && at<nextWrite;
                        var r=probeOnly?Access(null,clear:false):Access(ClockMath.Format(at,showSeconds,twelveHour),clear:false);
                        switch(r.State)
                        {
                            case "connected" when r.Stale:
                                // 自己的槽不見了（RTSS 快速重啟等），或後方出現其他程式的槽、或原本退回現在可以定位：立即重寫。
                                nextWrite=DateTime.MinValue;wait=TimeSpan.Zero;break;
                            case "connected":
                                wait=Min(nextWrite-now(),retry);break;
                            case "updated":
                                mayOwnEntry=true;Interlocked.Increment(ref writeCount);
                                report=Set(RtssLink.Connected,r,lastWrite:at);
                                var next=ClockMath.NextUpdate(now(),showSeconds);nextWrite=now()+next;wait=Min(next,retry);break;
                            case "busy":
                                report=Set(RtssLink.Busy,r);wait=BusyRetry;break;
                            case "claim-conflict":
                                // 其他程式剛好同時認領同一個空槽：已放棄該槽，短暫等待後重新掃描。
                                report=Set(RtssLink.Busy,r,error:true);wait=BusyRetry;break;
                            case "not-running" or "signature-invalid-or-dead":
                                // 映射不存在或已標記待釋放：舊槽隨之失效，重新出現時再配置。
                                mayOwnEntry=false;probe=true;report=Set(RtssLink.WaitingForRtss,r,error:true);wait=retry;break;
                            default:
                                report=Set(RtssLink.Error,r,error:true);wait=errorRetry;break;
                        }
                    }
                }
                catch(Exception ex)
                {
                    // 後端故障不得影響桌面時鐘：記錄類型後以錯誤間隔重試。
                    report=Set(RtssLink.Error,new("exception-"+ex.GetType().Name),error:true);wait=errorRetry;
                }
            }
            if(probe)
            {
                bool running=rtssProcess();
                lock(gate){if(status.Link==RtssLink.WaitingForRtss){status=status with{ProcessRunning=running};report=status;}}
            }
            if(report is not null)StatusChanged?.Invoke(report);
            if(wait==Timeout.InfiniteTimeSpan)wake.WaitOne();else wake.WaitOne(wait);
        }
    }

    private static TimeSpan Min(TimeSpan a,TimeSpan b)=>a<TimeSpan.Zero?TimeSpan.Zero:a<b?a:b;
    private OsdResult Access(string? text,bool clear){Interlocked.Increment(ref accessCount);return SharedOsd.Access(map,text,clear,placement);}

    private RtssStatus Set(RtssLink link,OsdResult r,bool error=false,DateTime? lastWrite=null)
    {
        var at=DateTimeOffset.Now;
        status=status with
        {
            Link=link,Code=r.State,Entry=r.Entry,Version=r.Version!=0?r.Version:status.Version,Text=link==RtssLink.Connected?r.Text:"",
            LastWrite=lastWrite is DateTime w?new DateTimeOffset(w):link==RtssLink.Disabled?null:status.LastWrite,
            LastError=error?r.State:status.LastError,LastErrorAt=error?at:status.LastErrorAt,
            // 能開啟並驗證映射即代表 RTSS 存在；等待中由程序偵測補上；錯誤時沿用上一次結果。
            ProcessRunning=link switch{RtssLink.Connected or RtssLink.Busy=>true,RtssLink.WaitingForRtss or RtssLink.Error=>status.ProcessRunning,_=>null},OwnsEntry=mayOwnEntry,
            Placement=placement,Positioned=link==RtssLink.Connected && r.Positioned
        };
        return status;
    }

    // 正常退出與 Windows 登出／關機時同步呼叫。有上限的等待：不會無限阻擋結束工作階段。
    // 回傳 true 表示已確認沒有殘留的自有槽。
    public bool Shutdown(TimeSpan? timeout=null)
    {
        Thread? t;
        lock(gate)
        {
            if(disposed && (worker is null || !worker.IsAlive))return !mayOwnEntry;
            enabled=false;disposed=true;t=worker;
        }
        if(t is null)
        {
            // 從未啟動工作執行緒：直接在呼叫端清除一次。
            lock(gate){var r=Access(null,clear:true);mayOwnEntry=r.State is not("cleared" or "already-clear" or "not-running" or "signature-invalid-or-dead");return !mayOwnEntry;}
        }
        wake.Set();
        bool joined=t.Join(timeout??TimeSpan.FromMilliseconds(2500));
        lock(gate)return joined && !mayOwnEntry;
    }

    public void Dispose()
    {
        // 工作執行緒仍存活時不釋放事件，避免背景執行緒存取已釋放物件。
        if(Shutdown() || worker is null || !worker.IsAlive)wake.Dispose();
    }
}
