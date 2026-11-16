# GameClock Overlay v0.3.0 Release Notes

Windows x64 使用者請下載 `GameClockOverlay-v0.3.0-win-x64.zip`，解壓縮後執行 `GameClockOverlay.exe`。此執行檔未簽章。原始碼另附於 `GameClockOverlay-v0.3.0-source.zip`，兩份 ZIP 的 SHA-256 收錄在 `SHA256SUMS-v0.3.0.txt`。

## 重點

- **以 Avalonia 12.1.6 重新開發的桌面時鐘**（.NET 11、C# 15、Windows 11 x64）。保留 v0.2.0 的主要功能，新增 12 小時制、字體與顏色選擇、置頂開關、邊緣內縮與介面主題。
- **新增遊戲內時鐘（透過 RTSS）**：在支援 RTSS OSD 的遊戲畫面中顯示時間，可選擇右上、右下、左下，或依 RTSS 設定。

## 需求

- Windows 11 x64。
- [.NET 11 Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)（本套件為 framework-dependent，不含 Runtime）。
- 遊戲內時鐘另外需要 **RTSS（RivaTuner Statistics Server）**：請自行從 Guru3D 官方網站下載安裝並啟動，開啟「Show On-Screen Display」。不需要 MSI Afterburner。本套件不包含 RTSS 或其 SDK 的任何檔案。

## 從 v0.2.0 升級

- v0.3.0 是獨立的新程式（`GameClockOverlay.exe`），**不會覆蓋或修改 v0.2.0**。設定檔位置不同：
  - v0.2.0：`%LOCALAPPDATA%\GameClockOverlay\settings.json`
  - v0.3.0：`%LOCALAPPDATA%\GameClockOverlay-Avalonia\settings.json`
- 可在「一般」分頁按「複製匯入舊版設定」，只讀取 v0.2.0 的設定，不會寫回。
- 兩個版本可以同時存在，但都使用 **Ctrl + Alt + Shift + F10**。同時執行時，後啟動的一方會顯示快捷鍵註冊失敗並停用滑鼠穿透；建議只執行其中一個。

## 遊戲內時鐘的運作方式

- 本程式只透過 RTSS 官方的 Shared Memory 介面，在自己的 OSD 槽寫入時間文字；不修改 RTSS 的設定、Profile 或其他程式的 OSD，不注入遊戲，也沒有自製 DirectX Hook。
- 位置使用 RTSS 官方的 sticky 位置標籤。位置標籤會延續到排在後面的其他程式，所以本程式只在自己排在最後一個非空槽時才加標籤；否則暫時改回 RTSS 設定的位置，並在「實際位置」說明原因。對方離開後會自動恢復。
- 「遊戲顯示」分頁的狀態都來自實際寫入結果：只有成功寫入才顯示「已連線」。

## 已知限制

- **RTSS OSD 槽的並行競態（協定限制）**：RTSS 的 Shared Memory 是合作式協定，SDK 範例本身也會在鎖外認領 OSD 槽。本程式在認領前後都會重新確認，發現衝突就退讓、絕不清除其他程式的項目，但對不遵守同一把鎖、剛好同時寫入的程式，無法保證絕對隔離。
- 只支援 RTSS 共享記憶體 v2.14–v2.21（本機實測 v2.21）；其他版本會顯示「不支援的版本」且不寫入。
- 強制終止本程式時，OSD 項目會殘留到下次啟動才清除；如果剛好在持有 RTSS 鎖時被終止，需要重新啟動 RTSS。
- 如果其他程式也用 sticky 標籤放在同一個角落，兩者可能互相遮蓋。
- 「已套用」只代表最近一次寫入共享記憶體時帶有位置標籤；RTSS 沒有逐幀的渲染回報。
- GCO-05：拖曳後位置／螢幕下拉選單可能沒有即時同步；實際位置與保存值正確，屬非阻擋限制。
- 真正獨佔全螢幕不保證桌面時鐘可見。乾淨 Windows 環境、多螢幕、混合 DPI、螢幕插拔、Windows 10／ARM64 未驗證。

## 驗收狀態

- 自動化：整合分支的建置與測試紀錄摘要見 `GITHUB-INTEGRATION-0.3.0.md`；與下列既有人工回報分開記錄。
- 使用者人工驗收（既有回報）：桌面時鐘、拖曳／位置保存／Escape 取消、系統匣、實體快捷鍵、Windows 登出、Final Fantasy IX 與 Rune Factory 3 Special 全螢幕遊戲內時鐘、右上定位、Alt+Tab、停用與退出。

## 授權

本程式為 MIT 授權。隨附的第三方元件與授權見 `THIRD-PARTY-NOTICES.md` 與 `licenses/`。
