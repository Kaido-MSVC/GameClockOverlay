using System.Text;
using System.Text.Json;
using GameClockOverlay.AvaloniaApp;
using GameClockOverlay.AvaloniaApp.Models;
using GameClockOverlay.AvaloniaApp.Services;
using GameClockOverlay.AvaloniaApp.Services.Rtss;
using GameClockOverlay.AvaloniaApp.ViewModels;

namespace GameClockOverlay.Avalonia.Tests;

// Avalonia-2-POSITION：遊戲內時鐘位置。隔離映射佈局同 OsdFixture：第 0 槽 Primary、第 1 槽 Foreign、第 2、3 槽空、尾端 canary。
internal static partial class Program
{
    private static void PutOwner(OsdFixture f,int entry,string owner)
    {
        var bytes=new byte[OsdFixture.Stride];Encoding.ASCII.GetBytes(owner+"\0").CopyTo(bytes,256);
        Encoding.ASCII.GetBytes("foreign text\0").CopyTo(bytes,266752);
        f.View.WriteArray(OsdFixture.Offset+entry*OsdFixture.Stride,bytes,0,bytes.Length);
    }
    private static byte[] Entry(byte[] snapshot,int entry)=>snapshot.AsSpan(OsdFixture.Offset+entry*OsdFixture.Stride,OsdFixture.Stride).ToArray();
    private static bool EntryEmpty(OsdFixture f,int entry)=>Entry(f.Snapshot(),entry).All(b=>b==0);

    // GUI：位置選項與實際狀態一致；變更遊戲內位置不影響桌面時鐘；後方有其他程式時顯示退回原因。結束時保持啟用（右上）。
    private static async Task RtssPositionUi(GameClockOverlay.AvaloniaApp.App app)
    {
        var vm=app.ViewModel;var window=app.SettingsWindow;var f=uiFixture!;
        window.SelectedTab=3;await Task.Delay(150);
        var combo=global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<global::Avalonia.Controls.ComboBox>().Single(x=>global::Avalonia.Automation.AutomationProperties.GetName(x)==Strings.Get("GamePosition"));
        var label=RtssValue(window,"RtssPositionLabel");
        string Applied(string key)=>string.Format(Strings.Get("PosApplied"),Strings.Get(key));
        Check(vm.GameClockPosition=="TopRight" && combo.SelectedIndex==0 && combo.ItemCount==4,"gui-game-position-default-top-right");
        Check(await UiWait(()=>app.Rtss.Status is {Positioned:true,Entry:3} && label.Text==Applied("TopRight") && f.TextAt(3).StartsWith("<P2><L0>",StringComparison.Ordinal)),"gui-game-position-applied-matches-memory");
        Capture(window,"rtss-position-top-right.png");
        string desktopPosition=vm.Position;var desktopWindow=app.Clock.Position;
        combo.SelectedIndex=2;// BottomLeft
        Check(await UiWait(()=>f.TextAt(3).StartsWith("<P6><L0>",StringComparison.Ordinal) && label.Text==Applied("BottomLeft")),"gui-game-position-change-updates-osd");
        Check(vm.Position==desktopPosition && app.Clock.Position==desktopWindow,"gui-game-position-does-not-move-desktop-clock");
        combo.SelectedIndex=3;// Rtss
        Check(await UiWait(()=>app.Rtss.Status is {Positioned:false,Placement:OsdPlacement.Rtss} && label.Text==Strings.Get("PosRtss") && f.TextAt(app.Rtss.Status.Entry)==app.Rtss.Status.Text),"gui-game-position-rtss-plain-text");
        // 後方槽有其他程式：停用後讓外部程式占用最後的槽，再以右上啟用。
        vm.GameClockEnabled=false;
        Check(await UiWait(()=>f.Owned()==0),"gui-game-position-disable-clears");
        PutOwner(f,3,"LaterApp");var later=Entry(f.Snapshot(),3);
        combo.SelectedIndex=0;vm.GameClockEnabled=true;
        Check(await UiWait(()=>app.Rtss.Status is {Link:RtssLink.Connected,Positioned:false,Entry:2} && label.Text==string.Format(Strings.Get("PosFallback"),Strings.Get("TopRight"))) && Entry(f.Snapshot(),3).SequenceEqual(later),"gui-game-position-fallback-shown");
        Capture(window,"rtss-position-fallback.png");
        f.View.WriteArray(OsdFixture.Offset+3*OsdFixture.Stride,new byte[OsdFixture.Stride],0,OsdFixture.Stride);
        Check(await UiWait(()=>app.Rtss.Status is {Positioned:true,Entry:3} && label.Text==Applied("TopRight") && f.Owned()==1),"gui-game-position-restored-after-other-exits");
        Check(await app.SaveAsync() && SettingsStore.Read(App.TestSettingsPath!).GameClockPosition=="TopRight","gui-game-position-saved");
    }

    private static void RtssPositionCore()
    {
        using(var f=new OsdFixture())
        {
            var baseline=f.Snapshot();
            // Rtss（預設參數）：與 Avalonia-2 完全相同，第一個空槽、不加標籤。
            Check(SharedOsd.Access(f.Name,"12:34:56") is {State:"updated",Entry:2,Text:"12:34:56",Positioned:false} && f.TextAt(2)=="12:34:56","pos-rtss-default-unchanged");
            Check(SharedOsd.Access(f.Name,clear:true).State=="cleared" && ForeignUnchanged(baseline,f.Snapshot()),"pos-rtss-clear");

            // 各位置使用官方 sticky 標籤，並配置到所有非空槽之後的最高空槽（第 3 槽）。
            foreach(var (place,tag) in new[]{(OsdPlacement.TopRight,"<P2><L0>"),(OsdPlacement.BottomLeft,"<P6><L0>"),(OsdPlacement.BottomRight,"<P8><L0>")})
            {
                f.Reset();baseline=f.Snapshot();
                var r=SharedOsd.Access(f.Name,"12:34:56",placement:place);
                Check(r is {State:"updated",Entry:3,Text:"12:34:56",Positioned:true} && f.TextAt(3)==tag+"12:34:56" && f.Owned()==1 && EntryEmpty(f,2) && ForeignUnchanged(baseline,f.Snapshot()),"pos-"+place+"-tag-in-last-slot");
            }
            Check(SharedOsd.Inspect(f.Name) is {Owned:1,FirstEntry:3,Text:"12:34:56",Tag:"<P8><L0>"},"pos-inspect-separates-tag");
            Check(SharedOsd.Access(f.Name,clear:true) is {State:"cleared",Entry:3} && f.Owned()==0 && ForeignUnchanged(baseline,f.Snapshot()),"pos-clear-tagged-entry");

            // 由 Rtss 切換到右上：同一次持鎖內搬到最後的槽，舊槽完全清除，只留 1 個自有槽。
            f.Reset();baseline=f.Snapshot();
            SharedOsd.Access(f.Name,"12:34:56");
            Check(SharedOsd.Access(f.Name,"12:34:57",placement:OsdPlacement.TopRight) is {Entry:3,Positioned:true} && EntryEmpty(f,2) && f.Owned()==1 && ForeignUnchanged(baseline,f.Snapshot()),"pos-relocates-to-last-slot");
            // 切回 Rtss：沿用自己的槽，移除標籤。
            Check(SharedOsd.Access(f.Name,"12:34:58") is {Entry:3,Positioned:false} && f.TextAt(3)=="12:34:58","pos-back-to-rtss-removes-tag");

            // 最後的槽有其他程式：不加標籤（避免位置延續到它的文字），外部槽逐位元組不變。
            f.Reset();PutOwner(f,3,"LaterApp");baseline=f.Snapshot();var later=Entry(baseline,3);
            Check(SharedOsd.Access(f.Name,"12:34:56",placement:OsdPlacement.TopRight) is {State:"updated",Entry:2,Positioned:false} && f.TextAt(2)=="12:34:56"
                && Entry(f.Snapshot(),3).SequenceEqual(later) && ForeignUnchanged(baseline,f.Snapshot()),"pos-fallback-when-foreign-after");
            Check(SharedOsd.Access(f.Name,null,placement:OsdPlacement.TopRight) is {State:"connected",Positioned:false,Stale:false},"pos-probe-fallback-not-stale-while-blocked");
            // 對方離開：唯讀驗證回報需要重寫，重寫後搬到第 3 槽並套用。
            f.View.WriteArray(OsdFixture.Offset+3*OsdFixture.Stride,new byte[OsdFixture.Stride],0,OsdFixture.Stride);
            var before=f.Snapshot();
            Check(SharedOsd.Access(f.Name,null,placement:OsdPlacement.TopRight) is {State:"connected",Stale:true} && before.SequenceEqual(f.Snapshot()),"pos-probe-stale-when-unblocked-read-only");
            Check(SharedOsd.Access(f.Name,"12:34:57",placement:OsdPlacement.TopRight) is {Entry:3,Positioned:true} && EntryEmpty(f,2) && f.Owned()==1,"pos-upgrades-after-unblocked");

            // 已定位但後方出現其他程式（例如對方使用最後的槽）：唯讀驗證回報需要重寫，重寫後退回不加標籤。
            f.Reset();SharedOsd.Access(f.Name,"<P2><L0>12:00");PutOwner(f,3,"LaterApp");later=Entry(f.Snapshot(),3);
            Check(SharedOsd.Access(f.Name,null,placement:OsdPlacement.TopRight) is {Positioned:true,Stale:true},"pos-probe-stale-when-foreign-appears-after");
            Check(SharedOsd.Access(f.Name,"12:01",placement:OsdPlacement.TopRight) is {Entry:2,Positioned:false} && f.TextAt(2)=="12:01" && Entry(f.Snapshot(),3).SequenceEqual(later),"pos-tagged-falls-back-when-foreign-after");

            // 自己已在最後、前面才出現新程式：保持定位，不搬移。
            f.Reset();SharedOsd.Access(f.Name,"12:00",placement:OsdPlacement.TopRight);PutOwner(f,2,"EarlierApp");var earlier=Entry(f.Snapshot(),2);
            Check(SharedOsd.Access(f.Name,null,placement:OsdPlacement.TopRight) is {Positioned:true,Stale:false},"pos-probe-not-stale-when-foreign-before");
            Check(SharedOsd.Access(f.Name,"12:01",placement:OsdPlacement.TopRight) is {Entry:3,Positioned:true} && Entry(f.Snapshot(),2).SequenceEqual(earlier),"pos-keeps-last-slot-foreign-before");

            // 槽全滿：不覆寫任何外部槽。
            f.Reset();PutOwner(f,2,"A");PutOwner(f,3,"B");baseline=f.Snapshot();
            Check(SharedOsd.Access(f.Name,"12:00",placement:OsdPlacement.TopRight).State=="no-free-entry" && baseline.SequenceEqual(f.Snapshot()),"pos-full-slots-not-overwritten");
            // Rtss 的狀態查詢：自己的槽還在時不回報 Stale，也不改變任何位元組（找不到自己的槽見 gco01-*）。
            f.Reset();SharedOsd.Access(f.Name,"12:00");baseline=f.Snapshot();
            Check(SharedOsd.Access(f.Name) is {State:"connected",Stale:false} && baseline.SequenceEqual(f.Snapshot()),"pos-rtss-probe-unchanged");
        }

        // 後端：位置設定變更立即重寫；分鐘模式在後方程式離開後 3 秒內（測試 200 ms）就改為定位。
        using(var f=new OsdFixture())
        {
            var baseline=f.Snapshot();
            using var svc=Service(f.Name);
            svc.Configure(true,true,false,OsdPlacement.TopRight);
            Check(WaitUntil(()=>svc.Status is {Link:RtssLink.Connected,Entry:3,Positioned:true,Placement:OsdPlacement.TopRight} && f.TextAt(3)=="<P2><L0>"+svc.Status.Text,1500),"pos-service-top-right");
            svc.Configure(true,true,false,OsdPlacement.BottomLeft);
            Check(WaitUntil(()=>f.TextAt(3).StartsWith("<P6><L0>",StringComparison.Ordinal),600),"pos-service-change-applies-immediately");
            svc.Configure(true,true,false,OsdPlacement.Rtss);
            Check(WaitUntil(()=>svc.Status is {Positioned:false,Placement:OsdPlacement.Rtss} && f.TextAt(3)==svc.Status.Text,600) && f.Owned()==1,"pos-service-back-to-rtss");
            svc.Configure(false,true,false,OsdPlacement.Rtss);
            Check(WaitUntil(()=>f.Owned()==0,1500) && ForeignUnchanged(baseline,f.Snapshot()),"pos-service-disable-clears");

            PutOwner(f,3,"LaterApp");var later=Entry(f.Snapshot(),3);
            svc.Configure(true,false,false,OsdPlacement.TopRight);
            Check(WaitUntil(()=>svc.Status is {Link:RtssLink.Connected,Entry:2,Positioned:false},1500) && Entry(f.Snapshot(),3).SequenceEqual(later),"pos-service-fallback-reported");
            long writes=svc.WriteCount;
            f.View.WriteArray(OsdFixture.Offset+3*OsdFixture.Stride,new byte[OsdFixture.Stride],0,OsdFixture.Stride);
            Check(WaitUntil(()=>svc.Status is {Entry:3,Positioned:true} && f.Owned()==1,1500) && (svc.WriteCount==writes+1 || DateTime.Now.Second<3),"pos-service-minute-mode-upgrades-once");
            writes=svc.WriteCount;Thread.Sleep(800);
            Check(svc.WriteCount==writes || DateTime.Now.Second<2,"pos-service-minute-mode-no-extra-writes");
            Check(svc.Shutdown() && f.Owned()==0 && ForeignUnchanged(baseline,f.Snapshot()),"pos-service-shutdown-clears");
        }

        // 設定：全新預設右上；舊設定檔缺少欄位時保留 RTSS 位置；保存讀回；無效值回到 Rtss；與桌面位置互相獨立。
        string dir=Path.Combine(evidence,"rtss-position-settings");Directory.CreateDirectory(dir);
        Check(new ClockSettings().GameClockPosition=="TopRight" && new SettingsStore(Path.Combine(dir,"missing.json")).Load().GameClockPosition=="TopRight","settings-new-default-game-position-top-right");
        string old=Path.Combine(dir,"avalonia2.json");
        var oldJson=JsonSerializer.SerializeToNode(new ClockSettings{GameClockEnabled=true,Position="BottomLeft"})!.AsObject();oldJson.Remove("GameClockPosition");
        File.WriteAllText(old,oldJson.ToJsonString());
        Check(SettingsStore.Read(old) is {GameClockPosition:"Rtss",GameClockEnabled:true,Position:"BottomLeft"},"settings-existing-file-keeps-rtss-position");
        var store=new SettingsStore(Path.Combine(dir,"roundtrip.json"));
        Check(store.Save(new ClockSettings{GameClockPosition="BottomLeft"}) && new SettingsStore(store.Path).Load().GameClockPosition=="BottomLeft","settings-game-position-roundtrip");
        File.WriteAllText(old,"{\"GameClockPosition\":\"Center\"}");
        Check(SettingsStore.Read(old).GameClockPosition=="Rtss","settings-invalid-game-position-normalized");
        File.WriteAllText(old,"{\"GameClockPosition\":\"TopLeft\"}");
        Check(SettingsStore.Read(old).GameClockPosition=="Rtss","settings-top-left-maps-to-rtss-default");
        var vm=new SettingsViewModel(new ClockSettings{Position="TopLeft"});
        vm.GameClockPosition="BottomRight";bool desktopKept=vm.Position=="TopLeft";vm.Position="Free";
        Check(desktopKept && vm.GameClockPosition=="BottomRight","settings-game-and-desktop-position-independent");
    }
}
