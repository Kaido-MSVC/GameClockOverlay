# GameClockOverlay 0.2.0 升級報告

檢查日期：2026-11-11。首次升級起始提交 `478534df0c51f2bae6eec2bc72b2869d831b706b`，當時工作區乾淨。本次正式版修復起點含全部未提交 v0.2.0 修改，已保留。

## 原始狀態

0.1.0，C#（net10.0 預設 C# 14）／WPF，Windows Forms NotifyIcon 與 Screen，System.Text.Json。
SDK 固定 10.0.403 latestPatch，本機實際選用 10.0.404，Desktop Runtime 10.0.15。
沒有 PackageReference／第三方 NuGet；測試為 console runner。
透明 layered WPF Topmost、NoActivate／ToolWindow、WS_EX_TRANSPARENT 穿透、註冊全域復原快捷鍵。
DispatcherTimer 每秒讀時鐘，文字有變才更新。已有 opacity、自由／四角、多螢幕座標復原、PerMonitorV2 manifest、顯示變更訊息處理。
已有三語、系統匣、單執行個體、設定原子儲存／備份、MIT 與離線授權；無全螢幕模式偵測、無獨佔後端、無字體大小或指定螢幕選項。
升級前基準：34 回歸 + 23 lifecycle + 6 restart 全數通過。未重複實作既有功能。

## 獨佔全螢幕結論

**本次沒有可靠實作真正獨佔全螢幕覆蓋。** 本次版本號代表顯示功能／Runtime 升級，不代表獨佔功能完成。

WPF Topmost 僅指定桌面視窗 Z-order，不取得其他程序的呈現鏈控制。真正 FSE 可直接控制顯示輸出，普通外部視窗無可靠受支援的通用覆蓋契約。
DX11／DX12 都不能只由 API 名稱或覆蓋螢幕的 HWND 判斷；DXGI flip、DirectFlip／Independent Flip、FSO、驅動及硬體平面會改變呈現行為。
Microsoft 說明 FSO 會把部分 FSE 遊戲轉成最佳化無邊框，外部 overlay 可見並不證明真正獨佔。
DXGI GetFullscreenState 需操作對應 swap chain；本工具沒有遊戲內 swap chain，不將視窗大小啟發式偵測當成獨佔判定。

| 候選 | 評估與選擇 |
| --- | --- |
| 保留 WPF layered Topmost | 維護成本最低，保留桌面／視窗化及可行的無邊框顯示；採用 |
| DirectComposition／Windows composition | 合成 API，沒有覆蓋外部真正獨佔的保證；無理由為此更換 |
| 原生 Win32／C++ DLL／獨立 D3D11／12 視窗 | 自己的視窗與 swap chain 仍不等於遊戲內呈現；增加建置、DPI、裝置遺失處理成本，不採用 |
| Windows Graphics Capture | 擷取影像的 API，不是插入遊戲呈現的 API；重顯捕獲遊戲將改變使用模式並增加 GPU／延遲，不採用 |
| 硬體 overlay plane | 由系統／驅動與硬體配置決定，不可作為 GTX1060 等環境的通用可靠契約 |
| 遊戲官方插件／整合 | 可能逐款實作，但非通用工具範圍，也沒有本次可用整合 |
| DLL injection／DirectX hooks | 可介入渲染但違反本次低風險預設，未開發 |

保留單一 WPF 視窗，無遊戲寫入、無擷取、無背景服務、無提權；不改遊戲模式與安全設定。
使用者已回報無邊框、Alt+Tab 及解析度／顯示設定切換正常。未提供按 DX11／DX12 分類的逐款資料；真正獨佔仍不支援。模擬視窗不替代實機結果。

## 實作

- 字體 16–96 DIP，預設 32，視窗尺寸同比例調整；沿用原字型／陰影。
- 可指定 WinForms Screen.DeviceName，暫時未連接時回退最近可見螢幕，保留選擇；拖曳解除固定螢幕。
- 工作區（預設）或完整螢幕邊界定位；後者適合無邊框，不是獨佔後端。
- DPI／顯示配置訊息後合併排程校正，讓 WPF 先處理 DPI 建議尺寸；保留實體像素座標。
- 時鐘按顯示秒／分鐘邊界更新，20ms 容差避免提早更新；僅變更文字時重繪。時間變更／電源訊息重新讀值。
- 關閉停止 timer、解除 hotkey、移除 HwndSource hook；排程定位在關閉後失效。
- 設定向後相容，新文字完整繁中／英文／日文。

## .NET 決策

採 **net11.0-windows，C# 15.0，SDK 11.0.100（allowPrerelease=false）**。
本機有正式 SDK 11.0.100 與 WindowsDesktop.App 11.0.0；官方下載頁列 2026-11-10 正式版及 C# 15。
WPF 與 Windows Forms 使用 SDK 的 WindowsDesktop reference，不新增 NuGet。新版本須以實際 Release 建置與執行測試驗證，不憑文件宣稱零風險。
目前官方政策列 .NET 10 LTS（3 年）、11 STS（2 年）均到 2028-11-14；10 的優點是較久部署經驗，11 符合已安裝版本與使用者優先選擇，沒有本工具必須降回 10 的依賴障礙。
無 Preview、無 LangVersion=latest、無 C++ 規範。

Windows x64 framework-dependent 明確使用 win-x64 RID，需 Desktop Runtime 11，適合少量分享與小包；不允許僅靠 .NET 10 啟動。
Self-contained 可省安裝 Runtime，但包較大、需自行更新 Runtime 並重做依賴／授權盤點，本次未交付。

## Claude 7 項問題修復（v0.2.0）

| 問題 | 根因 | 修復與驗證 |
| --- | --- | --- |
| P1 點擊改定位 | MouseDown 先清除定位，再進入 DragMove | 按下僅記錄游標／起始 DPI，移動超過系統門檻（至少 4 DIP）才進入原生 move loop；完成且最終位移有效才提交 Free／空 MonitorDevice。取消／小位移／InvalidOperationException 不提交並復原。原生移動中暫停 Place/CapturePosition 避免中途儲存 |
| P1 重複啟動復原 | FindWindow 依一般標題可能找錯 | 保留 Mutex，新增 Windows 具名 AutoReset Event；先建立事件再取得 Mutex，避免啟動競態遺失訊號。一次 Set 即退出，不等待 UI。ThreadPool 註冊等待僅在訊號觸發時排入 Dispatcher，退出取消等待並在最後釋放 Mutex |
| P2 設定即時同步 | Changed 未更新定位控制項 | SyncExternal 用 guard 更新位置／螢幕／穿透，不呼叫 Changed、不重建、不動其他編輯欄位 |
| P2 系統匣例外 | WinForms 事件可能落入預設處理 | 所有托盤事件使用 RunTrayAction；接管 ThreadException，與 WPF 共用 ReportError。三語簡訊，不顯示堆疊；errors.log 保留診斷，寫檔失敗記 Trace。托盤操作錯誤不直接退出 |
| P2 設定重建 | Recover 無條件 Refresh/Build | 已開啟內容同語言僅同步，恢復／啟用原視窗；切換語言才重建並保留 scroll；Recover 有重入防護 |
| P2 選單生命週期 | Click 呼叫 Changed，再 RefreshTray 釋放當前選單 | 固定單一 ContextMenuStrip，僅更新文字／勾選／可用性。Exit 排到 Dispatcher 離開 Click stack 後處理；NotifyIcon 和選單各自明確 Dispose。Icon 僅建一次、HICON finally DestroyIcon、退出 Dispose |
| P3 換行 | 無 .gitattributes，worktree 混合 CRLF/LF | Git 儲存 LF；C#／XAML／專案／JSON／Markdown LF，PowerShell worktree CRLF；二進位 -text。只正規化已修改文字檔，MIT 與既有授權原文字節不改 |

沒有採用訊息廣播，因此不需要 SendMessageTimeout；具名事件 Set 不等待接收者，主程序無回應時第二程序仍退出。
同一使用者／session 的訊號干擾為已接受限制；沒有新增服務、額外常駐程序或大型依賴。
排入 Dispatcher 的復原會合併；主程序忙碌時視窗復原須等 UI 可處理，事件送達不代表 UI 已立即完成，沒有無限等待或自動重啟。
原生 DragMove 由 Windows 處理 Escape；使用者回報實機功能正常，不同 DPI 間的整體流程因無設備未測試。

## 本次驗證紀錄

| 自動套件 | 通過 | 失敗 |
| --- | ---: | ---: |
| 既有主要回歸（含設定損壞復原、三語） | 40 | 0 |
| 顯示／定位 | 14 | 0 |
| 生命週期 | 23 | 0 |
| 生命週期重啟 | 6 | 0 |
| 新缺陷回歸（合成輸入／原生定位提交、UI 同步、選單、錯誤與實際第二 EXE） | 37 | 0 |
| 拖曳設定獨立程序重啟 | 1 | 0 |
| IPC 初始化／非泵訊息／訊號保存／釋放再啟動 | 6 | 0 |
| 合計 | **127** | **0** |

自動測試全部可執行，未執行 0。使用者本次確認修正後實機功能驗收正常，包含無邊框、Alt+Tab 與遊戲解析度／顯示設定切換。第二實體螢幕與混合 DPI 因無設備未測試，不能混入實機通過結果。
真正 Exclusive Fullscreen 仍未支援，未宣稱完成。

Release x64 / net11.0-windows / C#15：0 warnings，0 errors。
NuGet 對應用與測試執行 --vulnerable --include-transitive：沒有弱點套件，亦無額外 PackageReference；此掃描不等於整個 .NET／Windows 或驅動的安全審核。
環境仍是 Windows 11 x64、GTX1060 6GB、單一 1920×1080／96 DPI；第二裝置名稱／DPI 數值是合成測試，非第二實體螢幕。
錯誤視窗案例使用同名 WPF 模態測試視窗，不是破壞使用者設定製造的真實錯誤。第二 EXE 則是真正啟動並檢查退出狀態。
已執行的生命週期含 tray／menu／settings／timer／hotkey／mutex 釋放；沒有宣稱以短測試證明所有 GDI 或記憶體洩漏皆不存在。

完整最終紀錄：artifacts/release-validation/；scripts/test.ps1 可用新輸出目錄重跑。人工步驟見 MANUAL-ACCEPTANCE.md。

## 發佈及檔案

正式版本 **0.2.0**。專案明確指定 AssemblyVersion/FileVersion 0.2.0.0、InformationalVersion 0.2.0；manifest 同步。
保留原 MIT、作者及原法律文字。runtime 第三方通知明確標示繼承自 v10.0.14，未冒稱 .NET 11 完整盤點；本次未同梱 Runtime。

主要修改：global.json、src/*.csproj、Settings.cs、SettingsWindow.cs、OverlayWindow.cs、Strings.cs、App.cs、SingleInstance.cs、app.manifest、
tests/Program.cs／Lifecycle.cs／DisplayChecks.cs／UiSession.cs／FixChecks.cs、.gitattributes、scripts/test.ps1／package.ps1／package-candidate.ps1、README、CHANGELOG、此報告及驗收文件。
保留 PRODUCT.md 的 Windows WPF 平台描述並更新技術與正式發布授權。

- EXE：dist/releases/v0.2.0/win-x64/GameClockOverlay.exe
- 使用包：dist/releases/packages/GameClockOverlay-v0.2.0-win-x64.zip
- 測試包：dist/releases/packages/GameClockOverlay-v0.2.0-tests-win-x64.zip
- 可重建原始碼：dist/releases/packages/GameClockOverlay-v0.2.0-source.zip
- 包 SHA-256：dist/releases/packages/SHA256SUMS-v0.2.0.txt；使用包內另附 SHA256SUMS.txt。

封裝檢查：明確 win-x64 apphost PE、正式 .NET 11 framework-dependent runtimeconfig、只有專案本身的 deps library、
完整 DLL／JSON／文件／授權清單、版本號與逐檔 SHA-256。測試包含執行 runner、來源與測試紀錄（不含使用者設定或整個桌面截圖）。
舊 dist 檔案逐檔比對原 SHA-256，不覆寫舊包。正式版本使用乾淨 Commit、v0.2.0 Tag 及 GitHub Release；RELEASE-PROVENANCE.json 記錄 Commit／Tree，來源碼由相同 Git 提交匯出。

原有發布與測試包均保留，新正式輸出獨立放在 dist/releases。正式目錄／ZIP 已存在時停止，避免覆寫。Git Tag、來源碼、EXE／關於版本、文件、ZIP 與 SHA-256 必須一致才發布。

## 官方依據（2026-11-11 查閱）

Context7 查 /dotnet/wpf，DPI／Window／HwndTarget 文件；目前 .NET 發布狀態再查官方頁面。

- [.NET 11 SDK、Desktop Runtime、C# 15 正式版本](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)
- [.NET 支援政策](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [C# 語言版本與 TFM](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/configure-language-version)
- [WPF Windows-only framework](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
- [Microsoft Fullscreen Optimizations 解說](https://devblogs.microsoft.com/directx/demystifying-full-screen-optimizations/)
- [DXGI flip／DirectFlip／Independent Flip](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model)
- [DXGI GetFullscreenState](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgiswapchain-getfullscreenstate)
- [SetWindowPos Topmost 語意](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)
- [DirectComposition](https://learn.microsoft.com/en-us/windows/win32/directcomp/directcomposition-portal)
- [Windows Graphics Capture](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture)
- [部署模式](https://learn.microsoft.com/en-us/dotnet/core/deploying/)

- [EventWaitHandle](https://learn.microsoft.com/en-us/dotnet/api/system.threading.eventwaithandle)
- [ThreadPool.RegisterWaitForSingleObject](https://learn.microsoft.com/en-us/dotnet/api/system.threading.threadpool.registerwaitforsingleobject)
- [Window.DragMove](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.dragmove)
