using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using GameClockOverlay.AvaloniaApp;
using GameClockOverlay.AvaloniaApp.Models;
using MainProgram=GameClockOverlay.AvaloniaApp.Program;
using GameClockOverlay.AvaloniaApp.Services;
using GameClockOverlay.AvaloniaApp.Services.Rtss;

namespace GameClockOverlay.Avalonia.Tests;

internal static partial class Program
{
    // 隔離映射：沿用 RTSS-1 SelfTests 的佈局（v2.21、4 槽、第 0 槽 Primary、第 1 槽 Foreign、尾端 canary）。
    // 名稱隨機，絕不觸碰正式的 RTSSSharedMemoryV2。
    internal sealed class OsdFixture : IDisposable
    {
        internal const int Stride=299520,Offset=128,Count=4,Size=Offset+Count*Stride+128;
        internal readonly string Name;
        private readonly MemoryMappedFile map;
        internal readonly MemoryMappedViewAccessor View;
        internal OsdFixture(string? name=null)
        {
            Name=name??"GameClockAvalonia.Test."+Guid.NewGuid().ToString("N");
            map=MemoryMappedFile.CreateNew(Name,Size);View=map.CreateViewAccessor();Reset();
        }
        internal void Reset()
        {
            byte[] bytes=new byte[Size];
            SharedOsd.Put(bytes,0,SharedOsd.Signature);SharedOsd.Put(bytes,4,0x20015);
            SharedOsd.Put(bytes,20,Stride);SharedOsd.Put(bytes,24,Offset);SharedOsd.Put(bytes,28,Count);
            Array.Fill<byte>(bytes,0xa5,Offset,Stride);Encoding.ASCII.GetBytes("Primary\0").CopyTo(bytes,Offset+256);
            Array.Fill<byte>(bytes,0x5a,Offset+Stride,Stride);Encoding.ASCII.GetBytes("Foreign\0").CopyTo(bytes,Offset+Stride+256);
            Array.Fill<byte>(bytes,0xcc,Offset+Count*Stride,128);
            View.WriteArray(0,bytes,0,bytes.Length);
        }
        internal byte[] Snapshot(){byte[] bytes=new byte[Size];View.ReadArray(0,bytes,0,bytes.Length);return bytes;}
        internal int Owned(){var s=Snapshot();int n=0;for(int i=0;i<Count;i++)if(SharedOsd.IsOwner(s.AsSpan(Offset+i*Stride,Stride)))n++;return n;}
        internal string TextAt(int entry)
        {
            var s=Snapshot().AsSpan(Offset+entry*Stride+266752,64);int end=s.IndexOf((byte)0);
            return Encoding.ASCII.GetString(end<0?s:s[..end]);
        }
        public void Dispose(){View.Dispose();map.Dispose();}
    }
    // 外部槽、第 0 槽與尾端 canary 必須逐位元組不變（計數器與 dwBusy 位於標頭，另外比對）。
    private static bool ForeignUnchanged(byte[] before,byte[] after)=>
        before.AsSpan(OsdFixture.Offset,OsdFixture.Stride*2).SequenceEqual(after.AsSpan(OsdFixture.Offset,OsdFixture.Stride*2))
        && before.AsSpan(OsdFixture.Offset+OsdFixture.Count*OsdFixture.Stride).SequenceEqual(after.AsSpan(OsdFixture.Offset+OsdFixture.Count*OsdFixture.Stride));
    private static bool WaitUntil(Func<bool> condition,int ms=3000)
    {
        var watch=Stopwatch.StartNew();
        while(watch.ElapsedMilliseconds<ms){if(condition())return true;Thread.Sleep(20);}
        return condition();
    }

    private static void SharedOsdCore()
    {
        Check(SharedOsd.Access("GameClockAvalonia.Absent."+Guid.NewGuid()).State=="not-running","osd-missing-map");
        Check(SharedOsd.Owner!="GameClockOverlayProbe" && SharedOsd.OwnerBytes[^1]==0,"osd-owner-distinct-from-prototype");
        using(var f=new OsdFixture())
        {
            var baseline=f.Snapshot();
            Check(SharedOsd.Access(f.Name).State=="connected" && baseline.SequenceEqual(f.Snapshot()),"osd-status-read-only");
            Check(SharedOsd.Inspect(f.Name) is {State:"connected",Owned:0} && baseline.SequenceEqual(f.Snapshot()),"osd-inspect-read-only");
            Check(SharedOsd.Access(f.Name,"12:34:56") is {State:"updated",Entry:2,Text:"12:34:56",Frame:1},"osd-claim-and-readback");
            Check(SharedOsd.Access(f.Name,"01:02:03 PM") is {State:"updated",Entry:2,Text:"01:02:03 PM",Frame:2},"osd-same-owner-12h-text");
            Check(SharedOsd.Access(f.Name,"12:34") is {State:"updated",Entry:2,Text:"12:34"} && f.TextAt(2)=="12:34","osd-shorter-text-clears-old-tail");
            var after=f.Snapshot();
            Check(ForeignUnchanged(baseline,after),"osd-primary-foreign-and-canary-unchanged");
            Check(SharedOsd.Inspect(f.Name) is {Owned:1,FirstEntry:2,Text:"12:34"},"osd-inspect-finds-own-entry");
            Check(SharedOsd.Access(f.Name,clear:true) is {State:"cleared",Entry:2},"osd-normal-cleanup");
            Check(f.Snapshot().AsSpan(OsdFixture.Offset+2*OsdFixture.Stride,OsdFixture.Stride).IndexOfAnyExcept((byte)0)<0,"osd-entire-owned-entry-cleared");
            Check(SharedOsd.Access(f.Name,clear:true).State=="already-clear" && ForeignUnchanged(baseline,f.Snapshot()),"osd-cleanup-idempotent-preserves-foreign");
            (string Name,int Field,uint Value)[] invalid=
            [
                ("dead-signature",0,0xdead),("uninitialized",0,0),("old-unlocked-version",4,0x2000d),("future-version",4,0x20016),
                ("unknown-major",4,0x30000),("short-entry",20,511),("missing-ex2-region",20,266752),("header-overlap",24,36),
                ("offset-overflow",24,uint.MaxValue),("count-overflow",28,uint.MaxValue),("no-client-slots",28,1),("app-overlap",16,1)
            ];
            foreach(var test in invalid)
            {
                f.Reset();f.View.Write(test.Field,test.Value);baseline=f.Snapshot();
                Check(SharedOsd.Access(f.Name,"12:34:58").State!="updated" && baseline.SequenceEqual(f.Snapshot()),"osd-"+test.Name+"-no-write");
            }
            f.Reset();f.View.Write(36,1);baseline=f.Snapshot();
            Check(SharedOsd.Access(f.Name,"12:34:59").State=="busy" && baseline.SequenceEqual(f.Snapshot()),"osd-busy-does-not-unlock-other-client");
            f.View.Write(36,2);
            Check(SharedOsd.Access(f.Name,"12:34:59").State=="updated" && f.View.ReadInt32(36)==2,"osd-preserve-other-busy-bits");
            f.Reset();for(int i=2;i<OsdFixture.Count;i++)f.View.Write(OsdFixture.Offset+i*OsdFixture.Stride+256,(byte)'X');baseline=f.Snapshot();
            Check(SharedOsd.Access(f.Name,"12:34:59").State=="no-free-entry" && baseline.SequenceEqual(f.Snapshot()),"osd-full-slots-do-not-overwrite");
            f.Reset();SharedOsd.Access(f.Name,"12:34:59");f.View.Write(OsdFixture.Offset+2*OsdFixture.Stride+256,(byte)'X');baseline=f.Snapshot();
            Check(SharedOsd.Access(f.Name,clear:true).State=="already-clear" && baseline.SequenceEqual(f.Snapshot()),"osd-lost-owner-never-cleared");
            f.Reset();f.View.Write(4,0x2000e);f.View.Write(20,266752);
            Check(SharedOsd.Access(f.Name,"12:34:59").State=="updated","osd-v2.14-known-layout");
            bool rejected=false;try{SharedOsd.Validate(new byte[39]);}catch(InvalidDataException){rejected=true;}
            Check(rejected,"osd-truncated-header");
            f.Reset();baseline=f.Snapshot();
            foreach(string bad in new[]{"",new string('1',64),"12：00","12:00\n"})
            {
                rejected=false;try{SharedOsd.Access(f.Name,bad);}catch(ArgumentException){rejected=true;}
                Check(rejected && baseline.SequenceEqual(f.Snapshot()),"osd-reject-invalid-text-"+bad.Length);
            }
        }
        string restart="GameClockAvalonia.Restart."+Guid.NewGuid().ToString("N");
        using(var old=new OsdFixture(restart))
        {
            Check(SharedOsd.Access(restart,"10:00:00").State=="updated","osd-restart-initial-connect");
            old.View.Write(0,0xdeadu);var bytes=old.Snapshot();
            Check(SharedOsd.Access(restart,"10:00:01").State=="signature-invalid-or-dead" && bytes.SequenceEqual(old.Snapshot()),"osd-shutdown-dead-no-write");
        }
        Check(SharedOsd.Access(restart,"10:00:02").State=="not-running","osd-releases-map-handles");
        using(var fresh=new OsdFixture(restart))
        {
            Check(SharedOsd.Access(restart,"10:00:03") is {State:"updated",Frame:1},"osd-restart-reopens-fresh-map");
            Check(SharedOsd.Access(restart,clear:true).State=="cleared","osd-restart-cleanup");
        }
    }

    private static RtssClockService Service(string map,Func<DateTime>? now=null)=>
        new(map,TimeSpan.FromMilliseconds(200),TimeSpan.FromMilliseconds(300),now,()=>false);

    private static void RtssServiceCore()
    {
        var timeline=new List<object>();
        // 前次異常結束留下的自有槽：未啟用時啟動也要清掉，且不動外部槽。
        using(var f=new OsdFixture())
        {
            var baseline=f.Snapshot();
            SharedOsd.Access(f.Name,"99:99:99");
            var svc=Service(f.Name);
            svc.Configure(false,true,false);
            Check(WaitUntil(()=>f.Owned()==0 && svc.Status.Link==RtssLink.Disabled),"rtss-startup-clears-stale-own-entry");
            Check(ForeignUnchanged(baseline,f.Snapshot()),"rtss-startup-cleanup-preserves-foreign");
            long writes=svc.WriteCount;Thread.Sleep(600);
            Check(svc.WriteCount==writes && f.Owned()==0,"rtss-disabled-writes-nothing");

            svc.Configure(true,true,false);
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500),"rtss-enable-connects");
            var s=svc.Status;
            Check(s.Entry==2 && f.TextAt(2)==s.Text && System.Text.RegularExpressions.Regex.IsMatch(s.Text,@"^\d\d:\d\d:\d\d$") && s.ProcessRunning==true,"rtss-gui-status-matches-shared-memory");
            string first=s.Text;writes=svc.WriteCount;Thread.Sleep(2100);
            long perTwoSeconds=svc.WriteCount-writes;
            Check(f.TextAt(2)!=first && perTwoSeconds is >=1 and <=3,"rtss-updates-on-second-boundary-only");
            timeline.Add(new{test="second-cadence",writesIn2100ms=perTwoSeconds,accesses=svc.AccessCount});

            svc.Configure(true,true,true);
            Check(WaitUntil(()=>f.TextAt(2).EndsWith('M') && f.TextAt(2).Length==11,600),"rtss-setting-change-updates-osd-immediately");
            svc.Configure(true,false,false);
            Check(WaitUntil(()=>f.TextAt(2).Length==5,600),"rtss-minute-format-without-seconds");
            writes=svc.WriteCount;Thread.Sleep(1200);
            Check(svc.WriteCount==writes || DateTime.Now.Second<2,"rtss-minute-mode-no-per-second-writes");
            svc.Configure(true,true,false);

            svc.Configure(false,true,false);
            Check(WaitUntil(()=>f.Owned()==0 && svc.Status.Link==RtssLink.Disabled,1500),"rtss-disable-clears-own-entry");
            var stopped=f.Snapshot();writes=svc.WriteCount;Thread.Sleep(1300);
            Check(stopped.SequenceEqual(f.Snapshot()) && svc.WriteCount==writes,"rtss-no-write-after-disable");

            for(int i=0;i<30;i++)svc.Configure(i%2==0,true,false);
            svc.Configure(true,true,false);
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500) && f.Owned()==1,"rtss-repeated-toggle-single-entry");
            for(int i=0;i<30;i++)svc.Configure(i%2==1,true,false);
            svc.Configure(false,true,false);
            Check(WaitUntil(()=>f.Owned()==0,1500) && ForeignUnchanged(baseline,f.Snapshot()),"rtss-repeated-toggle-ends-clean");

            // 外部持有 dwBusy：不寫入、不解鎖；釋放後恢復。
            svc.Configure(true,true,false);WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500);
            f.View.Write(36,1);
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.Busy,1500),"rtss-busy-detected");
            var busy=f.Snapshot();Thread.Sleep(600);
            Check(busy.SequenceEqual(f.Snapshot()) && f.View.ReadInt32(36)==1,"rtss-busy-no-write-no-unlock");
            f.View.Write(36,0);
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500),"rtss-busy-recovers");

            // 不支援的版本：顯示錯誤、不寫入；修正後以錯誤重試間隔恢復。
            f.View.Write(4,0x20016);
            Check(WaitUntil(()=>svc.Status is {Link:RtssLink.Error,LastError:"unsupported-version-0x00020016"},1500),"rtss-unsupported-version-reported");
            var bad=f.Snapshot();Thread.Sleep(700);
            Check(bad.SequenceEqual(f.Snapshot()),"rtss-error-state-no-write");
            f.View.Write(4,0x20015);
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500) && svc.Status.LastError=="unsupported-version-0x00020016","rtss-recovers-and-keeps-last-error");

            // 槽已滿：不覆寫別人的槽。
            svc.Configure(false,true,false);WaitUntil(()=>f.Owned()==0,1500);
            for(int i=2;i<OsdFixture.Count;i++)f.View.Write(OsdFixture.Offset+i*OsdFixture.Stride+256,(byte)'X');
            var full=f.Snapshot();svc.Configure(true,true,false);
            Check(WaitUntil(()=>svc.Status is {Link:RtssLink.Error,Code:"no-free-entry"},1500) && full.AsSpan(OsdFixture.Offset).SequenceEqual(f.Snapshot().AsSpan(OsdFixture.Offset)),"rtss-full-slots-not-overwritten");
            f.Reset();
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500),"rtss-recovers-when-slot-frees");

            // 正常結束：清除、執行緒結束，之後任何 Configure 都不再寫入。
            var watch=Stopwatch.StartNew();bool clean=svc.Shutdown();watch.Stop();
            Check(clean && f.Owned()==0 && !svc.WorkerAlive,"rtss-shutdown-clears-and-stops-worker");
            Check(watch.ElapsedMilliseconds<1000,"rtss-shutdown-fast-when-idle");
            var final=f.Snapshot();svc.Configure(true,true,false);Thread.Sleep(500);
            Check(final.SequenceEqual(f.Snapshot()),"rtss-no-write-after-shutdown");
            Check(svc.Shutdown(),"rtss-shutdown-idempotent");
            svc.Dispose();
            timeline.Add(new{test="shutdown",ms=watch.ElapsedMilliseconds});
        }
        // RTSS 不存在 → 啟動後自動連線 → 中途關閉 → 重新啟動後建立新槽。
        string name="GameClockAvalonia.Lifecycle."+Guid.NewGuid().ToString("N");
        using(var svc=Service(name))
        {
            svc.Configure(true,true,false);
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.WaitingForRtss && svc.Status.ProcessRunning==false,1500),"rtss-absent-waits-and-probes-process");
            long before=svc.AccessCount;Thread.Sleep(1000);long perSecond=svc.AccessCount-before;
            Check(perSecond is >=3 and <=7 && svc.Status.LastError=="not-running","rtss-absent-bounded-retry");
            timeline.Add(new{test="absent-retry",accessesPerSecond=perSecond,retryMs=200});
            using(var f=new OsdFixture(name))
            {
                Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500) && f.Owned()==1,"rtss-auto-connect-when-rtss-starts");
                f.View.Write(0,0xdeadu);var dead=f.Snapshot();
                Check(WaitUntil(()=>svc.Status is {Link:RtssLink.WaitingForRtss,Code:"signature-invalid-or-dead"},1500),"rtss-closing-map-detected");
                Thread.Sleep(500);
                Check(dead.SequenceEqual(f.Snapshot()),"rtss-closing-map-no-write");
            }
            Check(WaitUntil(()=>svc.Status is {Link:RtssLink.WaitingForRtss,Code:"not-running"},1500),"rtss-exit-detected");
            using(var f=new OsdFixture(name))
            {
                Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500) && f.Owned()==1 && svc.Status.Entry==2,"rtss-restart-reconnects-with-new-entry");
            }
            // 分鐘模式：兩次寫入之間 RTSS 關閉，也要在重試間隔內改為等待，不得持續顯示「已連線」。
            svc.Configure(true,false,false);
            using(var f=new OsdFixture(name))
            {
                Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500),"rtss-minute-mode-connects");
                long w=svc.WriteCount;Thread.Sleep(700);
                Check(svc.WriteCount==w || DateTime.Now.Second<2,"rtss-minute-mode-probes-without-writing");
            }
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.WaitingForRtss,1000),"rtss-minute-mode-detects-rtss-exit-between-writes");
            svc.Configure(true,true,false);
            using(var f=new OsdFixture(name))
            {
                Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500),"rtss-reconnect-after-minute-mode");
                Check(svc.Shutdown() && f.Owned()==0,"rtss-restart-then-shutdown-clean");
            }
        }
        // 外部持續持有 dwBusy 時，結束等待有上限，且誠實回報未能清除；下次啟動再清。
        using(var f=new OsdFixture())
        {
            var svc=Service(f.Name);svc.Configure(true,true,false);
            WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500);
            f.View.Write(36,1);WaitUntil(()=>svc.Status.Link==RtssLink.Busy,1500);
            var watch=Stopwatch.StartNew();bool clean=svc.Shutdown(TimeSpan.FromSeconds(3));watch.Stop();
            Check(!clean && watch.ElapsedMilliseconds<3200 && f.View.ReadInt32(36)==1 && f.Owned()==1,"rtss-shutdown-bounded-and-honest-when-busy");
            timeline.Add(new{test="shutdown-busy",ms=watch.ElapsedMilliseconds,clean});
            svc.Dispose();
            f.View.Write(36,0);
            using var next=Service(f.Name);next.Configure(false,true,false);
            Check(WaitUntil(()=>f.Owned()==0,1500),"rtss-next-start-clears-residue");
        }
        // 例外不終止後端：時間來源丟出例外時回報錯誤，之後自動恢復。
        using(var f=new OsdFixture())
        {
            int calls=0;
            using var svc=Service(f.Name,()=>Interlocked.Increment(ref calls)==1?throw new InvalidOperationException("injected"):DateTime.Now);
            svc.Configure(true,true,false);
            Check(WaitUntil(()=>svc.Status.LastError=="exception-InvalidOperationException",1500),"rtss-exception-reported");
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected,1500) && svc.WorkerAlive,"rtss-exception-recovers");
        }
        // 設定：缺少新欄位的 Avalonia-1 設定檔可載入且預設關閉；新欄位可保存及讀回。
        string dir=Path.Combine(evidence,"rtss-settings");Directory.CreateDirectory(dir);
        string legacy=Path.Combine(dir,"avalonia1.json");
        var oldJson=JsonSerializer.SerializeToNode(new ClockSettings{ShowSeconds=true,FontSize=44})!.AsObject();oldJson.Remove("GameClockEnabled");
        File.WriteAllText(legacy,oldJson.ToJsonString());
        Check(SettingsStore.Read(legacy) is {GameClockEnabled:false,ShowSeconds:true,FontSize:44},"settings-avalonia1-file-loads-game-clock-off");
        var store=new SettingsStore(Path.Combine(dir,"new.json"));
        Check(store.Save(new ClockSettings{GameClockEnabled=true}) && new SettingsStore(store.Path).Load().GameClockEnabled,"settings-game-clock-roundtrip");
        // 連動啟動新增 AutoStartRtss／RtssPath 兩個使用者設定；檢查改為「只保存 ClockSettings 的公開屬性」，執行期狀態不得進入設定檔。
        var savedNames=JsonDocument.Parse(File.ReadAllText(store.Path)).RootElement.EnumerateObject().Select(p=>p.Name).ToHashSet();
        Check(savedNames.SetEquals(typeof(ClockSettings).GetProperties().Select(p=>p.Name)) && !savedNames.Overlaps(["Link","Status","ProcessRunning","StartedByUs","LastError","Entry"])
            && !File.ReadAllText(store.Path).Contains("Error",StringComparison.Ordinal),"settings-no-runtime-rtss-state");
        File.WriteAllText(Path.Combine(evidence,"rtss-timing.json"),JsonSerializer.Serialize(timeline,new JsonSerializerOptions{WriteIndented=true}));
    }

    private static OsdFixture? uiFixture;
    private static byte[]? uiBaseline;
    private static global::Avalonia.Controls.TextBlock RtssValue(global::Avalonia.Controls.Window window,string key)=>
        global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<global::Avalonia.Controls.TextBlock>().Single(x=>global::Avalonia.Automation.AutomationProperties.GetName(x)==Strings.Get(key));
    private static async Task<bool> UiWait(Func<bool> condition,int ms=3000)
    {
        var watch=Stopwatch.StartNew();
        while(watch.ElapsedMilliseconds<ms){if(condition())return true;await Task.Delay(30);}
        return condition();
    }
    // GUI 狀態必須與後端實際狀態及共享記憶體內容一致；隔離映射在測試中途才建立，模擬 RTSS 晚啟動。
    private static async Task RtssUi(GameClockOverlay.AvaloniaApp.App app)
    {
        var vm=app.ViewModel;var window=app.SettingsWindow;var clock=app.Clock;
        window.SelectedTab=3;await Task.Delay(200);
        var box=global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<global::Avalonia.Controls.CheckBox>().Single(x=>global::Avalonia.Automation.AutomationProperties.GetName(x)==Strings.Get("GameEnable"));
        var link=RtssValue(window,"RtssLinkLabel");
        Check(!vm.GameClockEnabled && box.IsChecked==false && link.Text==Strings.Get("LinkDisabled") && app.Rtss.Status.Link==RtssLink.Disabled,"gui-rtss-default-off");
        Check(await UiWait(()=>RtssValue(window,"RtssInstallLabel").Text!=Strings.Get("RtssChecking")),"gui-rtss-install-detection-finishes");
        File.WriteAllText(Path.Combine(evidence,"rtss-install.json"),JsonSerializer.Serialize(app.RtssInstallation));
        box.IsChecked=true;
        Check(await UiWait(()=>app.Rtss.Status.Link==RtssLink.WaitingForRtss && link.Text==string.Format(Strings.Get(app.Rtss.Status.ProcessRunning==true?"LinkWaitingRunning":"LinkWaiting"),app.Rtss.Retry.TotalSeconds)),"gui-rtss-absent-shows-waiting");
        Check(!link.Text!.StartsWith(Strings.Get("LinkConnected")[..3],StringComparison.Ordinal) && RtssValue(window,"RtssErrorLabel").Text!.Contains(Strings.Get("ErrNotRunning")),"gui-rtss-absent-not-connected-and-last-error");
        vm.ShowSeconds=true;string tick=clock.TimeText;await Task.Delay(1200);
        Check(clock.TimeText!=tick && clock.IsVisible,"desktop-clock-runs-while-rtss-absent");
        Capture(window,"rtss-waiting.png");
        uiFixture=new OsdFixture(app.Rtss.MapName);uiBaseline=uiFixture.Snapshot();
        Check(await UiWait(()=>app.Rtss.Status.Link==RtssLink.Connected && uiFixture.Owned()==1),"gui-rtss-auto-connects-when-map-appears");
        // 同一次取樣比對 GUI、後端快照與共享記憶體，避免跨秒更新造成的假失敗。
        Check(await UiWait(()=>
        {
            var st=app.Rtss.Status;
            return st.Link==RtssLink.Connected && link.Text==string.Format(Strings.Get("LinkConnected"),"2.21",st.Entry,st.Text)
                && uiFixture.TextAt(st.Entry)==(st.Positioned?SharedOsd.PlacementTag(st.Placement):"")+st.Text && System.Text.RegularExpressions.Regex.IsMatch(st.Text,@"^\d\d:\d\d:\d\d$");
        }),"gui-rtss-connected-matches-backend-and-memory");
        Check(RtssValue(window,"RtssProcessLabel").Text==Strings.Get("RtssRunning"),"gui-rtss-process-running-when-connected");
        Capture(window,"rtss-connected.png");
        vm.TwelveHour=true;
        Check(await UiWait(()=>uiFixture.TextAt(app.Rtss.Status.Entry).EndsWith('M')),"gui-setting-change-updates-osd");
        vm.TwelveHour=false;
        await ResourceSample(window,"rtss-enabled-settings-visible");
        box.IsChecked=false;
        Check(await UiWait(()=>uiFixture.Owned()==0 && link.Text==Strings.Get("LinkDisabled")),"gui-disable-clears-entry");
        Check(ForeignUnchanged(uiBaseline,uiFixture.Snapshot()),"gui-disable-preserves-foreign");
        box.IsChecked=true;
        Check(await UiWait(()=>uiFixture.Owned()==1 && app.Rtss.Status.Link==RtssLink.Connected),"gui-re-enable-reconnects");
        // RTSS 端結構異常：GUI 顯示錯誤，桌面時鐘不受影響。
        uiFixture.View.Write(4,0x20016);
        Check(await UiWait(()=>app.Rtss.Status.Link==RtssLink.Error && link.Text!.Contains("unsupported-version",StringComparison.Ordinal)),"gui-rtss-error-shown");
        tick=clock.TimeText;await Task.Delay(1200);
        Check(clock.TimeText!=tick,"desktop-clock-unaffected-by-rtss-error");
        Capture(window,"rtss-error.png");
        uiFixture.View.Write(4,0x20015);
        Check(await UiWait(()=>app.Rtss.Status.Link==RtssLink.Connected),"gui-rtss-recovers-after-error");
        Check(await app.SaveAsync() && SettingsStore.Read(App.TestSettingsPath!).GameClockEnabled,"game-clock-setting-saved");
        string hash=Hash(App.TestSettingsPath!);await Task.Delay(1500);
        Check(Hash(App.TestSettingsPath!)==hash,"rtss-ticks-do-not-write-settings");
        await RtssPositionUi(app);
        window.SelectedTab=0;await Task.Delay(100);
        // 保持啟用到程式結束，由 Main 驗證退出清理。
    }

    // 實機 GUI：使用正式 RTSS 映射與獨立的測試設定檔，不改動使用者自己的設定。
    private static int RtssLiveUi(string[] args)
    {
        int seconds=args.SkipWhile(a=>a!="--seconds").Skip(1).Select(a=>int.TryParse(a,out int v)?v:0).FirstOrDefault();
        if(seconds is <=0 or >3600)seconds=60;
        var samples=new List<object>();
        App.TestReady=async app=>
        {
            try
            {
                var vm=app.ViewModel;var window=app.SettingsWindow;
                // --no-game：只供量測對照（相同條件但不啟用遊戲內時鐘），此時「至少連線一次」預期失敗。
                // --game-position X：實機定位驗證用，未指定時使用新設定的預設值（右上）。
                string? place=args.SkipWhile(a=>a!="--game-position").Skip(1).FirstOrDefault();if(place is not null)vm.GameClockPosition=place;
                vm.ShowSeconds=true;vm.GameClockEnabled=!args.Contains("--no-game");window.SelectedTab=3;
                var watch=Stopwatch.StartNew();int connected=0,mismatched=0,ticks=0,count=0,transient=0;string lastDesktop="";bool previousBad=false;
                bool measure=args.Contains("--measure");
                while(watch.Elapsed.TotalSeconds<seconds)
                {
                    await Task.Delay(1000);
                    // --measure：第 15–80 秒隱藏設定視窗並停止觀察器，讓外部量測只反映常駐時（桌面＋RTSS）的負載。
                    if(measure && watch.Elapsed.TotalSeconds is >=15 and <80)
                    {
                        if(window.IsVisible){window.Hide();Console.WriteLine($"{DateTime.Now:HH:mm:ss} QUIET start");}
                        continue;
                    }
                    if(measure && !window.IsVisible){window.Show();window.SelectedTab=3;Console.WriteLine($"{DateTime.Now:HH:mm:ss} QUIET end");await Task.Delay(300);}
                    var st=app.Rtss.Status;var seen=SharedOsd.Inspect(SharedOsd.MapName);string gui=RtssValue(window,"RtssLinkLabel").Text??"";
                    count++;
                    if(app.Clock.TimeText!=lastDesktop){ticks++;lastDesktop=app.Clock.TimeText;}
                    if(st.Link==RtssLink.Connected)connected++;
                    // GUI 只有在後端實際寫入成功時才顯示已連線。後端與觀察器在不同時刻取樣：RTSS 剛關閉的那一筆
                    // 可能仍是上一秒的成功結果，只接受單筆、且下一筆即恢復一致；連續兩筆不一致即判定失敗。
                    bool guiConnected=gui.StartsWith(string.Format(Strings.Get("LinkConnected"),"","","")[..3],StringComparison.Ordinal);
                    bool bad=(st.Link==RtssLink.Connected && seen.Owned!=1) || (guiConnected && st.Link!=RtssLink.Connected);
                    if(bad && previousBad)mismatched++;else if(bad)transient++;
                    previousBad=bad;
                    string pos=RtssValue(window,"RtssPositionLabel").Text??"";
                    samples.Add(new{at=DateTimeOffset.Now,link=st.Link.ToString(),st.Code,st.Entry,st.Text,st.ProcessRunning,st.LastError,placement=st.Placement.ToString(),st.Positioned,observed=seen.State,observedOwned=seen.Owned,observedEntry=seen.FirstEntry,observedText=seen.Text,observedTag=seen.Tag,gui,guiPosition=pos,desktop=app.Clock.TimeText,desktopPosition=app.Clock.Position.ToString()});
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss} {st.Link} {st.Text} slot={st.Entry} positioned={st.Positioned} osd={seen.State}/{seen.Owned}/#{seen.FirstEntry}/{seen.Tag}{seen.Text} gui-pos={pos} desktop={app.Clock.TimeText}@{app.Clock.Position}");
                    if(watch.Elapsed.TotalSeconds>5 && watch.Elapsed.TotalSeconds<7)Capture(window,"live-gui.png");
                }
                samples.Add(new{phase="summary",count,connected,ticks,transient,mismatched});
                Check(mismatched==0,"live-ui-gui-backend-memory-consistent");
                Check(ticks>=count-1,"live-ui-desktop-clock-kept-ticking");
                Check(connected>0,"live-ui-connected-at-least-once");
            }
            catch(Exception ex){Check(false,"live-ui-exception-"+ex);}
            finally{await app.QuitAsync();}
        };
        int exit=MainProgram.Main(["--ui-test"]);
        var after=SharedOsd.Inspect(SharedOsd.MapName);
        samples.Add(new{at=DateTimeOffset.Now,phase="after-exit",after.State,after.Owned});
        File.WriteAllText(Path.Combine(evidence,"rtss-live-ui.json"),JsonSerializer.Serialize(samples,new JsonSerializerOptions{WriteIndented=true}));
        Check(exit==0,"live-ui-normal-exit");
        Check(after.Owned==0,"live-ui-exit-leaves-no-own-entry");
        WriteResults();return failed==0?0:1;
    }

    // 實機：寫入正式的 RTSSSharedMemoryV2。只在明確指定 --rtss-live 時執行。
    private static int RtssLive(int seconds)
    {
        var log=new List<object>();
        log.Add(new{at=DateTimeOffset.Now,phase="before",inspect=SharedOsd.Inspect(SharedOsd.MapName).ToString(),rtss=RtssClockService.IsRtssProcessRunning()});
        using var svc=new RtssClockService();
        RtssLink last=(RtssLink)(-1);
        svc.StatusChanged+=s=>{if(s.Link!=last){last=s.Link;lock(log)log.Add(new{at=DateTimeOffset.Now,phase="status",link=s.Link.ToString(),s.Code,s.Entry,version=$"0x{s.Version:X8}",s.LastError,s.ProcessRunning});}};
        svc.Configure(true,true,false);
        var watch=Stopwatch.StartNew();int connectedSamples=0,matched=0;
        while(watch.Elapsed.TotalSeconds<seconds)
        {
            Thread.Sleep(1000);
            var st=svc.Status;var seen=SharedOsd.Inspect(SharedOsd.MapName);
            if(st.Link==RtssLink.Connected){connectedSamples++;if(seen.Owned==1 && seen.Text.Length==8)matched++;}
            lock(log)log.Add(new{at=DateTimeOffset.Now,phase="sample",link=st.Link.ToString(),st.Text,observedState=seen.State,observedOwned=seen.Owned,observedEntry=seen.FirstEntry,observedText=seen.Text});
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} {st.Link} {st.Text} observed={seen.State}/{seen.Owned}/{seen.Text}");
        }
        bool clean=svc.Shutdown();
        var after=SharedOsd.Inspect(SharedOsd.MapName);
        log.Add(new{at=DateTimeOffset.Now,phase="after",clean,inspect=after.ToString(),writes=svc.WriteCount,accesses=svc.AccessCount});
        File.WriteAllText(Path.Combine(evidence,"rtss-live.json"),JsonSerializer.Serialize(log,new JsonSerializerOptions{WriteIndented=true}));
        Check(connectedSamples==0 || matched>=connectedSamples-1,"live-observer-sees-single-own-entry");
        Check(clean && after.Owned==0,"live-shutdown-leaves-no-own-entry");
        WriteResults();return failed==0?0:1;
    }
}
