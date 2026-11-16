---
name: GameClock Overlay Avalonia
description: 清楚、即時回饋的 Windows 11 桌面時鐘設定工具
colors:
  settings-bg-light: "#F5F6F8"
  settings-bg-dark: "#1B1D21"
  settings-text-light: "#20242C"
  settings-text-dark: "#F2F3F5"
  preview-bg: "#18232E"
  preview-bg-alt: "#E6EBF0"
  error-light: "#BA2636"
  error-dark: "#FF929C"
  clock-default: "#FFFFFF"
typography:
  title:
    fontFamily: "Microsoft JhengHei UI"
    fontSize: "28px"
    fontWeight: 600
  body:
    fontFamily: "Microsoft JhengHei UI"
    fontSize: "14px"
  hint:
    fontFamily: "Microsoft JhengHei UI"
    fontSize: "12px"
  clock-default:
    fontFamily: "Consolas"
    fontSize: "32px"
rounded:
  preview: "10px"
spacing:
  tight: "4px"
  small: "6px"
  row: "8px"
  body: "12px"
  group: "16px"
  section: "20px"
  window-top: "24px"
  window-side: "28px"
components:
  settings-light:
    backgroundColor: "{colors.settings-bg-light}"
    textColor: "{colors.settings-text-light}"
    typography: "{typography.body}"
    padding: "24px 28px 20px"
  settings-dark:
    backgroundColor: "{colors.settings-bg-dark}"
    textColor: "{colors.settings-text-dark}"
    typography: "{typography.body}"
    padding: "24px 28px 20px"
  clock-preview:
    backgroundColor: "{colors.preview-bg}"
    rounded: "{rounded.preview}"
    padding: "12px 16px"
  footer-button:
    padding: "9px 16px"
  error-light:
    textColor: "{colors.error-light}"
    typography: "{typography.hint}"
  error-dark:
    textColor: "{colors.error-dark}"
    typography: "{typography.hint}"
---

# Design System: GameClock Overlay Avalonia

## Overview

**Creative North Star: "清楚的桌面設定面板"**

以 Windows 11 原生桌面工具的操作習慣為準：現代、簡潔、清楚分組，提供跟隨系統、淺色與深色主題。採用 Avalonia Fluent 標準控制項，讓時鐘預覽、設定欄位與狀態訊息各有明確位置。

介面文字以台灣繁體中文為主要參考，保留英文與日本語。設定即時套用並自動儲存；視覺回饋須清楚呈現此行為，無須另加儲存按鈕。此文件描述目前桌面介面的設計，不代表 RTSS、完整功能或硬體情境已驗收。

**Key Characteristics:**

- 標準 Fluent 控制項與熟悉的桌面操作。
- 深淺主題使用一致的版面與語意。
- 深色時鐘預覽、可讀錯誤訊息與持續可見的操作狀態。
- 適中視窗與可捲動的分頁內容。

來源為 `PRODUCT.md`、`src/Views/SettingsWindow.cs`、`src/Views/ClockWindow.cs`、`src/App.cs` 與 `src/Models/ClockSettings.cs`。技術基準為 C# 15、.NET 11、Avalonia / Avalonia.Desktop / Avalonia.Themes.Fluent 12.1.6。前置資料中的 `px` 為可攜式設計格式；套用至 Avalonia 時應視為 DIP，不是實體螢幕像素。

`.impeccable/design.json` 採 schemaVersion 2，補充顯示名稱、合成色階、時鐘陰影、敘述與面板元件範例。合成色階僅供面板瀏覽，不是產品使用的色票。HTML／CSS 是原生控制項的結構示意，瀏覽器的滑入與焦點樣式不代表 Avalonia FluentTheme 的精確外觀；正式外觀以實作及既有截圖為準。

## Colors

冷灰中性色支撐設定介面，深藍灰預覽承接使用者選定的時鐘色彩，錯誤紅色依主題調整。

### Neutral

- `settings-bg-light`／`settings-bg-dark`：設定視窗背景。
- `settings-text-light`／`settings-text-dark`：相對應主題的主要文字。
- `preview-bg`：深淺主題共用的時鐘預覽底色。
- `preview-bg-alt`：使用者文字色與 `preview-bg` 對比低於 3:1（例如 `#161B22`）時改用的淺色預覽底；依文字色切換，不隨主題切換。
- `clock-default`：桌面時鐘的初始文字色；實際色彩由使用者設定。

### Semantic states

- `error-light`／`error-dark`：無效色碼提示，必須隨目前實際主題切換。
- 選取、焦點、停用及一般控制項底色由既有 FluentTheme 提供；本專案未另訂品牌強調色。
- 色碼快捷選項是可選的時鐘文字色，不是設定介面的品牌色盤。

**The Theme Pair Rule.** 背景、主要文字與錯誤提示必須一起對應實際主題；預覽底色維持共用。

## Typography

設定介面使用前置資料中的 `body` 字體，標題使用 `title`，輔助說明與狀態使用 `hint`。部分導讀文字使用 13 DIP、預覽區標籤使用 16 DIP；勿把所有文字放大到同一層級。輔助說明可降低不透明度，錯誤訊息則保留完整可讀性。

桌面時鐘預設採 `clock-default`，字體可由使用者選擇系統字體，字級範圍為 16–96 DIP。設定視窗內的時鐘預覽最多 56 DIP，桌面時鐘維持使用者選定大小；預覽說明須持續表達這個差異。

**The Preview Limit Rule.** 不得將預覽的字級上限套用到桌面時鐘，也不得把縮小後的預覽描述成原尺寸。

## Layout

設定視窗初始尺寸為 850 × 740 DIP，最小 640 × 520 DIP。根容器使用前置資料的視窗外距：左右 `window-side`、上方 `window-top`、下方 `section`。標題與預覽位於上方，狀態與「關閉／結束」位於下方，分頁內容使用中間剩餘空間。

設定列為兩欄：左欄彈性寬度，右欄 280 DIP。每列上下間距使用 `row`；一般垂直內容間距使用 `body`，較鬆的資訊群組使用 `group`。分頁內垂直捲動，停用水平捲軸；說明文字換行。最小尺寸時仍維持桌面兩欄操作，不套用網頁導覽列或行動版斷點。

預覽框寬度範圍為 260–400 DIP，旁邊說明最大寬度 280 DIP。每個分頁依序處理一般、外觀、位置、遊戲及關於資訊，避免把不相關的控制項塞入同一群組。

## Elevation & Depth

設定頁以背景色、間距與 Fluent 控制項狀態建立層次，未另訂卡片陰影或裝飾性模糊效果。保留系統與 FluentTheme 的視窗、彈出及焦點外觀。桌面時鐘字形具有黑色陰影，模糊半徑 4 DIP、位移 X 1／Y 2 DIP、不透明度 0.9；這是時鐘在桌布上保持可讀的專用效果，不推廣成設定頁的容器陰影。

## Shapes

預覽框使用 `rounded.preview`，其餘按鈕、欄位與分頁的邊框及圓角沿用 FluentTheme。桌面時鐘無視窗裝飾、背景透明；設定視窗保持標準桌面視窗外觀。不要為每個設定群組增加額外卡片或膠囊造型。

## Components

### Buttons

採標準 Fluent Button，保留既有滑入、按下、鍵盤焦點與停用狀態，不自訂未觀察到的品牌變體。頁尾按鈕使用 `footer-button` 內距。「關閉」隱藏設定視窗；「結束」保存並結束應用程式，兩個動作的文字與位置應明確區別。

### Inputs / Fields

核取方塊、選單與數字欄位採 Fluent 控制項；核取方塊、選單及數字欄位最小高度為 36 DIP。為輸入控制項提供可存取名稱，維持可見標籤與用途相符。色碼欄位接受 `#RRGGBB`，無效輸入時在欄位下方顯示目前主題的錯誤文字，保留最後有效設定；不得僅靠顏色表達錯誤。

### Navigation

使用標準 TabControl，分頁標籤沿用本文字級，內容區各自垂直捲動。切換語言或重建內容時保留原選取分頁。遊戲頁明確呈現目前階段的限制，不將預留資訊包裝成可用的 RTSS 整合功能。

### Clock preview

預覽使用 `clock-preview` 的色彩、圓角與內距，文字沿用使用者設定的字體、色彩與不透明度，字級遵守預覽上限。讓說明與預覽並列，維持最小尺寸下的文字換行。

### Status and recovery

頁尾持續顯示儲存中、已儲存或錯誤狀態。穿透設定必須搭配恢復操作說明；恢復熱鍵不可用時停用穿透選項並說明原因，不讓純視覺簡化移除必要的恢復線索。

## Do's and Don'ts

### Do:

- **Do** 使用標準 Fluent 控制項與一致的鍵盤焦點行為。
- **Do** 同時維護深淺主題的背景、文字與錯誤提示。
- **Do** 在最小視窗尺寸維持說明可換行、內容可捲動。
- **Do** 保留台灣繁體中文、英文與日本語各自的原始語系文字。
- **Do** 清楚說明預覽字級上限與桌面時鐘的差異。

### Don't:

- **Don't** 用網頁宣傳頁、大型裝飾標題或卡片牆取代桌面設定操作。
- **Don't** 讓錯誤提示依賴低不透明度的輔助文字樣式。
- **Don't** 新增與即時套用、自動儲存行為矛盾的儲存按鈕。
- **Don't** 將視覺複核通過擴張為完整功能、硬體、授權或發布驗收。

現有視覺證據為 `evidence/review-fixes/light-settings.png`、`dark-settings.png`、`compact-settings.png`、`light-color-error.png`、`dark-color-error.png`。`evidence/ui-review-verdict.md` 對原有兩項缺陷的結論為 resolved／ship；其範圍僅限該次局部 UI 複核，截圖為 RenderTargetBitmap 客戶區證據。本文件擷取既有實作與紀錄，未新增執行或測試結果。
