# GameClock Overlay v0.1.0

Windows 11 x64 的透明置頂時鐘，C# + WPF + .NET 10，MIT 授權。
使用本機既有 .NET 10 Windows Desktop Runtime，不需安裝 UWP、WinUI 或 MSVC 工作負載。
沒有額外 NuGet 套件、網路請求、遙測、遊戲 DLL 注入或遊戲 Hook，也不需要系統管理員權限。

## English overview

GameClock Overlay is a transparent, always-on-top desktop clock for Windows 11 x64. It shows the local time in a 24-hour format with optional seconds. Choose a screen corner or drag the clock to a custom position, and adjust its opacity from 25% to 100%.

Optional click-through lets mouse input reach the window beneath the clock. Use **Ctrl + Alt + Shift + F10** or the tray menu to turn click-through off and reopen settings. The interface supports Traditional Chinese, English, and Japanese, with a system language option and a saved manual preference.

The app runs locally without network requests, telemetry, DLL injection, or game hooks. Borderless fullscreen worked in the user's test. Exclusive fullscreen is unsupported. Check each game's rules before using the overlay.

See the [English quick start](#english-quick-start) for setup with the .NET 10 Windows Desktop Runtime x64. The project code is [MIT-licensed](LICENSE). Keep the bundled license files and third-party notices when sharing the package.

## 啟動

執行 `dist\win-x64\GameClockOverlay.exe`。分享時請保留同資料夾內的 DLL 及 JSON 檔案。
此為依賴本機 Runtime 的版本；朋友電腦需先有 Microsoft .NET 10 Windows Desktop Runtime x64。
程式未簽章。不會自動開機啟動，也不會修改遊戲或系統設定。

下載分享 ZIP 後，請先完整解壓縮再執行其中的 EXE。Runtime 官方下載：
[.NET 10 Windows Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)（選 Windows x64）。

## 使用

- 預設顯示本機 24 小時時間 `HH:mm`，右上角，不透明度 90%。可勾選秒數 `HH:mm:ss`。
- 設定中的不透明度範圍為 25–100%，數字越低越透明；背景透明。
- 未啟用穿透時，左鍵拖曳時鐘即可自由移動；從四角開始拖曳也會自動改為自由位置。
- 四角以時鐘目前所在螢幕的工作區為準，避開工作列。移到另一個螢幕時請先解除穿透，再拖曳過去。
- 右鍵點時鐘開啟設定；雙擊時鐘也能開啟。
- 時鐘與設定視窗均不占用工作列；設定視窗最小化會隱藏至系統匣，時鐘繼續顯示。雙擊系統匣圖示可解除穿透並重新開啟設定。
- 關閉設定視窗只會回到系統匣常駐。右鍵點系統匣時鐘圖示可設定、切換穿透、查看「關於／授權」或結束。
- **解除穿透：Ctrl + Alt + Shift + F10**。此快捷鍵永遠是「解除並開啟設定」，不會反向鎖定。
- 也可從系統匣選「解除穿透並設定」。再次啟動 EXE 也會要求現有執行個體解除穿透並顯示設定。
- 如果解除快捷鍵已被其他程式占用，穿透會自動停用，設定頁會說明原因。
- 語言選項：依系統、繁體中文、English、日本語。系統中文對應繁中、日文對應日文，其餘預設英文；手動選擇優先。

## 關於與授權

設定頁或系統匣的「關於／授權」可查看目前組件版本、作者 Kaido-MSVC、版權、MIT 全文及第三方通知；支援繁中／英／日，法律原文保持原樣，可離線閱讀及複製。
`LICENSE`、`THIRD-PARTY-NOTICES.txt` 及 `licenses/` 會自動隨建置／發佈複製，分享時請一併保留。
沒有額外 NuGet 套件；.NET 啟動器與另外安裝的 Runtime 授權範圍詳見第三方通知。

## 設定檔與復原

設定位於 `%LOCALAPPDATA%\GameClockOverlay\settings.json`，修改後約 450 毫秒自動儲存，正常結束時再儲存一次。
包含語言選項、秒數、不透明度、穿透、角落／自由位置及實體像素座標。
寫入採暫存檔加原子替換，前一版保存在 `settings.json.bak`。
遇到無法解析的設定會嘗試將原檔複製成 `.invalid-日期時間`，載入備份或安全預設並提示。
無法寫入時設定頁會顯示錯誤。螢幕移除或解析度改變時會嘗試將時鐘限制回可見工作區。
若需要重設，先結束程式，再自行將設定檔改名後重啟；不要在執行中修改。

## 建置與測試

在原始碼根目錄執行以下命令。本機驗證 SDK：10.0.403，Windows Desktop Runtime：10.0.14。
`global.json` 固定 10.0.403，允許同 feature band 的修補版。

```powershell
dotnet build GameClockOverlay.slnx -c Release
dotnet run --project tests/GameClockOverlay.Tests.csproj -c Release -- <測試輸出資料夾>
dotnet run --project tests/GameClockOverlay.Tests.csproj -c Release -- --lifecycle <獨立測試資料夾>
dotnet run --project tests/GameClockOverlay.Tests.csproj -c Release -- --lifecycle <相同測試資料夾> reload
dotnet publish src/GameClockOverlay.csproj -c Release --self-contained false -o dist/win-x64
```

測試不需第三方測試框架。34 項檢查涵蓋設定與實際 WPF/Win32 視窗；需要正常互動式 Windows 桌面，受限制的執行環境可能拒絕全域快捷鍵。
另有 23 項應用程式生命週期與 6 項新程序重啟檢查，使用獨立設定檔，不改動使用者設定；涵蓋設定視窗不占工作列、最小化隱藏、系統匣選單事件還原、解除穿透、自動儲存與退出移除圖示。
`tests` 的 `--interactive` 模式提供獨立點擊計數視窗，可檢查時鐘置頂與穿透；不是遊戲相容性測試。

## 最簡單驗收（約兩分鐘）

1. 啟動 EXE：確認右上角 `HH:mm`、背景透明；勾選秒數與調整不透明度。
2. 最小化設定：確認工作列沒有按鈕、時鐘仍顯示；雙擊系統匣圖示還原設定。再關閉設定：拖曳時鐘到其他位置，再右鍵開設定、選四角。
3. 啟用滑鼠穿透、關閉設定：在時鐘數字上點擊，確認底下視窗可以操作。
4. 按 **Ctrl + Alt + Shift + F10**：確認穿透解除且設定出現；亦可測試系統匣的解除選項。
5. 手動選另一種語言，結束程式再啟動：確認語言、秒數、透明度與位置記憶。
6. 開啟自己的遊戲，分別測試視窗模式與無邊框全螢幕；確認時鐘可見且遊戲滑鼠操作正常。

## 支援界線

支援及本次驗證平台為 Windows 11 x64；Windows 10、ARM64 與其他平台未驗證，不列為此版本支援平台。
啟用反作弊的遊戲請先確認遊戲及服務條款是否允許外部懸浮工具；本工具不保證相容或不受處分，也不提供繞過反作弊的方式。若遊戲禁止或發生異常，請結束本工具。

已驗證一般桌面／普通 WPF 視窗的置頂與穿透。沒有代替使用者啟動或操作真實遊戲。
使用者已實測無邊框全螢幕可顯示、真正獨佔全螢幕無法顯示；此為接受的限制，本次未重新操作遊戲。其他遊戲仍須個別驗收，**不保證真正獨佔全螢幕**。
其他置頂視窗、遊戲顯示機制、受保護畫面、安全桌面或不同權限的程式可能影響顯示與快捷鍵。
不會強搶前景焦點或用反覆提升置頂的迴圈對抗其他程式，也不繞過遊戲限制。
多螢幕／混合 DPI、螢幕熱插拔與 Explorer 重啟未完成實機驗收。
自動測試已驗證位置還原與四角，工具無法直接選取透明工具視窗，因此實際滑鼠拖曳及系統匣選單點擊仍請依上列步驟確認。

套件體積小不等於 WPF 的執行記憶體極小；本次觀察工作集約 145–153 MiB，並非長期效能基準。
未做低機率極端壓力測試、乾淨電腦安裝或真實遊戲相容性測試。

## 文件參考

已使用 Context7 查閱 .NET Desktop / WPF 文件，再以官方文件確認原生視窗與快捷鍵語意：

- [WPF Windows](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows)
- [NotifyIcon 選單](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-associate-a-shortcut-menu-with-a-windows-forms-notifyicon-component)
- [Extended Window Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)
- [Layered windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows)
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)

`HwndSource.AddHook` 僅處理本程式視窗訊息，不是對遊戲安裝 Hook。
MIT 適用本專案程式碼；Windows 與本機安裝的 .NET 仍適用各自條款。本套件未重新散布 .NET Runtime。

## 完整封裝

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1
```

封裝腳本會重新發佈 framework-dependent Windows x64 版本，檢查必要檔案及版本，
產生 `dist/packages/GameClockOverlay-v0.1.0-win-x64.zip` 及 `SHA256SUMS`。
ZIP 包含完整 `win-x64/` 目錄及授權文件，未包含 .NET Runtime。若同名 ZIP 已存在會停止，請先自行移走舊封裝。
Git 僅追蹤原始碼、測試、文件及腳本；`dist/` 與測試產物不提交。建置與封裝不會推送、建立 tag 或 GitHub Release。

## English quick start

Windows 11 x64 transparent, always-on-top clock by **Kaido-MSVC**, licensed under MIT.
Install the [.NET 10 Windows Desktop Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0),
extract the entire ZIP, and run `win-x64/GameClockOverlay.exe`. Keep all DLL, JSON and license files together.
This unsigned, framework-dependent package does not include the runtime.
Right-click or double-click the clock to open settings. Drag it while click-through is off.
Minimizing settings hides it in the tray; closing settings keeps the clock running.
Use **Ctrl + Alt + Shift + F10**, the tray recovery command, or launch the EXE again to disable click-through and open settings.
Select the tray Exit command to quit. Settings are stored in `%LOCALAPPDATA%/GameClockOverlay/settings.json`.
The UI supports Traditional Chinese, English and Japanese; About / Licenses works offline.
Borderless fullscreen worked in the user's test; **exclusive fullscreen is unsupported**.
Other games and anti-cheat systems are not guaranteed compatible. Check each game's rules before use; do not use where overlays are prohibited.
Windows 10 and ARM64, clean-machine installation, mixed-DPI monitors and Explorer restart have not been verified.
Build with .NET SDK 10.0.403 using the commands above. Run `scripts/package.ps1` to create the full ZIP and SHA256SUMS.

## 日本語クイックスタート

**Kaido-MSVC** による Windows 11 x64 用の透明・最前面時計。MIT ライセンスです。
[.NET 10 Windows Desktop Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) をインストールし、
ZIP 全体を展開して `win-x64/GameClockOverlay.exe` を実行してください。DLL、JSON、ライセンス文書も同じフォルダーに保持してください。
未署名の framework-dependent 配布で、Runtime は同梱しません。
時計の右クリックまたはダブルクリックで設定を開き、クリック透過を無効にするとドラッグできます。
設定の最小化はトレイへ隠し、設定を閉じても時計は動作します。
**Ctrl + Alt + Shift + F10**、トレイの復帰メニュー、または EXE の再起動で透過を解除して設定を開けます。終了はトレイの終了メニューから行います。
設定は `%LOCALAPPDATA%/GameClockOverlay/settings.json` に保存します。UI は繁体字中国語・英語・日本語、ライセンスはオフラインで確認できます。
ユーザーのテストでボーダーレス全画面表示を確認しています。**排他的全画面は非対応**です。
ゲームやアンチチートとの互換性は保証しません。各ゲームの規約を確認し、外部オーバーレイが禁止される場合は使用しないでください。
Windows 10・ARM64、クリーン環境、混在 DPI、Explorer 再起動は未検証です。
ビルドは .NET SDK 10.0.403 と上記コマンドを使用し、`scripts/package.ps1` で完全な ZIP と SHA256SUMS を生成できます。
