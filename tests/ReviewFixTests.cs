using System.Diagnostics;
using System.Text;
using GameClockOverlay.AvaloniaApp.Services.Rtss;
using MainProgram=GameClockOverlay.AvaloniaApp.Program;

namespace GameClockOverlay.Avalonia.Tests;

// CODEX-AVALONIA-2-REVIEW 的修正回歸：GCO-01（快速重啟漏看）、GCO-02（鎖外認領競態）、GCO-03（--ui-test 實體隔離）。
// 全部使用隨機名稱的隔離映射，不觸碰正式 RTSS，也不讀寫使用者設定。
internal static partial class Program
{
    private static byte[] ForeignEntry(string owner)
    {
        var bytes=new byte[OsdFixture.Stride];Encoding.ASCII.GetBytes(owner+"\0").CopyTo(bytes,256);
        Encoding.ASCII.GetBytes("racer text\0").CopyTo(bytes,266752);return bytes;
    }
    // 模擬 SDK 範例：在 dwBusy 鎖之外把 Owner 寫入「剛才看到是空的」槽。只觸發一次。
    private static void RaceOnce(OsdFixture f,string owner)
    {
        int fired=0;
        SharedOsd.TestBeforeClaim=slot=>{if(Interlocked.Exchange(ref fired,1)==0)f.View.WriteArray(OsdFixture.Offset+slot*OsdFixture.Stride,ForeignEntry(owner),0,OsdFixture.Stride);};
    }

    private static void ReviewFixCore()
    {
        // GCO-01：RTSS 在兩次唯讀探測之間快速重啟（同名映射重建、看不到 not-running），所有位置都必須回報需要重寫。
        using(var f=new OsdFixture())
        {
            SharedOsd.Access(f.Name,"12:00");
            Check(SharedOsd.Access(f.Name) is {State:"connected",Stale:false},"gco01-rtss-probe-own-present-not-stale");
            f.Reset();
            Check(SharedOsd.Access(f.Name) is {State:"connected",Stale:true},"gco01-rtss-probe-lost-entry-stale");
            f.Reset();PutOwner(f,3,"LaterApp");
            Check(SharedOsd.Access(f.Name,placement:OsdPlacement.TopRight) is {State:"connected",Stale:true},"gco01-top-right-probe-lost-entry-highest-foreign-stale");
            f.Reset();
            Check(SharedOsd.Access(f.Name,placement:OsdPlacement.TopRight) is {Stale:true},"gco01-top-right-probe-lost-entry-empty-map-stale");
        }
        using(var f=new OsdFixture())
        {
            // 注入從分鐘第 5 秒起算的時間（隨實際經過時間前進），本區段在數秒內完成，
            // 不會跨過分鐘邊界：通過只能來自「失去自有槽→立即重寫」，不會由正常的分鐘更新湊巧滿足（RECHECK P3）。
            var clock=Stopwatch.StartNew();var origin=new DateTime(2026,11,16,12,30,5);
            using var svc=Service(f.Name,()=>origin+clock.Elapsed);
            // 分鐘模式：兩次寫入相隔最長 60 秒，以前只會在下一分鐘邊界才發現。
            svc.Configure(true,false,false);
            Check(WaitUntil(()=>svc.Status.Link==RtssLink.Connected && f.Owned()==1,1500),"gco01-minute-mode-connected");
            var before=svc.Status.LastWrite;
            f.Reset();
            Check(WaitUntil(()=>f.Owned()==1 && svc.Status.LastWrite>before && svc.Status.Link==RtssLink.Connected,1500),"gco01-minute-mode-rtss-restart-rewrites-promptly");
            svc.Configure(true,false,false,OsdPlacement.TopRight);
            Check(WaitUntil(()=>svc.Status is {Positioned:true,Entry:3},1500),"gco01-minute-mode-top-right-connected");
            f.Reset();PutOwner(f,3,"LaterApp");var later=Entry(f.Snapshot(),3);
            Check(WaitUntil(()=>svc.Status is {Link:RtssLink.Connected,Positioned:false,Entry:2} && f.Owned()==1,1500) && Entry(f.Snapshot(),3).SequenceEqual(later),"gco01-restart-with-foreign-highest-falls-back-promptly");
            // 新映射槽已全滿：不得保留過期的「已連線」，改為真實錯誤。
            f.Reset();PutOwner(f,2,"A");PutOwner(f,3,"B");var full=f.Snapshot();
            Check(WaitUntil(()=>svc.Status is {Link:RtssLink.Error,Code:"no-free-entry"},1500) && full.AsSpan(OsdFixture.Offset).SequenceEqual(f.Snapshot().AsSpan(OsdFixture.Offset)),"gco01-restart-full-map-revokes-connected");
            Check((origin+clock.Elapsed).Minute==30,"gco01-injected-clock-stayed-within-minute");
            Check(svc.Shutdown(),"gco01-service-shutdown");
        }

        // GCO-02：掃描後、認領前被其他程式在鎖外認領：必須放棄，不得清除對方。
        using(var f=new OsdFixture())
        {
            try
            {
                var baseline=f.Snapshot();
                RaceOnce(f,"Racer");
                var r=SharedOsd.Access(f.Name,"12:00");
                Check(r.State=="claim-conflict" && Entry(f.Snapshot(),2).SequenceEqual(ForeignEntry("Racer")) && f.Owned()==0 && ForeignUnchanged(baseline,f.Snapshot()),"gco02-rtss-claim-race-keeps-foreign");
                SharedOsd.TestBeforeClaim=null;
                Check(SharedOsd.Access(f.Name,"12:01") is {State:"updated",Entry:3} && Entry(f.Snapshot(),2).SequenceEqual(ForeignEntry("Racer")),"gco02-retry-uses-next-free-slot");

                // 搬移到最高槽時被搶：舊槽保留（仍是自己的、文字不變），新槽屬於對方。
                f.Reset();SharedOsd.Access(f.Name,"12:02");var own=Entry(f.Snapshot(),2);
                RaceOnce(f,"Racer");
                Check(SharedOsd.Access(f.Name,"12:03",placement:OsdPlacement.TopRight).State=="claim-conflict" && Entry(f.Snapshot(),2).SequenceEqual(own) && Entry(f.Snapshot(),3).SequenceEqual(ForeignEntry("Racer")) && f.Owned()==1,"gco02-relocate-race-keeps-old-slot-and-foreign");
                SharedOsd.TestBeforeClaim=null;
                Check(SharedOsd.Access(f.Name,"12:04",placement:OsdPlacement.TopRight) is {State:"updated",Entry:2,Positioned:false} && Entry(f.Snapshot(),3).SequenceEqual(ForeignEntry("Racer")),"gco02-after-race-falls-back-without-tag");
            }
            finally{SharedOsd.TestBeforeClaim=null;}
        }
        using(var f=new OsdFixture())
        {
            using var svc=Service(f.Name);
            try
            {
                RaceOnce(f,"Racer");
                svc.Configure(true,true,false,OsdPlacement.TopRight);
                Check(WaitUntil(()=>svc.Status.LastError=="claim-conflict",1500),"gco02-service-reports-conflict");
                Check(WaitUntil(()=>svc.Status is {Link:RtssLink.Connected,Entry:2,Positioned:false},1500) && Entry(f.Snapshot(),3).SequenceEqual(ForeignEntry("Racer")) && f.Owned()==1,"gco02-service-recovers-without-touching-foreign");
            }
            finally{SharedOsd.TestBeforeClaim=null;}
            Check(svc.Shutdown() && f.Owned()==0 && Entry(f.Snapshot(),3).SequenceEqual(ForeignEntry("Racer")),"gco02-service-exit-keeps-foreign");
        }

        // GCO-04：實際輸出的每個第三方 DLL 都要出現在 THIRD-PARTY-NOTICES；元件授權檔不得留有範本占位符。
        string dir=AppContext.BaseDirectory,notices=File.ReadAllText(Path.Combine(dir,"THIRD-PARTY-NOTICES.md"));
        var uncovered=Directory.GetFiles(dir,"*.dll").Select(Path.GetFileName).OfType<string>()
            .Where(n=>!n.StartsWith("GameClockOverlay",StringComparison.Ordinal))
            .Where(n=>n.StartsWith("Avalonia.",StringComparison.Ordinal)&&n!="Avalonia.dll"?!notices.Contains(n["Avalonia.".Length..^4],StringComparison.Ordinal):n!="Avalonia.dll"&&!notices.Contains(n,StringComparison.Ordinal)).ToArray();
        File.WriteAllLines(Path.Combine(evidence,"notices-uncovered-dlls.txt"),uncovered);
        Check(uncovered.Length==0,"gco04-notices-cover-shipped-dlls");
        var licenseFiles=Directory.GetFiles(Path.Combine(dir,"licenses"));
        Check(licenseFiles.Where(f=>Path.GetFileName(f).Contains("-LICENSE",StringComparison.Ordinal)).All(f=>!System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(f),"<year>|<owner>|<copyright holders>"))
            && !licenseFiles.Any(f=>Path.GetFileName(f) is "MIT.txt" or "BSD-3-Clause.txt"),"gco04-no-license-placeholders");
        foreach(string need in new[]{"Avalonia-12.1.6-LICENSE.txt","MicroCom.Runtime-0.11.6-LICENSE.txt","Tmds.DBus.Protocol-0.94.1-LICENSE.txt"})
            Check(File.Exists(Path.Combine(dir,"licenses",need)),"gco04-license-shipped-"+need);

        // GCO-03：命令列直接帶 --ui-test 啟動正式產品，不得另開共用真實設定與正式 RTSS Owner 的實體。
        bool table=MainProgram.Decide(true,true,false)==MainProgram.Startup.Refuse && MainProgram.Decide(true,true,true)==MainProgram.Startup.Run
            && MainProgram.Decide(true,false,false)==MainProgram.Startup.Run && MainProgram.Decide(false,true,false)==MainProgram.Startup.Activate && MainProgram.Decide(false,false,false)==MainProgram.Startup.Activate;
        Check(table,"gco03-startup-decision-table");
        // 判斷表正確才實際啟動子程序：若修正失效，子程序可能以使用者的真實設定啟動，所以不冒險。
        if(table)
        {
            using var child=Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"GameClockOverlay.Avalonia.exe"),"--ui-test"){UseShellExecute=false,CreateNoWindow=true})!;
            bool exited=child.WaitForExit(15000);
            if(!exited)child.Kill(true);
            Check(exited && child.ExitCode==MainProgram.RefusedExit,"gco03-cli-ui-test-refused-without-runner");
        }
    }
}
