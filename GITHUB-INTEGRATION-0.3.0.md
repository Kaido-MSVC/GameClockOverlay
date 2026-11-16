# GameClockOverlay v0.3.0：本機 Git 整合與 RC2 驗證

日期：2026-11-16。**已安全整合至原倉庫的本機開發分支，完成提交、公開還原驗證、RC2 封裝及合理回歸，具備進入正式發布前最終檢查的條件。尚未推送、建立新 Tag 或發布 Release。**

## 1. 整合前狀態

- 原工作目錄：工作區下的 `GameClockOverlay/`；分支 `main`，工作樹乾淨。
- `main`／`origin/main`：`76a7a1f33bd877e3d1b457b8d8e1e63920a0be57`，與遠端實際查詢一致。
- v0.2.0 annotated Tag 物件：`56fa351a90ed93e958a9530289a8dd51c374f625`，指向上述提交。
- 既有 Tag：v0.1.0、v0.2.0。未改寫、移動或新增 Tag。
- 既有署名為 `Kaido-MSVC <windfirge@gmail.com>`；本輪沿用目前 Git 設定，未改全域使用者設定。
- 已閱讀候選封裝紀錄、Codex 初審／複查、Avalonia-2／定位文件、README、CHANGELOG、Release Notes、封裝腳本，並核對實際來源。

## 2. 分支與工作樹

從原 `main` 建立 `feature/avalonia-rtss`，使用同一 Git 倉庫的獨立 worktree：`../GameClockOverlay-v0.3.0-integration/`。原 `GameClockOverlay/` 保持在 main，原有檔案不受整合操作影響。

新分支提交保留全部舊歷史，原 main 提交仍是祖先。沒有使用 reset、rebase、force push、遠端分支建立或 GitHub 寫入操作。

## 3. 新舊結構與來源範圍

新版主程式直接使用 `src/`，必要測試使用 `tests/`，由 `GameClockOverlay.slnx` 統一建置。WPF 原始碼由完整 Git 歷史及 v0.2.0 Tag 保留，不永久同步維護兩套 GUI。

```text
src/
  GameClockOverlay.csproj、App.cs、Program.cs、AssemblyInfo.cs
  Models/ClockSettings.cs
  Services/ClockMath.cs、NativeWindow.cs、SettingsStore.cs、SingleInstance.cs
  Services/Strings.cs、UiStrings.cs
  Services/Rtss/SharedOsd.cs、RtssClockService.cs、RtssInstall.cs
  ViewModels/SettingsViewModel.cs
  Views/ClockWindow.cs、SettingsWindow.cs
  app.manifest、clock.ico、packages.lock.json
tests/
  GameClockOverlay.Tests.csproj、Program.cs、ReleaseTests.cs
  ReviewFixTests.cs、RtssTests.cs、RtssPositionTests.cs、packages.lock.json
scripts/
  package-rc.ps1、test.ps1、verify-package.ps1
licenses/                         25 份條文／通知
docs/legacy/v0.2.0/
  MANUAL-ACCEPTANCE.md、UPGRADE-REPORT.md、THIRD-PARTY-NOTICES.txt
```

另外追蹤必要的 README、CHANGELOG、Release Notes、PRODUCT、DESIGN、自有 LICENSE、第三方 notices、NuGet.Config、Directory.Build.props、global.json、Git／繁中檢查配置。本機 Codex／Claude 報告、截圖與 evidence 不複製公開；本文件只保存整合摘要。

整合來源清單可由 `git ls-tree -r --name-only ff25eb3` 重現；RC2 來源 ZIP 與對應清單有 72 個檔案。本報告是封裝完成後獨立新增的文件提交，未塞回已驗證的 RC2 ZIP。

大量替換前已在本機保存原追蹤清單、雜湊及替換範圍；另保留整合 patch、改名／公開建置 patch 與最終差異清單，不提交這些本機資料。差異可直接檢查：

```powershell
git diff --stat 76a7a1f ff25eb3
git diff --name-status 76a7a1f ff25eb3
git show --stat fde2412
git show --stat ff25eb3
```

## 4. 歷史與既有資產保護

- 原 WPF 工作樹所有追蹤檔案前後 SHA-256 一致，main 仍乾淨且提交未變。
- v0.1.0／v0.2.0 Tag 保持原物件；遠端 main 與 v0.2.0 Tag 再查詢仍一致。
- [原 v0.2.0 Release](https://github.com/Kaido-MSVC/GameClockOverlay/releases/tag/v0.2.0) 仍是公開正式版；4 個附件的 ID、名稱、大小與 GitHub digest 前後一致，沒有刪除或替換。
- 原 Avalonia 候選來源與 RC1 保留不變，未修改 RTSS 原型或 DirectX Hook 實驗。
- RC1：11,740,213 位元組；ZIP SHA-256 `aee394421c0848026800e47e1e194593dec34b6cc9aa1c8af4daa9d425959c74`；其中 EXE SHA-256 `355becf688dae7cd4987eff397740f4e5baeab626f0213a0c42905246f0d3c5e`，均重新讀取確認。

## 5. 執行檔名稱與相容性決策

新版正式名稱為 **GameClockOverlay.exe**，不是單純重新命名磁碟檔案：

- 專案改為 `src/GameClockOverlay.csproj`，AssemblyName／ApplicationName 明確為 GameClockOverlay，manifest identity 同步。
- 產品 DLL、deps、runtimeconfig 一起改名；兩處 avares 圖示 URI 改用新 Assembly 名稱。
- 測試專案／Assembly 為 GameClockOverlay.Tests，InternalsVisibleTo、子程序啟動、版本測試與腳本同步更新。
- C# namespace 保留 GameClockOverlay.AvaloniaApp，避免為名稱變更大幅搬動已驗證程式碼。
- Mutex／IPC **保留既有 Avalonia 識別**：`Local\GameClockOverlay.Avalonia1.Singleton`／`.Activate`，測試仍有 .UiTest 字尾。
- 設定仍是 `%LOCALAPPDATA%\GameClockOverlay-Avalonia\settings.json`；舊 WPF 路徑只讀匯入，不寫回。
- RTSS Owner、Ctrl + Alt + Shift + F10、系統匣與圖示內容不變。WPF 與新版同時執行仍可能競爭同一快捷鍵，後啟動者會停用穿透。

SharedOsd、RtssClockService、RtssInstall、SettingsStore、SingleInstance、ClockWindow、ClockSettings 與 RC1 來源逐位元組相同。本輪沒有重構 RTSS 安全退讓、位置排序或生命週期邏輯。

## 6. 公開 NuGet 還原

`NuGet.Config` 只含官方 `https://api.nuget.org/v3/index.json`，不含本機路徑、私人來源或憑證；package source mapping 的所有套件僅映射到 nuget.org。NuGetAudit 啟用，模式 all。

Directory.Build.props 啟用 RestorePackagesWithLockFile，兩個專案各提交 packages.lock.json。公開還原使用：

```powershell
dotnet restore GameClockOverlay.slnx --locked-mode
dotnet build GameClockOverlay.slnx -c Release --no-restore
```

本輪首次還原指定全新、空的獨立 packages 目錄並使用 `--no-http-cache --force-evaluate`；成功後再以 `--locked-mode --no-http-cache` 驗證。全部 25 個套件實際存在獨立目錄，其 .nupkg.metadata 來源均為官方 nuget.org；沒有依賴個人全域快取。assets 另列出 VS 共用 fallback 位置，但 25 個實際套件皆已位於本次獨立快取。

25 個套件 ID／版本與 RC1 完全一致，沒有升級套件。`dotnet package list --project GameClockOverlay.slnx --include-transitive --vulnerable --no-restore --format json` 結束碼 0，兩專案沒有列出受影響版本；不等於未揭露或所有內含原生程式碼漏洞均已排除。

文件依據：[NuGet lock files](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)，經 Context7 查閱；封裝讀 ZIP 的 stream／Dispose 用法亦核對 [.NET ZIP 文件](https://github.com/dotnet/docs/blob/main/docs/standard/io/zip-tar-best-practices.md)，並以實際封裝驗證。

## 7. Git 忽略與版本一致性

.gitignore 排除 `**/bin/`、`**/obj/`、dist、.vs、artifacts、evidence、TestResults、.integration、暫存／log、.env、憑證及本機 settings 檔。實際以 check-ignore 確認排除生效，LICENSE、notices、來源、測試、lock files、NuGet.Config 及封裝腳本均未被誤排除。追蹤清單沒有建置產物、測試證據、RTSS／Hook binary 或私人設定。

程式／資訊版本為 0.3.0，Assembly／檔案版本為 0.3.0.0；關於頁動態讀 Assembly 資訊版本。README、CHANGELOG、Release Notes 與封裝命名同步，RC2 字尾只屬候選包名稱。舊 WPF CHANGELOG 歷史保留。

## 8. 建置與回歸（本輪實際執行）

SDK 11.0.100、C# 15、Avalonia 12.1.6、net11.0-windows、PlatformTarget x64／RID win-x64，Windows 11 x64。

| 項目 | Debug | Release |
| --- | --- | --- |
| solution 完整建置（--no-incremental） | 0 警告、0 錯誤 | 0 警告、0 錯誤 |
| UI／核心／RTSS 隔離／設定／版本 | 264 PASS、0 FAIL | 264 PASS、0 FAIL |
| hotkey-conflict | 40／0 | 40／0 |
| quit-fault | 35／0 | 35／0 |
| session-end | 37／0 | 37／0 |
| review | 40／0 | 40／0 |

這是案例執行數，含重複覆蓋，不相加宣稱獨立功能數。預設 UI 模式保留 native Shared Memory fixture、canary／foreign 逐位元組保護、啟停切換、RTSS 缺席／重連、GCO-01 快速重啟、GCO-02 決定性交錯、設定讀寫、GCO-03 保護、圖示、版本與新 Assembly 名稱檢查。

測試只使用隨機隔離映射與設定。沒有執行 --rtss-live／--rtss-live-ui，沒有操作現存 RTSS 或遊戲。session-end 使用 TryShutdown 驗證生命週期，不冒充真正 Windows 登出。

## 9. RC2 封裝、解壓與執行

由乾淨的來源提交 `ff25eb3d60db6643a7e7874ba426ea4e390261a5` 執行 package-rc.ps1。腳本先完整 Release 建置，再以 `--no-build`、framework-dependent win-x64 發布，拒絕覆寫既有輸出；移除本次產生的原生 PDB。沒有修改或刪除 RC1。

以下路徑相對於本開發分支工作樹：

| 產物 | 路徑 | 大小（位元組） |
| --- | --- | ---: |
| Runtime ZIP | `dist/packages/GameClockOverlay-v0.3.0-rc2-win-x64.zip` | 11,741,910 |
| Source ZIP | `dist/packages/GameClockOverlay-v0.3.0-rc2-source.zip` | 407,595 |
| ZIP 雜湊 | `dist/packages/SHA256SUMS-v0.3.0-rc2.txt` | 213 |
| 來源雜湊 | `dist/packages/SOURCE-SHA256SUMS-v0.3.0-rc2.txt` | 7,376 |

```text
Runtime ZIP SHA-256
0632b6ed49471f6ee3e84cc580fde370d167d6c55dc4f9bd61458636207d8e6b
GameClockOverlay.exe SHA-256
0f9a71c076b756e44ccfe3766cfa9d9a843080106e6bc4db5042e95c6ebcaa0d
Source ZIP SHA-256
05748bafa63e639ad29521857d3ea8c2fddeed16d1c0b54e01ba69c08ab23bb0
```

獨立解壓後：65 個檔案與 publish 逐檔一致，64 條內部 SHA-256 全部吻合，無額外檔案。版本／Assembly／runtimeconfig／x64 apphost 與授權檔檢查通過；不含 Runtime、RTSS、MSI Afterburner、測試、evidence、Hook／SDK、腳本或 PDB。執行時仍需 .NET 11 Runtime x64。

直接執行解壓後 EXE 的 --ui-test 保護分支，等待程序結束並確認 exit 2；不啟動未隔離的 GUI、不讀寫真實設定。另在獨立測試組合目錄放入解壓的完整產品 payload 及 4 個 runner 檔，以**封裝內產品 DLL**完成 264／264 回歸：GUI 啟動、圖示、秒／分鐘、設定、第二程序、正常退出與隔離 OSD 清理皆通過。這些 runner 檔只在本機測試目錄，未加入 Runtime ZIP。

一般不帶測試掛鉤的 GUI 直接啟動，沒有為本輪再使用真實設定或正式 RTSS 操作；上述完整 GUI 驗證使用既有隔離測試掛鉤。兩份真實設定檔前後雜湊不變。

## 10. 來源與授權對照

來源 ZIP 由 git archive 指定上述 commit 產生，72 個檔案與提交清單／來源 SHA-256 清單一致。獨立檢查 Git blob 時，9 個文字檔只有 Git attributes 的 LF／CRLF 轉換，其餘逐位元組相同；內部來源清單以 ZIP 原始位元組計算，因此不混用工作樹與 Git blob 雜湊。

54 個共用第三方 DLL／授權檔與 RC1 ZIP 逐位元組一致。每個第三方 DLL 對應 Avalonia、MicroCom、Tmds、Skia／HarfBuzz、ANGLE 的 notices；三份補齊的 MIT 著作權聲明與原生通知保留。自有 LICENSE 及 .NET apphost 通知隨包提供，未宣稱 bundle .NET Runtime。

處理前已讀授權庫規則；沒有重新下載條文，也沒有修改本機範本。RTSS 是外部依賴，不包含 SDK binary、不宣稱可重新散布 RTSS。

## 11. 本機提交與署名

| 提交 | 內容 |
| --- | --- |
| `fde2412db792532a775dedc835450d57a4f451b1` | Avalonia＋RTSS 來源整合、WPF 歷史文件整理、排除本機產物 |
| `ff25eb3d60db6643a7e7874ba426ea4e390261a5` | 公開 locked restore、完整改名、文件與 RC2 封裝／驗證腳本；**RC2 來源提交** |
| 本文件的獨立後續提交 | 記錄已完成驗證與產物雜湊；只增加報告，不變更已測試產品來源 |

署名均使用 `Kaido-MSVC <windfirge@gmail.com>`。本文件的提交 SHA 可由 `git log -1 --format=fuller -- GITHUB-INTEGRATION-0.3.0.md` 取得；避免在檔案內自我引用尚未存在的提交雜湊。最終交付另提供分支 HEAD。

## 12. 人工驗收與尚未驗證項目

保留使用者先前回報：Avalonia 桌面／拖曳保存與 Escape／系統匣／實體快捷鍵／Windows 登出，以及 FF9、Rune Factory 3 Special 全螢幕遊戲內時鐘、右上、Alt+Tab、啟停與退出清理。這些為**既有人工驗收**，本輪沒有重啟遊戲驗證。

本輪未實機驗證多螢幕、混合 DPI、螢幕插拔、Explorer 重啟、休眠喚醒、Windows 10／ARM64、僅安裝 Runtime 的乾淨 Windows 電腦。沒有將其標為通過，也不僅因這些低機率／設備限制延後個人用途候選版。

GCO-05 的拖曳後下拉同步限制仍屬 P3。RTSS 槽位仍是合作式協定：最高槽排序保護與衝突退讓保持不變，但對鎖外並行認領不能保證絕對隔離；強制終止於持 busy 時可能需要由使用者重啟 RTSS。沒有新增 DirectX Hook 或 DLL 注入。

## 13. 品質與本機資料保存

git diff --check 與 default 模式 zhtw-guard 通過；保留英文／日文，不執行全專案 fix。原始碼命名、版本、圖示、必要腳本與授權追蹤清單已核對。

原 WPF 追蹤檔、原候選來源、RC1、兩份真實設定及原 main／Tag 的保護核對保存於工作樹之外的本機資料目錄。執行紀錄亦只在本機 evidence 或外部工作目錄；未加入 Git、來源 ZIP 或 Runtime ZIP。

## 14. 正式 GitHub 發布前剩餘事項與停止點

1. 審查本分支與本報告，決定何時推送／合併；此輪沒有操作遠端。
2. 若正式發布要去除 rc2 字尾，應從決定的正式提交重新封裝並重新計算雜湊，不沿用 RC1／RC2 數值。
3. 以最終包再次核對版本、來源提交、依賴／授權、檔案清單與 checksum；報告提交和來源提交的關係須清楚保留。
4. 建議在僅有 .NET 11 Runtime x64 的乾淨 Windows／獨立使用者環境確認一般 EXE 啟動；本輪不是乾淨系統驗證。
5. 簽章與 self-contained 版本為可另行決定的工作，現有候選未簽章、framework-dependent。
6. 只有取得另外授權才推送、建立正式 Tag、建立 GitHub Release；保留 v0.2.0 Tag／附件，不強制推送。

**停止於本機開發分支、提交、公開還原驗證、RC2 及報告。沒有自行進入正式發布。**
