# GameClock Overlay v0.3.1 Release Notes

Windows x64 使用者請下載 `GameClockOverlay-v0.3.1-win-x64.zip`，完整解壓縮後執行 `win-x64/GameClockOverlay.exe`。執行檔未簽章。原始碼另附於 `GameClockOverlay-v0.3.1-source.zip`，兩份 ZIP 的 SHA-256 收錄在 `SHA256SUMS-v0.3.1.txt`。

## 重點

- **RTSS 自動偵測**：自動找出 RTSS.exe，安裝在非系統磁碟也能找到；也可以手動選擇。
- **連動啟動 RTSS**：可選擇在啟動 GameClockOverlay 時一併啟動 RTSS（預設開啟）。
- **安全性**：只會啟動由 RTSS 發行者有效簽署、且未被替換的 RTSS.exe。

## 新功能

### RTSS 安裝偵測

依下列順序尋找 RTSS.exe：

1. 「遊戲顯示」分頁手動指定的路徑。
2. Windows 登錄：RTSS 安裝程式寫入的資訊與「解除安裝」清單，32／64 位元檢視都會檢查。
3. 常見安裝位置：Program Files (x86)、Program Files，以及其他固定磁碟上的同名資料夾。

每個找到的路徑都必須是確實存在、名稱為 `RTSS.exe` 的檔案。手動指定的路徑失效時，介面會提示，本次改用自動偵測的結果。

### 連動啟動

- 新增「啟動 GameClockOverlay 時自動啟動 RTSS」，預設開啟。
- 只在已勾選「在遊戲中顯示時鐘」時生效。
- RTSS 已在執行時不會重複啟動；找不到 RTSS.exe 時不會啟動。
- RTSS 本身要求系統管理員權限，所以 Windows 會顯示使用者帳戶控制（UAC）提示。本程式自身維持一般權限。
- 新增「立即啟動 RTSS」按鈕，以及「選擇 RTSS.exe…」與「改回自動偵測」。
- 「RTSS 狀態」會分別顯示 RTSS 程式的狀態（未偵測到安裝／未執行／啟動中／執行中／啟動失敗）與共享記憶體連線狀態：RTSS 正在執行，不代表遊戲內時鐘已連線。
- 啟動失敗（例如取消 UAC 提示）時會顯示原因，桌面時鐘與其他功能照常運作。
- 設定新增兩個欄位：`AutoStartRtss`、`RtssPath`。從 v0.3.0 升級時，自動啟動預設為開啟、路徑為自動偵測。

### 不提供「關閉時一併關閉 RTSS」

RTSS 以系統管理員權限執行，一般權限的程式無法送出正常的結束要求，而本程式也不使用強制結束。所以結束 GameClockOverlay 時，RTSS 會繼續執行。

## 安全修正

- **SEC-01：只啟動有效簽署的 RTSS.exe。**
  - 問題：如果 RTSS 安裝在非系統磁碟，該資料夾通常可被一般程式改寫。被替換的 RTSS.exe 可能借用每次啟動都會出現的 UAC 提示，取得系統管理員權限。
  - 修正：v0.3.1 啟動前會驗證 Authenticode 數位簽章，且簽署者必須為 MICRO-STAR INTERNATIONAL CO., LTD.；未簽署、簽章無效或發行者不符時不啟動。簽章驗證不連網。
- **SEC-02：防止驗證後被換成其他檔案。**
  - 從驗證到啟動期間持有 RTSS.exe，讓它無法被改寫或改名。
  - 一律啟動實際驗證過的那個檔案的真實路徑，所以路徑中的 junction 或符號連結就算在驗證後被改指向別處，也不會啟動其他檔案。
  - 無法確認實際位置（例如位於網路磁碟）時不啟動。

建議：RTSS 安裝在 `Program Files (x86)` 這類只有系統管理員能寫入的位置最安全。

## 需求

- Windows 11 x64。
- [.NET 11 Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)（本套件為 framework-dependent，不含 Runtime）。
- 遊戲內時鐘需要另外安裝 **RTSS（RivaTuner Statistics Server）**，請從 Guru3D 官方網站下載，並在 RTSS 中開啟「Show On-Screen Display」。不需要 MSI Afterburner。本套件不包含 RTSS 或其 SDK 的任何檔案，也不會下載 RTSS。

## 從 v0.3.0 升級

- 直接以新的檔案取代即可；設定檔位置不變：`%LOCALAPPDATA%\GameClockOverlay-Avalonia\settings.json`。
- 如果你原本就開啟了遊戲內時鐘，升級後第一次啟動時若 RTSS 尚未執行，會出現 RTSS 的 UAC 提示。不想要的話，可以在「遊戲顯示」分頁關閉「啟動 GameClockOverlay 時自動啟動 RTSS」。
- 與 WPF v0.2.0 的關係不變：兩者分開保存、分開設定，同時執行時快捷鍵只有一方可用。

## 已知限制

- RTSS 改用其他憑證簽署，或使用未簽署的舊版時，自動啟動會顯示「啟動失敗」並說明原因；仍可自行啟動 RTSS，遊戲內時鐘照常連線。
- 尚未回應 UAC 提示時就結束本程式，事後按「是」仍會啟動 RTSS。
- 其餘限制與 v0.3.0 相同：RTSS OSD 槽是合作式協定，無法對不遵守同一把鎖的程式保證絕對隔離；只支援 RTSS 共享記憶體 v2.14–v2.21；強制終止本程式會讓 OSD 項目殘留到下次啟動；真正獨佔全螢幕不保證桌面時鐘可見；多螢幕、混合 DPI、Windows 10／ARM64 未驗證。

## 驗收狀態

- 自動化：Release 建置通過，全部測試模式通過（核心、GUI、RTSS 連動啟動、登出、保存失敗、快捷鍵衝突、系統匣）。RTSS 啟動相關測試以模擬方式進行，不會實際啟動 RTSS；簽章驗證以本機真實 RTSS.exe、竄改副本、其他發行者的程式與未簽署檔案測試。
- 使用者實機驗收：已確認 RTSS 自動偵測與連動啟動可正常運作。

## 授權

本程式為 MIT 授權。隨附的第三方元件與授權見 `THIRD-PARTY-NOTICES.md` 與 `licenses/`。
