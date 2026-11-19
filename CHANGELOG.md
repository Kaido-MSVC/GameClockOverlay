# Changelog

## 0.3.1 — 2026-11-17

Assembly／檔案版本 0.3.1.0；資訊版本 0.3.1。

### RTSS 自動偵測與連動啟動

- 新增 RTSS.exe 偵測：手動指定路徑 → Windows 登錄（`Unwinder\RTSS`、解除安裝清單，32／64 位元檢視）→ 常見安裝位置（含非系統磁碟）；所有路徑都驗證檔名與檔案存在。
- 新增「啟動 GameClockOverlay 時自動啟動 RTSS」（預設開啟，只在遊戲內時鐘啟用時生效）、RTSS.exe 手動選擇、立即啟動按鈕，以及程序狀態（未安裝／未執行／啟動中／執行中／啟動失敗）顯示；與共享記憶體連線狀態分開顯示。
- RTSS 啟動後立即喚醒共享記憶體重試。設定新增 `AutoStartRtss`、`RtssPath`。
- 安全性：啟動前驗證 RTSS.exe 的 Authenticode 簽章，且簽署者須為 MICRO-STAR INTERNATIONAL CO., LTD.；驗證期間持有檔案，防止被替換；並改以檔案控制代碼解析出的最終路徑驗證與啟動，防止驗證後經由 junction／符號連結換成其他檔案（SEC-02）。未簽署、簽章無效或發行者不符時不啟動。
- 不提供「關閉時一併關閉 RTSS」：RTSS 以系統管理員權限執行，一般權限的本程式無法安全地要求它正常結束。

## 0.3.0 — 2026-11-16

Assembly／檔案版本 0.3.0.0；資訊版本 0.3.0。以 Avalonia 12.1.6 重新開發，與 WPF v0.2.0 分開保存。

### 平台

- UI 框架由 WPF／Windows Forms 改為 **Avalonia 12.1.6**（Avalonia、Avalonia.Desktop、Avalonia.Themes.Fluent），.NET 11／C# 15，framework-dependent win-x64。
- 正式執行檔與 Assembly 名稱統一為 GameClockOverlay；圖示資源 URI、測試、封裝腳本一併調整。既有 Avalonia 設定路徑、Mutex／IPC、RTSS Owner 與快捷鍵保持相容。
- 公開建置改用 nuget.org、提交 NuGet lock files，發布附件以 Git 提交識別來源。
- 新的設定路徑 `%LOCALAPPDATA%\GameClockOverlay-Avalonia\`、新的單一執行個體名稱；可從 v0.2.0 唯讀複製匯入設定。

### 桌面時鐘

- 保留 v0.2.0 的主要功能：透明置頂、秒數、字體大小、不透明度、四角／自由拖曳、螢幕選擇、滑鼠穿透與 Ctrl + Alt + Shift + F10 恢復、系統匣、三語介面、離線授權閱讀。
- 新增：12 小時制、字體與文字顏色選擇、永遠置頂開關、邊緣內縮、介面主題（系統／淺色／深色）、顯示／隱藏桌面時鐘。
- 設定保存沿用防彈跳、原子替換、`.bak`／`.invalid-*` 保護；Windows 登出時同步保存，不否決登出。

### 遊戲內時鐘（RTSS）

- 新增透過 **RTSS Shared Memory** 的遊戲內時鐘：可啟用／停用，RTSS 不存在時等待，RTSS 關閉或重新啟動後自動恢復，停用或結束時只清除自己的 OSD 項目。
- RTSS 需要使用者自行安裝並執行；不附帶 RTSS、SDK 或 MSI Afterburner。沒有注入、沒有自製 DirectX Hook。
- 遊戲內位置：右上（新設定預設）、右下、左下、依 RTSS 設定。使用 RTSS 官方 sticky 位置標籤，只在本程式排在最後一個非空 OSD 槽時套用，否則安全退回 RTSS 位置，不移動其他程式的文字；既有設定維持原本的 RTSS 位置。
- 「遊戲顯示」分頁顯示共享記憶體連線、實際位置、RTSS 程式與安裝狀態、最近一次錯誤。

### 審查後修正

- 分鐘模式下 RTSS 在兩次檢查之間快速重啟時，立即重新寫入，不再保留過期的「已連線」。
- 認領 OSD 槽前重新確認仍是空槽；與其他程式同時認領時退讓，不清除對方。
- 從命令列直接帶 `--ui-test` 啟動時，不再另開共用真實設定與正式 RTSS 的實體。
- 第三方授權通知改為對照實際輸出的每個 DLL，補上 Avalonia、MicroCom.Runtime、Tmds.DBus.Protocol 的著作權聲明。

### 已知限制

- RTSS OSD 槽是合作式協定，對不遵守鎖的並行寫入者只能偵測與退讓，不保證絕對隔離。
- 多螢幕、混合 DPI、Windows 10／ARM64 未驗證；真正獨佔全螢幕不保證桌面時鐘可見。
- GCO-05：拖曳後已開啟的位置下拉可能顯示舊值；不影響位置保存，屬非阻擋限制。

## WPF 發布歷史

### 0.2.0 — 2026-11-11

Formal release: assembly / file version 0.2.0.0; informational version and Git tag 0.2.0 / v0.2.0.

- Preserve corner and monitor on single/double click; commit only a completed substantial drag, restore cancelled moves.
- Replace title-based second-launch recovery with a named AutoReset event plus the existing mutex; no waiting on a foreign UI thread.
- Synchronize only external position / monitor / click-through controls; reuse settings content and scroll on recovery.
- Route tray and WinForms errors to centralized diagnostics and localized messages.
- Reuse one owned tray menu; defer exit until its Click stack unwinds, then explicitly dispose it.
- Add .gitattributes, focused regression / real-process IPC checks and reproducible versioned packaging.
- The user reports normal post-fix real-device functionality, including borderless games, Alt+Tab and resolution/display switching. A second physical monitor and mixed DPI remain untested due to unavailable equipment.

- Upgrade to stable .NET 11 / C# 15, SDK 11.0.100; preserve WPF and framework-dependent x64 deployment.
- Add adjustable font size (16–96 DIP), monitor selection, and optional full-monitor corner placement.
- Coalesce placement after display/DPI changes; update at second/minute boundaries and on time/power messages.
- Preserve old settings, click-through recovery, tray lifecycle, and Traditional Chinese / English / Japanese.
- Clarify fullscreen limitations and add technical assessment / game acceptance instructions.
- Use versioned publish directories and checksum files, preserving earlier packages.
- True exclusive fullscreen is NOT implemented. Borderless visibility is not evidence of exclusive support.

### 0.1.0 — 2026-11-04

Initial shared Windows x64 WPF clock: seconds, opacity, dragging / corners, click-through recovery,
tray, persistent settings, three languages and offline license viewer.
