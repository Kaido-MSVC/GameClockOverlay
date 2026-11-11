# GameClock Overlay v0.2.0

Windows 11 x64 透明置頂時鐘。C# 15 + WPF / Windows Forms + .NET 11，MIT 授權。
沒有額外 NuGet 套件、網路請求、畫面擷取、遊戲注入、遊戲 Hook、背景服務或管理員權限需求。

## English overview

GameClock Overlay is a transparent, always-on-top desktop clock for Windows 11 x64. It shows local time in 24-hour format with optional seconds, adjustable font size and opacity, a screen corner or a free position.

Optional click-through lets mouse input reach the window beneath the clock. Use **Ctrl + Alt + Shift + F10** or the tray menu to unlock and reopen settings. The interface supports Traditional Chinese, English and Japanese, with system language and saved manual preferences.

The app runs locally without network requests, telemetry, DLL injection or game hooks. The user has accepted borderless games; true exclusive fullscreen is unsupported. See the [English setup](#english) for .NET 11 Windows Desktop Runtime x64 requirements. The project code is [MIT-licensed](LICENSE); keep bundled notices when sharing.

## 啟動與使用

安裝 [.NET 11 Windows Desktop Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)，
完整解壓縮 `GameClockOverlay-v0.2.0-win-x64.zip`，執行 `win-x64/GameClockOverlay.exe`。
本機發佈檔在 `dist/releases/v0.2.0/win-x64/`，請保留所有 DLL、JSON、文件與 licenses/。
此為未簽章的 framework-dependent 發佈，不包含 Runtime。舊版 `dist/win-x64/` 及較早的 `dist/v0.2.0/` 不是本次修正版。

- 本機 24 小時時間 `HH:mm`，可選 `HH:mm:ss`；預設右上角、90% 不透明度。
- 字體大小 16–96 DIP，預設 32。依 Windows DPI 縮放；不透明度 25–100%。
- 可自由拖曳或選四角；可指定螢幕。預設以工作區避開工作列；「使用整個螢幕邊界」適合無邊框遊戲，但可能與工作列重疊。
- 指定的螢幕未連接時，暫用位置附近的螢幕。裝置名稱可能隨硬體配置改變，需重新選擇。
- 單擊／雙擊不會更改固定位置或指定螢幕。超過系統拖曳門檻（至少 4 DIP、依起始 DPI 換算）且有效完成移動後，才改為自由位置並解除固定螢幕；取消或微小移動保留原設定。已開啟的設定欄位會即時同步。
- 單一時鐘，可在螢幕間移動；不會每個螢幕各開一個時鐘。
- 右鍵／雙擊時鐘開設定。關閉／最小化設定後，時鐘繼續於系統匣執行。
- 可啟用滑鼠穿透。**Ctrl + Alt + Shift + F10** 永遠解除穿透並開啟設定；亦可雙擊系統匣、使用系統匣解除選單或再次執行 EXE 復原。
- 快捷鍵被占用時停用穿透。從系統匣選「結束程式」才會退出。
- 支援依系統、繁體中文、English、日本語；關於／授權可離線閱讀與複製。

## 全螢幕支援範圍

| 模式 | 本版行為與驗收狀態 |
| --- | --- |
| 桌面／視窗化 | WPF 透明 Topmost，已做一般 WPF 視窗回歸 |
| 無邊框全螢幕 | 使用者已驗收其遊戲正常；其他遊戲仍須逐款確認 |
| Fullscreen Optimizations | 可能由 Windows 轉成最佳化無邊框，須逐款驗收；不算真正獨佔通過 |
| 真正獨佔全螢幕 | **無可靠支援**；未加入注入／呈現攔截；不列為支援功能 |

遊戲選單的「全螢幕」、視窗覆蓋整個螢幕、DX11／DX12 標籤或 Alt+Tab 很快，都不能單獨證明真正獨佔。
本程式沒有自動判定其他遊戲 swap chain 模式的功能，也不會改變遊戲顯示模式。
DirectComposition、獨立 DirectX 視窗或 Windows Graphics Capture 不能保證解決此限制。
不會搶焦點或持續提升 Z-order 對抗遊戲。可選無邊框／視窗化，或把時鐘移到另一個螢幕。
詳細技術理由見 [升級報告](UPGRADE-REPORT.md)，操作步驟見 [實機驗收](MANUAL-ACCEPTANCE.md)。

## 設定與復原

`%LOCALAPPDATA%/GameClockOverlay/settings.json`，約 450 ms 合併儲存，退出時再次儲存。
原有語言、秒數、透明度、穿透、位置及實體像素座標均保留；新增 FontSize、MonitorDevice、UseMonitorBounds。
0.1.0 設定缺少新欄位時自動採預設。原子替換，前版為 `.bak`；格式損壞先保留 `.invalid-日期時間`，再試備份或預設。
儲存錯誤會顯示於設定頁。若需重設，退出後自行將設定檔改名。勿在執行中編輯設定檔。
顯示配置與 DPI 變動後以事件重新校正位置；第二實體螢幕、熱插拔及混合 DPI 因設備限制未完成驗收；已回報遊戲解析度切換正常。

## 建置、測試及發佈

正式 SDK **11.0.100**、Windows Desktop Runtime **11.0.0**，`global.json` 允許同 feature band 的正式修補版。
應用程式及測試為 `net11.0-windows`、`LangVersion=15.0`、x64；未採 Preview API 或套件。

```powershell
powershell -NoProfile -File scripts/test.ps1 -OutputDirectory artifacts/release-validation
# Commit the verified source and create the matching local v0.2.0 tag first.
./scripts/package-candidate.ps1 -Candidate '' -OutputRoot dist/releases -ValidationDirectory artifacts/release-validation
```

測試為自有 console runner，需互動式 Windows 桌面；非 `dotnet test` 測試框架。
使用獨立設定檔，不改動正式使用者設定；測試視窗不能證明遊戲相容性。
封裝明確指定 `-r win-x64 --self-contained false`；已存在的版本目錄或 ZIP 會拒絕覆寫。
輸出 `dist/releases/packages/GameClockOverlay-v0.2.0-win-x64.zip`、來源碼／測試 ZIP 與 `SHA256SUMS-v0.2.0.txt`。
舊包與舊雜湊不覆寫。正式封裝要求乾淨的已提交工作區與指向 HEAD 的 v0.2.0 Tag，來源碼包由該提交直接匯出；封裝腳本本身不推送或發布。

Framework-dependent 包小、由已安裝的 Runtime 接收修補，但需先安裝相符 Desktop Runtime。
Self-contained 可不依賴預裝 Runtime，但包較大且每次安全修補都要重新發佈；本次未交付此模式。
兩者均需 Windows；不可把單一 EXE 當作完整發佈。Windows 10／ARM64／乾淨電腦未驗證。

## 修正後驗證

127 項自動檢查涵蓋既有功能、合成點擊／拖曳提交與取消、跨程序復原、選單生命週期及三語資源。
第二次執行以 Mutex + Windows 具名 AutoReset 事件通知現有程序，不依賴視窗標題，不等待對方 UI，亦無輪詢／額外服務。
設定頁恢復不重建整頁；系統匣選單使用固定實例，離開退出事件後才釋放。
系統匣事件錯誤顯示簡短三語訊息，完整診斷嘗試寫入設定資料夾的 errors.log；不把堆疊顯示給一般使用者。
Git 儲存文字為 LF，C#／XAML／Markdown 使用 LF；PowerShell 工作檔 CRLF，未改動二進位或 MIT 原文。

使用者已確認修正後實機功能驗收正常，包括無邊框、Alt+Tab、遊戲解析度／顯示器切換。**第二實體螢幕與混合 DPI 因無設備未測試**。
合成拖曳不代表真實滑鼠、第二實體螢幕、混合 DPI 或遊戲驗收；詳見實機驗收文件。
本機仍為單螢幕 1920×1080、100% DPI；真正獨佔仍無可靠支援。

## 授權

保留 MIT 與既有作者聲明。LICENSE、THIRD-PARTY-NOTICES.txt、licenses/ 隨包與關於視窗提供。
MIT 適用本專案，不會把 Windows、系統字型或 Runtime 全部重新授權為 MIT；本包不散布 Runtime。

## English

Windows 11 x64 transparent topmost clock, C# 15 / WPF / .NET 11, MIT. Install the
[.NET 11 Windows Desktop Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/11.0), extract the full ZIP and run `win-x64/GameClockOverlay.exe`.
Keep every DLL, JSON and license file. The unsigned framework-dependent package excludes the runtime.
Choose seconds, opacity (25–100%), font size (16–96 DIP), a monitor and a corner/free position.
Full monitor bounds may overlap the taskbar. Only a completed drag beyond the DPI-scaled threshold resets the monitor selection; single/double clicks and cancelled moves preserve it. Open settings synchronize without rebuilding or losing scroll position.
Close/minimize settings to keep running in the tray. Ctrl+Alt+Shift+F10, the tray recovery menu or a second launch unlocks click-through and opens settings. Exit from the tray.
Settings remain in `%LOCALAPPDATA%/GameClockOverlay/settings.json`; old settings are compatible.
The user reports normal real-device operation, including borderless games, Alt+Tab and resolution switching. A second physical monitor and mixed DPI were not tested because equipment was unavailable. Other games require individual checks.
**Reliable true exclusive fullscreen overlay is unsupported.** No injection, game hooks or capture are used.
A game's Fullscreen label does not prove exclusive presentation. See MANUAL-ACCEPTANCE.md and UPGRADE-REPORT.md.
Build with SDK 11.0.100 using the commands above; no external NuGet packages. UI supports Traditional Chinese, English and Japanese.

## 日本語

Windows 11 x64 用の透明・最前面時計。C# 15 / WPF / .NET 11、MIT ライセンス。
[.NET 11 Windows Desktop Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/11.0) をインストールし、ZIP 全体を展開して `win-x64/GameClockOverlay.exe` を実行してください。
DLL・JSON・ライセンスを保持してください。未署名の framework-dependent 配布で Runtime は同梱しません。
秒表示、不透明度（25–100%）、文字サイズ（16–96 DIP）、モニター、四隅・自由位置を設定できます。
画面全体の境界を使用するとタスクバーと重なる場合があります。DPI に応じたしきい値を超えてドラッグを完了した場合のみ、自由位置に切り替わりモニター指定が解除されます。クリック・ダブルクリック・キャンセルでは保持します。設定画面はスクロール位置を保持して同期します。
設定を閉じる・最小化してもトレイで動作します。Ctrl+Alt+Shift+F10、トレイの復帰メニュー、または再起動で透過解除・設定を開き、トレイの終了で終了します。
設定は `%LOCALAPPDATA%/GameClockOverlay/settings.json`。旧設定も読み込めます。
ユーザーの実機機能確認は正常です。ボーダーレス、Alt+Tab、解像度変更を確認済み。第2モニター・混在 DPI は機材不足のため未検証です。他のゲームは個別確認が必要です。
**真の排他フルスクリーンへの確実な重ね表示は非対応です。** 注入、ゲーム Hook、画面キャプチャは使用しません。
ゲームの「全画面」設定だけでは排他表示を証明できません。MANUAL-ACCEPTANCE.md と UPGRADE-REPORT.md を参照してください。
ビルドには SDK 11.0.100 と上記コマンドを使用。追加 NuGet はありません。UI は繁体字中国語・英語・日本語に対応します。
