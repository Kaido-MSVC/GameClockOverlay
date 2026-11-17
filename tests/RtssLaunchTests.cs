using System.ComponentModel;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.VisualTree;
using GameClockOverlay.AvaloniaApp;
using GameClockOverlay.AvaloniaApp.Models;
using GameClockOverlay.AvaloniaApp.Services;
using GameClockOverlay.AvaloniaApp.Services.Rtss;
using GameClockOverlay.AvaloniaApp.Views;
using MainProgram=GameClockOverlay.AvaloniaApp.Program;

namespace GameClockOverlay.Avalonia.Tests;

// RTSS 安裝偵測與連動啟動。全部是模擬測試：以 FakeRtss 取代程序清單與 Process.Start，
// 「RTSS.exe」是測試資料夾內的空檔案，絕不執行；真實 RTSS 的啟動需另行手動驗證。
internal static partial class Program
{
    internal sealed class FakeProcess : IRtssProcess
    {
        public volatile bool Exited;
        public volatile bool Disposed;
        public bool HasExited=>Exited;
        public void Dispose()=>Disposed=true;
    }
    internal sealed class FakeRtss(Func<string?,RtssInstallInfo> find) : IRtssSystem
    {
        public volatile bool Running;
        public int Starts;
        public string? LastStarted;
        public Func<string,IRtssProcess?>? OnStart;
        public TimeSpan StartDelay=TimeSpan.Zero;
        public RtssInstallInfo Find(string? configured)=>find(configured);
        public bool IsRunning()=>Running;
        public IRtssProcess? Start(string executable)
        {
            Interlocked.Increment(ref Starts);LastStarted=executable;
            if(StartDelay>TimeSpan.Zero)Thread.Sleep(StartDelay);
            if(OnStart is not null)return OnStart(executable);
            Running=true;return new FakeProcess();
        }
    }

    private static string FakeExe(string dir)
    {
        Directory.CreateDirectory(dir);string exe=Path.Combine(dir,"RTSS.exe");
        if(!File.Exists(exe))File.WriteAllBytes(exe,[]);
        return exe;
    }

    private static void RtssLaunchCore()
    {
        string root=Path.Combine(evidence,"rtss-launch");
        string custom=FakeExe(Path.Combine(root,"非預設 路徑","RivaTuner Statistics Server"));
        string common=FakeExe(Path.Combine(root,"Test Apps","RivaTuner Statistics Server"));
        string missing=Path.Combine(root,"已移除","RTSS.exe");
        string notRtss=Path.Combine(root,"other","notepad.exe");Directory.CreateDirectory(Path.GetDirectoryName(notRtss)!);File.WriteAllBytes(notRtss,[]);
        IEnumerable<RtssCandidate> None()=>[];
        IEnumerable<RtssCandidate> Reg(string path)=>[new(path,"7.3.7",RtssInstallSource.Registry)];
        IEnumerable<RtssCandidate> Common()=>[new(common,null,RtssInstallSource.CommonPath)];

        // ── 安裝偵測順序與路徑驗證 ──
        var r=RtssInstall.Find(custom,()=>Reg(common),None,File.Exists);
        Check(r is {ExecutableFound:true,Source:RtssInstallSource.Settings,ConfiguredPathInvalid:false} && r.ExecutablePath==custom,"rtss-detect-configured-path-first");
        r=RtssInstall.Find(null,()=>Reg(custom),Common,File.Exists);
        Check(r is {ExecutableFound:true,Source:RtssInstallSource.Registry,Version:"7.3.7"} && r.ExecutablePath==custom,"rtss-detect-registry-non-default-path");
        r=RtssInstall.Find(missing,()=>Reg(custom),Common,File.Exists);
        Check(r is {ExecutableFound:true,Source:RtssInstallSource.Registry,ConfiguredPathInvalid:true} && r.ExecutablePath==custom,"rtss-detect-stale-configured-path-falls-back");
        r=RtssInstall.Find(notRtss,None,None,File.Exists);
        Check(r is {ExecutableFound:false,ConfiguredPathInvalid:true,Directory:null},"rtss-detect-rejects-non-rtss-executable");
        Check(RtssInstall.Validate("RTSS.exe")is null && RtssInstall.Validate(@"..\RTSS.exe")is null && RtssInstall.Validate("")is null && RtssInstall.Validate(custom)==custom,"rtss-validate-requires-existing-absolute-rtss-exe");
        r=RtssInstall.Find(null,()=>Reg(missing),Common,File.Exists);
        Check(r is {ExecutableFound:true,Source:RtssInstallSource.CommonPath} && r.ExecutablePath==common,"rtss-detect-common-path-after-stale-registry");
        r=RtssInstall.Find(null,()=>Reg(missing),None,File.Exists);
        Check(r is {ExecutableFound:false,ExecutablePath:null} && r.Directory==Path.GetDirectoryName(missing),"rtss-detect-registry-record-without-exe");
        r=RtssInstall.Find(null,None,None,File.Exists);
        Check(r is {ExecutableFound:false,Directory:null,Source:RtssInstallSource.None},"rtss-detect-not-installed");
        r=RtssInstall.Find(null,()=>throw new UnauthorizedAccessException("injected"),Common,File.Exists);
        Check(r is {ExecutableFound:true,Source:RtssInstallSource.CommonPath},"rtss-detect-registry-error-skipped");
        Check(RtssInstall.DirectoryOf("\"X:\\Test Apps\\RivaTuner Statistics Server\\uninstall.exe\" /S")==@"X:\Test Apps\RivaTuner Statistics Server"
            && RtssInstall.DirectoryOf(@"C:\RTSS\uninstall.exe /S")==@"C:\RTSS","rtss-uninstall-string-parsing");
        // 本機實際偵測結果只記錄、不作為通過條件（依開發機安裝狀況而異）。
        var real=RtssInstall.Find();
        File.WriteAllText(Path.Combine(evidence,"rtss-install-real.json"),JsonSerializer.Serialize(new{real,candidates=RtssInstall.RegistryCandidates()},new JsonSerializerOptions{WriteIndented=true}));
        Check(true,"rtss-detect-real-machine-recorded(informational)");

        RtssInstallInfo Installed(string? _)=>RtssInstall.Find(null,()=>Reg(custom),None,File.Exists);
        RtssInstallInfo NotInstalled(string? cfg)=>RtssInstall.Find(cfg,None,None,File.Exists);
        RtssLauncher L(FakeRtss f)=>new(f,TimeSpan.FromMilliseconds(600),TimeSpan.FromMilliseconds(20));

        // ── 狀態與連動啟動 ──
        {
            var f=new FakeRtss(Installed){Running=true};using var l=L(f);
            var s=l.StartupAsync(null,true).Result;
            Check(s is {State:RtssRunState.Running,StartedByUs:false} && f.Starts==0,"rtss-launch-already-running-no-duplicate");
        }
        {
            var f=new FakeRtss(Installed);using var l=L(f);
            var s=l.StartupAsync(null,false).Result;
            Check(s.State==RtssRunState.NotRunning && f.Starts==0,"rtss-launch-autostart-off-does-not-start");
        }
        {
            var f=new FakeRtss(Installed);using var l=L(f);var seen=new List<RtssRunState>();
            l.StatusChanged+=x=>{lock(seen)seen.Add(x.State);};
            var s=l.StartupAsync(null,true).Result;
            Check(s is {State:RtssRunState.Running,StartedByUs:true} && f.Starts==1 && f.LastStarted==custom,"rtss-launch-autostart-on-starts-once");
            Check(seen.SequenceEqual([RtssRunState.Starting,RtssRunState.Running]),"rtss-launch-reports-starting-then-running");
        }
        {
            var f=new FakeRtss(NotInstalled);using var l=L(f);
            var s=l.StartupAsync(null,true).Result;
            Check(s.State==RtssRunState.NotInstalled && f.Starts==0,"rtss-launch-not-installed-no-start");
            s=l.StartupAsync(missing,true).Result;
            Check(s is {State:RtssRunState.NotInstalled,Install.ConfiguredPathInvalid:true} && f.Starts==0,"rtss-launch-stale-path-no-start");
        }
        {
            var f=new FakeRtss(Installed){OnStart=_=>throw new Win32Exception(1223)};using var l=L(f);
            var s=l.StartupAsync(null,true).Result;
            Check(s is {State:RtssRunState.StartFailed,Error:"canceled"},"rtss-launch-uac-canceled-reported");
            f.OnStart=_=>throw new InvalidOperationException("injected");
            s=l.StartAsync(null).Result;
            Check(s is {State:RtssRunState.StartFailed,Error:"start-InvalidOperationException"},"rtss-launch-start-exception-reported");
            Check(l.RefreshRunningAsync().Result.State==RtssRunState.StartFailed,"rtss-launch-failure-kept-until-running");
            f.Running=true;
            Check(l.RefreshRunningAsync().Result is {State:RtssRunState.Running,StartedByUs:false},"rtss-launch-recovers-when-started-elsewhere");
        }
        {
            var f=new FakeRtss(Installed){OnStart=_=>new FakeProcess{Exited=true}};using var l=L(f);
            var s=l.StartAsync(null).Result;
            Check(s is {State:RtssRunState.StartFailed,Error:"exited-early"},"rtss-launch-early-exit-failed");
            // 自己啟動的程序立即結束，但同時有其他 RTSS（例如 Afterburner 啟動）：判定執行中，但不是本程式擁有。
            f.OnStart=_=>{f.Running=true;return new FakeProcess{Exited=true};};
            s=l.StartAsync(null).Result;
            Check(s is {State:RtssRunState.Running,StartedByUs:false},"rtss-launch-race-other-instance-not-owned");
            f.Running=false;f.OnStart=_=>null;
            Check(l.StartAsync(null).Result is {State:RtssRunState.StartFailed,Error:"no-process"},"rtss-launch-null-process-failed");
        }
        {
            // 同時多次要求啟動（重複點選、啟動流程與手動啟動重疊）只會真的啟動一次。
            var f=new FakeRtss(Installed){StartDelay=TimeSpan.FromMilliseconds(150)};using var l=L(f);
            var all=Enumerable.Range(0,6).Select(_=>l.StartAsync(null)).ToArray();Task.WaitAll(all);
            Check(f.Starts==1 && all.All(t=>t.Result.State==RtssRunState.Running),"rtss-launch-concurrent-requests-single-start");
        }
        {
            // 擁有權：只有自己啟動且仍存活的程序標示為本程式啟動；結束後狀態更新，Dispose 只釋放控制代碼。
            FakeProcess? mine=null;
            var f=new FakeRtss(Installed);f.OnStart=_=>{f.Running=true;return mine=new FakeProcess();};var l=L(f);
            Check(l.StartAsync(null).Result.StartedByUs,"rtss-ownership-tracked");
            mine!.Exited=true;f.Running=false;
            Check(l.RefreshRunningAsync().Result is {State:RtssRunState.NotRunning,StartedByUs:false} && mine.Disposed,"rtss-ownership-cleared-when-exits");
            var other=new FakeRtss(Installed){Running=true};var l2=L(other);l2.StartupAsync(null,true).Wait();
            l2.Dispose();l.Dispose();
            Check(other.Running && other.Starts==0 && l2.Status.StartedByUs==false,"rtss-foreign-instance-never-touched");
            // 介面的 GetMethods 不含繼承自 IDisposable 的 Dispose：唯一成員只能是 HasExited。
            Check(typeof(IRtssProcess).GetMethods().Select(m=>m.Name).SequenceEqual(["get_HasExited"]) && typeof(IRtssProcess).GetInterfaces().SequenceEqual([typeof(IDisposable)])
                && !typeof(RtssLauncher).GetMethods().Any(m=>m.Name.Contains("Kill",StringComparison.OrdinalIgnoreCase)||m.Name.Contains("Close",StringComparison.OrdinalIgnoreCase)),"rtss-no-termination-api");
        }
        {
            // 啟動前檔案被移除：偵測時存在、啟動前再次驗證。
            string gone=FakeExe(Path.Combine(root,"gone"));
            var f=new FakeRtss(_=>RtssInstall.Find(gone,None,None,File.Exists));using var l=L(f);
            File.Delete(gone);
            var s=l.StartAsync(null).Result;
            Check(f.Starts==0 && s.State is RtssRunState.NotInstalled or RtssRunState.StartFailed,"rtss-launch-path-removed-before-start");
            var f2=new FakeRtss(_=>new(Path.GetDirectoryName(gone),null,true,gone,RtssInstallSource.Settings));using var l2=L(f2);
            Check(l2.StartAsync(null).Result is {State:RtssRunState.StartFailed,Error:"file-missing"} && f2.Starts==0,"rtss-launch-revalidates-before-start");
        }
        {
            var f=new FakeRtss(_=>throw new IOException("injected"));using var l=L(f);
            l.StatusChanged+=_=>throw new InvalidOperationException("handler");
            Check(l.StartupAsync(null,true).Result.State==RtssRunState.NotInstalled && f.Starts==0,"rtss-launch-detection-and-handler-errors-contained");
        }
        RtssTrustCore(root,custom);
        Check(RtssLauncher.ErrorCode(1223)=="canceled" && RtssLauncher.ErrorCode(2)=="file-missing" && RtssLauncher.ErrorCode(5)=="access-denied" && RtssLauncher.ErrorCode(1155)=="win32-1155","rtss-launch-error-codes");
        foreach(string language in new[]{"zh-TW","en","ja"})
        {
            Strings.Use(language);
            Check(new[]{"unsigned","signature-invalid","publisher-mismatch","unsupported-path","canceled","file-missing","access-denied","elevation-required","not-executable","exited-early","no-process","timeout","start-InvalidOperationException"}
                .All(c=>SettingsWindow.LaunchErrorText(c) is {Length:>0} t && !t.Contains('{')),"rtss-launch-error-text-"+language);
        }
        Strings.Use("zh-TW");

        // ── 設定持久化 ──
        Check(new ClockSettings() is {AutoStartRtss:true,RtssPath:""},"settings-rtss-autostart-default-on");
        string dir=Path.Combine(root,"settings");Directory.CreateDirectory(dir);
        string old=Path.Combine(dir,"v030.json");
        var json=JsonSerializer.SerializeToNode(new ClockSettings{GameClockEnabled=true})!.AsObject();json.Remove("AutoStartRtss");json.Remove("RtssPath");
        File.WriteAllText(old,json.ToJsonString());
        Check(SettingsStore.Read(old) is {AutoStartRtss:true,RtssPath:"",GameClockEnabled:true},"settings-rtss-old-file-defaults");
        var store=new SettingsStore(Path.Combine(dir,"launch.json"));
        Check(store.Save(new ClockSettings{AutoStartRtss=false,RtssPath=custom}) && new SettingsStore(store.Path).Load() is {AutoStartRtss:false} loaded && loaded.RtssPath==custom,"settings-rtss-roundtrip");
        Check(store.Save(new ClockSettings{AutoStartRtss=true,RtssPath=missing}) && new SettingsStore(store.Path).Load() is {AutoStartRtss:true} kept && kept.RtssPath==missing,"settings-rtss-stale-path-kept-for-warning");
        var oversized=new ClockSettings{RtssPath=new string('x',2000)};oversized.Normalize();
        Check(oversized.RtssPath=="","settings-rtss-path-oversized-cleared");

        // ── RTSS 啟動後喚醒共享記憶體重試：重試間隔 5 秒，Nudge 後應在 1 秒內連線 ──
        string map="GameClockAvalonia.Nudge."+Guid.NewGuid().ToString("N");
        using(var svc=new RtssClockService(map,TimeSpan.FromSeconds(5),TimeSpan.FromSeconds(5)))
        {
            svc.Configure(true,true,false);
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.WaitingForRtss,1500),"rtss-nudge-precondition-waiting");
            using var f=new OsdFixture(map);
            var watch=System.Diagnostics.Stopwatch.StartNew();svc.Nudge();
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected && f.Owned()==1,1000) && watch.ElapsedMilliseconds<1500,"rtss-nudge-connects-before-retry-interval");
            Check(svc.Shutdown() && f.Owned()==0,"rtss-nudge-cleanup");
        }
        using(var idle=new RtssClockService("GameClockAvalonia.Nudge."+Guid.NewGuid().ToString("N")))
        {
            idle.Nudge();idle.Configure(false,false,false);idle.Nudge();
            Check(WaitUntil(()=>idle.Status.Link==RtssLink.Disabled,1000) && idle.WriteCount==0,"rtss-nudge-disabled-noop");
        }
    }

    // 安全性修正 SEC-01：啟動前的 Authenticode 驗證。所有檔案只驗證、不執行。
    private sealed class RealStartFake(string exe) : IRtssSystem
    {
        private readonly RtssSystem real=new();
        public RtssInstallInfo Find(string? configured)=>new(Path.GetDirectoryName(exe),null,true,exe,RtssInstallSource.Settings);
        public bool IsRunning()=>false;
        public IRtssProcess? Start(string executable)=>real.Start(executable);
    }
    private static string? Trust(string path){using var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);return RtssTrust.Check(path,f.SafeFileHandle);}
    private static void RtssTrustCore(string root,string emptyExe)
    {
        Check(Trust(emptyExe)=="unsigned","sec-trust-empty-file-unsigned");
        // 經過合法簽署但不是 RTSS 發行者的程式（.NET 主機，Microsoft 簽署）：必須拒絕。
        string dotnet=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
        if(File.Exists(dotnet))Check(Trust(dotnet)=="publisher-mismatch","sec-trust-other-publisher-rejected");
        else Check(true,"sec-trust-other-publisher-rejected(skipped:no-dotnet.exe)");
        var real=RtssInstall.Find();
        if(real.ExecutablePath is string rtss)
        {
            Check(Trust(rtss) is null,"sec-trust-real-rtss-accepted");
            // SEC-02 後實際啟動的是最終路徑：真實安裝位置必須解析回同一路徑，且以最終路徑驗證仍通過。
            using(var h=new FileStream(rtss,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                string? final=RtssSystem.FinalPath(h.SafeFileHandle);
                Check(final is not null && string.Equals(final,rtss,StringComparison.OrdinalIgnoreCase) && RtssTrust.Check(final,h.SafeFileHandle) is null,"sec02-real-rtss-final-path-and-trust");
            }
            // 竄改副本：複製到測試資料夾後改動中段一個位元組，簽章雜湊應不符。
            string tampered=Path.Combine(root,"tampered","RTSS.exe");Directory.CreateDirectory(Path.GetDirectoryName(tampered)!);
            byte[] bytes=File.ReadAllBytes(rtss);bytes[bytes.Length/2]^=0xFF;File.WriteAllBytes(tampered,bytes);
            Check(Trust(tampered)=="signature-invalid","sec-trust-tampered-copy-rejected");
        }
        else Check(true,"sec-trust-real-rtss(skipped:not-installed)");
        // SEC-02：驗證後把路徑中的 junction 改指向別處。啟動目標必須是控制代碼的最終路徑（被驗證、被鎖住的檔案），
        // 而不是重新解析後的新檔案。junction 以 mklink /J 建立（僅測試使用，引數逐一傳入，不拼接字串）。
        {
            string jroot=Path.Combine(root,"junction-"+Guid.NewGuid().ToString("N")[..8]);
            string realExe=Path.Combine(jroot,"real","inner","RTSS.exe"),evilExe=Path.Combine(jroot,"evil","inner","RTSS.exe"),link=Path.Combine(jroot,"J");
            Directory.CreateDirectory(Path.GetDirectoryName(realExe)!);Directory.CreateDirectory(Path.GetDirectoryName(evilExe)!);
            File.WriteAllText(realExe,"verified");File.WriteAllText(evilExe,"swapped");
            bool Junction(string target)
            {
                var psi=new System.Diagnostics.ProcessStartInfo("cmd.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true};
                foreach(string a in new[]{"/d","/c","mklink","/J",link,target})psi.ArgumentList.Add(a);
                using var p=System.Diagnostics.Process.Start(psi)!;p.StandardOutput.ReadToEnd();p.WaitForExit();return p.ExitCode==0;
            }
            if(Junction(Path.Combine(jroot,"real")))
            {
                using var hold=new FileStream(Path.Combine(link,"inner","RTSS.exe"),FileMode.Open,FileAccess.Read,FileShare.Read);
                string? before=RtssSystem.FinalPath(hold.SafeFileHandle);
                Check(string.Equals(before,Path.GetFullPath(realExe),StringComparison.OrdinalIgnoreCase),"sec02-final-path-resolves-junction");
                Directory.Delete(link);bool swapped=Junction(Path.Combine(jroot,"evil"));
                string? after=RtssSystem.FinalPath(hold.SafeFileHandle);
                Check(swapped && File.ReadAllText(Path.Combine(link,"inner","RTSS.exe"))=="swapped","sec02-precondition-junction-swappable-while-held");
                Check(after is not null && string.Equals(after,before,StringComparison.OrdinalIgnoreCase) && File.ReadAllText(after)=="verified","sec02-launch-target-is-verified-file-after-swap");
                // 只移除 junction 本身（不跟隨連結刪除目標內容），避免在證據資料夾留下重新剖析點。
                Directory.Delete(link);
            }
            else Check(true,"sec02-junction(skipped:mklink-unavailable)");
        }
        // 實際啟動路徑：未簽署的 RTSS.exe 在 Process.Start 之前就被拒絕，狀態為啟動失敗。
        using var l=new RtssLauncher(new RealStartFake(emptyExe),TimeSpan.FromMilliseconds(300),TimeSpan.FromMilliseconds(20));
        Check(l.StartAsync(null).Result is {State:RtssRunState.StartFailed,Error:"unsigned"},"sec-launch-unsigned-not-started");
    }

    // GUI 整合（模擬 RTSS）：設定檔指定非預設路徑的 RTSS.exe、遊戲內時鐘與自動啟動皆開啟；
    // 假的 Start 會建立隔離共享記憶體，模擬 RTSS 啟動後提供 OSD，驗證介面與連線狀態依序更新。
    private static int RtssLaunchUi()
    {
        using var watchdog=Watchdog("rtss-launch-ui");
        string exe=FakeExe(Path.Combine(evidence,"rtss-launch-ui","Custom RTSS"));
        new SettingsStore(App.TestSettingsPath!).Save(new ClockSettings{GameClockEnabled=true,AutoStartRtss=true,RtssPath=exe});
        OsdFixture? fixture=null;byte[]? baseline=null;
        var fake=new FakeRtss(cfg=>RtssInstall.Find(cfg,()=>[],()=>[],File.Exists));
        fake.OnStart=_=>{fixture=new OsdFixture(App.TestRtssMap);baseline=fixture.Snapshot();fake.Running=true;return new FakeProcess();};
        App.TestRtssSystem=fake;
        App.TestReady=async app=>
        {
            try
            {
                var window=app.SettingsWindow;window.SelectedTab=3;await Task.Delay(200);
                Check(await UiWait(()=>app.RtssLaunch is {State:RtssRunState.Running,StartedByUs:true},4000) && fake.Starts==1 && fake.LastStarted==exe,"gui-launch-autostart-from-custom-path");
                Check(await UiWait(()=>app.Rtss.Status.Link==RtssLink.Connected && fixture?.Owned()==1,2000),"gui-launch-osd-connects-after-start");
                Check(await UiWait(()=>RtssValue(window,"RtssProcessLabel").Text==Strings.Get("RtssRunningOwned")
                    && RtssValue(window,"RtssLinkLabel").Text!.StartsWith(Strings.Get("LinkConnected")[..3],StringComparison.Ordinal)),"gui-launch-process-and-link-shown-separately");
                Check(RtssValue(window,"RtssInstallLabel").Text!.Contains(exe,StringComparison.Ordinal) && RtssValue(window,"RtssPathLabel").Text==exe,"gui-launch-shows-configured-path");
                var auto=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>global::Avalonia.Automation.AutomationProperties.GetName(x)==Strings.Get("RtssAutoStart"));
                var start=window.GetVisualDescendants().OfType<Button>().Single(x=>global::Avalonia.Automation.AutomationProperties.GetName(x)==Strings.Get("RtssStartNow"));
                Check(auto.IsChecked==true && !start.IsEnabled,"gui-launch-toggle-on-and-start-disabled-while-running");
                Capture(window,"rtss-launch-running.png");
                RtssValue(window,"RtssErrorLabel").BringIntoView();await Task.Delay(200);Capture(window,"rtss-launch-running-status.png");
                // 手動「立即啟動」在已執行時不會重複啟動。
                app.StartRtss();await Task.Delay(400);
                Check(fake.Starts==1,"gui-launch-manual-start-no-duplicate");
                auto.IsChecked=false;
                Check(await app.SaveAsync() && SettingsStore.Read(App.TestSettingsPath!) is {AutoStartRtss:false} saved && saved.RtssPath==exe,"gui-launch-toggle-persisted");
                // 路徑失效：改成不存在的檔案，介面提示失效、不啟動。
                fake.Running=false;
                app.ViewModel.RtssPath=Path.Combine(evidence,"rtss-launch-ui","gone","RTSS.exe");
                Check(await UiWait(()=>app.RtssInstallation?.ConfiguredPathInvalid==true && RtssValue(window,"RtssInstallLabel").Text!.Contains(app.ViewModel.RtssPath,StringComparison.Ordinal)),"gui-launch-stale-path-warning");
                Check(!start.IsEnabled && fake.Starts==1,"gui-launch-stale-path-start-disabled");
                RtssValue(window,"RtssInstallLabel").BringIntoView();await Task.Delay(200);Capture(window,"rtss-launch-stale-path.png");
                var useAuto=window.GetVisualDescendants().OfType<Button>().Single(x=>global::Avalonia.Automation.AutomationProperties.GetName(x)==Strings.Get("RtssUseAuto"));
                Check(useAuto.IsEnabled,"gui-launch-use-auto-enabled-with-custom-path");
                app.ViewModel.RtssPath="";
                Check(await UiWait(()=>RtssValue(window,"RtssPathLabel").Text==Strings.Get("RtssPathAuto") && !useAuto.IsEnabled),"gui-launch-use-auto-resets");
                foreach(string theme in new[]{"Light","Dark"}){app.ViewModel.Theme=theme;await Task.Delay(200);Capture(window,"rtss-launch-"+theme.ToLowerInvariant()+".png");}
            }
            catch(Exception ex){Check(false,"rtss-launch-ui-exception-"+ex);}
            finally{await app.QuitAsync();}
        };
        int exit=MainProgram.Main(["--ui-test"]);
        App.TestRtssSystem=null;
        Check(exit==0,"rtss-launch-ui-normal-exit");
        if(fixture is not null)
        {
            Check(fixture.Owned()==0 && ForeignUnchanged(baseline!,fixture.Snapshot()),"rtss-launch-ui-exit-clears-own-entry-only");
            fixture.Dispose();
        }
        WriteResults();return failed==0?0:1;
    }
}
