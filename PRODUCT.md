# GameClock Overlay Avalonia
<!-- impeccable:product-schema 1 -->

## Platform
Windows 11 x64 desktop.

## Stack
C# 15、.NET 11、Avalonia 12.1.6，輕量 MVVM，使用現有 NuGet 快取。

## Users and purpose
個人使用與少量分享友人，讓本機時間保持可見，調整桌面時鐘的顯示與位置。

## Capabilities and constraints
保留 v0.2.0 的桌面主要功能與中英日語，獨立設定資料；正式 WPF 與已驗證的 RTSS 原型全程唯讀。本輪只有桌面 GUI，RTSS 待下一階段。沒有開機啟動、注入或自製 Hook。使用者明確要求 Windows 11 現代化、清楚分組、深淺色主題、適中視窗、低資源使用。

## Product principles
即時回饋、可恢復的穿透操作、可靠保存、比例相稱的驗證。沿用舊版即時套用與自動儲存；已提出操作偏好詢問，沒有其他指定時依原有行為開發。

## Evidence
WPF 原始碼位於 D:\projects\GameClockOverlay；RTSS-1 證據位於既有獨立原型。使用者本輪回報 FF9 與 Rune Factory 3 Special 可顯示 RTSS 時鐘，視為人工歷史結果，本輪不重測或直接整合。
