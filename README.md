# GameClock Overlay v0.3.0

Windows 11 x64 透明置頂時鐘，可選擇透過 RTSS 在遊戲畫面中顯示時間。
C# 15 + **Avalonia 12.1.6** + .NET 11，MIT 授權。沒有網路請求、遙測、遊戲注入、自製 DirectX Hook、背景服務或管理員權限需求。

v0.3.0 是以 Avalonia 重新開發的新版，與原 WPF v0.2.0 分開保存、分開設定；v0.2.0 的程式碼、Tag 與 Release 不受影響。

## English overview

GameClock Overlay is a transparent, always-on-top desktop clock for Windows 11 x64, rebuilt on Avalonia 12.1.6 and .NET 11. It shows local time in 24- or 12-hour format with optional seconds, a chosen font, color, size and opacity, at a screen corner or a free position.

Version 0.3.0 adds an optional **in-game clock through RTSS (RivaTuner Statistics Server)**. RTSS is a separate program that you install and start yourself; it is not bundled. MSI Afterburner is not required. GameClock Overlay only writes its own text into RTSS shared memory: it does not change RTSS settings or other apps' on-screen displays, and it never injects into games.

Click-through can be unlocked with **Ctrl + Alt + Shift + F10** or the tray menu. The interface supports Traditional Chinese, English and Japanese. Requires the [.NET 11 Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/11.0) (framework-dependent package).

## 啟動與使用

1. 安裝 [.NET 11 Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)（Microsoft.NETCore.App 11.0.0 以上的同主版本）。
2. 完整解壓縮 `GameClockOverlay-v0.3.0-…-win-x64.zip`，執行 `win-x64/GameClockOverlay.Avalonia.exe`。請保留所有 DLL、JSON、文件與 `licenses/`。

本套件是未簽章的 framework-dependent 版本，不包含 .NET Runtime。

### 桌面時鐘

- 24 小時或 12 小時制，可選秒數；字體、顏色、大小（16–96 DIP）、不透明度（25–100%）。
- 四角或自由拖曳，可指定螢幕與邊緣內縮；可選擇使用整個螢幕邊界。
- 可設定永遠置頂、滑鼠穿透。**Ctrl + Alt + Shift + F10** 會解除穿透、顯示時鐘並開啟設定；也可從系統匣或再次執行 EXE 恢復。
- 關閉設定視窗後時鐘仍常駐系統匣；從系統匣或設定的「結束程式」退出。
- 介面主題（系統／淺色／深色），語言（依系統／繁體中文／English／日本語）。

### 遊戲內時鐘（RTSS）

- **需要使用者自行安裝並執行 RTSS**（從 Guru3D 官方網站取得）。本程式不附帶、不下載、不啟動 RTSS；不需要 MSI Afterburner。
- 在 RTSS 中開啟「Show On-Screen Display」，再到「遊戲顯示」分頁勾選「在遊戲中顯示時鐘（透過 RTSS）」。啟動順序不拘，RTSS 晚啟動、中途關閉或重新啟動時會自動重新連線。
- 時間格式沿用桌面時鐘的秒數與 12／24 小時制；字型、大小、顏色由 RTSS 的 OSD 設定決定。
- 位置：右上（全新設定的預設值）、右下、左下，或「依 RTSS 設定」（RTSS 預設為左上）。為了不移動其他 OSD 程式的文字，本程式會使用排在最後的 OSD 槽；後面已有其他程式時，會暫時改回 RTSS 設定的位置，並在「實際位置」說明原因。
- 停用或結束程式時會清除自己的 OSD 項目，不動其他程式的項目。
- 能否在遊戲中顯示，取決於 RTSS 對該遊戲的支援；使用者已在 Final Fantasy IX 與 Rune Factory 3 Special 的全螢幕畫面確認可以顯示。

## 設定

`%LOCALAPPDATA%\GameClockOverlay-Avalonia\settings.json`，變更後約 450 ms 合併儲存，退出時再次同步儲存。原子替換並保留 `.bak`；格式損壞時先保留 `.invalid-*` 副本，再嘗試備份或預設值。

與 WPF v0.2.0 的 `%LOCALAPPDATA%\GameClockOverlay\settings.json` 完全分開。「一般」分頁的「複製匯入舊版設定」只讀取舊檔，不會寫回。

## 已知限制

- 真正獨佔全螢幕不保證桌面時鐘可見；需要在遊戲中顯示時請使用 RTSS 遊戲內時鐘。
- RTSS 共享記憶體只支援 v2.14–v2.21（本機實測 v2.21）。
- RTSS 的 OSD 槽是合作式協定：不遵守同一把鎖、在鎖外寫入的其他程式，與本程式同時認領同一槽時，本程式只能偵測並退讓，無法保證絕對隔離。
- 強制終止本程式時，自己的 OSD 項目會殘留到下次啟動才清除；如果剛好在持有 RTSS 鎖時被終止，需要重新啟動 RTSS 才能恢復。
- 與 WPF v0.2.0 使用相同的快捷鍵；兩者同時執行時，後啟動的一方無法註冊快捷鍵，會停用滑鼠穿透。
- 多螢幕、混合 DPI、螢幕插拔未實機驗證。Windows 10／ARM64 未驗證。

詳細說明見 [Release Notes](RELEASE-NOTES-0.3.0.md)、[CHANGELOG](CHANGELOG.md) 與 [第三方元件](THIRD-PARTY-NOTICES.md)。

## 建置與測試

SDK **11.0.100**（`global.json` 固定）、`net11.0-windows`、C# 15、x64。

```powershell
dotnet build src/GameClockOverlay.Avalonia.csproj -c Release
dotnet build tests/GameClockOverlay.Avalonia.Tests.csproj -c Release
tests/bin/Release/net11.0-windows/win-x64/GameClockOverlay.Avalonia.Tests.exe <證據資料夾>
./scripts/package-rc.ps1 -Candidate rc1
```

測試是自有 console runner，需要互動式 Windows 桌面。測試使用獨立設定檔與隨機名稱的隔離共享記憶體，不會改動使用者設定或正式 RTSS；只有明確指定 `--rtss-live-ui` 時才會寫入正式 RTSS。

`NuGet.Config` 目前只指向開發機的本機 NuGet 快取，其他電腦需改成可用的套件來源才能還原。
