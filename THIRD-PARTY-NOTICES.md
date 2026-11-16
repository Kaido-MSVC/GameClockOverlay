# 第三方元件

本程式自身採 MIT 授權（見 `LICENSE`）。下列第三方元件分別適用各自的條款，不因本專案採 MIT 就全部改為 MIT。授權條文放在 `licenses/` 資料夾，也可以在設定視窗的「關於／授權」分頁離線閱讀。

對照依據：`src/bin/Release/net11.0-windows/win-x64/` 的實際輸出檔案，以及本機 NuGet 快取中各套件的 nuspec（`<license>`、`<copyright>`）與套件內附的授權檔。

## 隨程式附帶的元件（win-x64）

| 輸出檔案 | 套件與版本 | 授權 | 著作權 | 條文 |
| --- | --- | --- | --- | --- |
| `Avalonia*.dll`（Base、Controls、Desktop、Dialogs、FreeDesktop、FreeDesktop.AtSpi、HarfBuzz、Markup、Markup.Xaml、Metal、MicroCom、Native、OpenGL、Remote.Protocol、Skia、Themes.Fluent、Vulkan、Win32、Win32.Automation、X11、DesignerSupport） | Avalonia 家族 12.1.6 | MIT | Copyright 2013-2026 © The AvaloniaUI Project | `licenses/Avalonia-12.1.6-LICENSE.txt` |
| `MicroCom.Runtime.dll` | MicroCom.Runtime 0.11.6 | MIT | Copyright 2021 © Nikita Tsukanov | `licenses/MicroCom.Runtime-0.11.6-LICENSE.txt` |
| `Tmds.DBus.Protocol.dll` | Tmds.DBus.Protocol 0.94.1 | MIT | Tom Deseyn（nuspec 未列年份） | `licenses/Tmds.DBus.Protocol-0.94.1-LICENSE.txt` |
| `SkiaSharp.dll` | SkiaSharp 3.119.4 | MIT | © Microsoft Corporation；Xamarin, Inc. | `licenses/SkiaSharp-3.119.4-LICENSE.txt` |
| `libSkiaSharp.dll`（原生 Skia 與其依賴） | SkiaSharp.NativeAssets.Win32 3.119.4 | MIT＋上游元件各自條款（Skia、libpng、expat、FreeType、zlib 等） | 見檔案 | `licenses/SkiaSharp.NativeAssets.Win32-3.119.4-LICENSE.txt`、`…-THIRD-PARTY-NOTICES.txt` |
| `HarfBuzzSharp.dll` | HarfBuzzSharp 8.3.1.3 | MIT | © Microsoft Corporation；Xamarin, Inc. | `licenses/HarfBuzzSharp-8.3.1.3-LICENSE.txt` |
| `libHarfBuzzSharp.dll`（原生 HarfBuzz） | HarfBuzzSharp.NativeAssets.Win32 8.3.1.3 | MIT＋HarfBuzz 等上游條款 | 見檔案 | `licenses/HarfBuzzSharp.NativeAssets.Win32-8.3.1.3-LICENSE.txt`、`…-THIRD-PARTY-NOTICES.txt` |
| `av_libglesv2.dll`（ANGLE） | Avalonia.Angle.Windows.Natives 2.1.27548.20260419 | BSD-3-Clause 型（套件內附 LICENSE） | Copyright 2018 The ANGLE Project Authors（套件 nuspec：The AvaloniaUI Project） | `licenses/Avalonia.Angle.Windows.Natives-2.1.27548.20260419-LICENSE` |
| `GameClockOverlay.Avalonia.exe` 的啟動器部分 | .NET apphost | MIT | .NET Foundation and Contributors | `licenses/dotnet-runtime-LICENSE.txt`、`dotnet-runtime-THIRD-PARTY-NOTICES.txt` |

## 其他說明

- **不附帶 .NET Runtime。** 本程式是 framework-dependent 版本，執行時需要另外安裝 .NET 11（Microsoft.NETCore.App 11.0.0）。.NET 的授權與通知只因 apphost 啟動器而附上。
- `licenses/` 內 SkiaSharp／HarfBuzzSharp 的 Linux、macOS、WebAssembly 原生套件授權檔，是 NuGet 還原時一併解析的同版本條文。win-x64 輸出不包含這些平台的原生檔案，保留只供參考。
- `licenses/Apache-2.0.txt` 是 SPDX 原文，上述通知檔引用的部分上游元件使用此授權。
- 建置工具 Avalonia.BuildServices 11.3.2（MIT）只在建置時使用，不會複製到輸出。
- 上游通知檔中的 `<year>` 等字樣是原作者條文本身的內容（例如 FreeType 的使用說明），依原文保留、不修改。
- RTSS（RivaTuner Statistics Server）是使用者另外安裝的外部程式。本程式不附帶、不散布 RTSS 或其 SDK 的任何檔案。
- 本檔對照的是目前的 Release 輸出。製作正式公開套件時，請以最終 ZIP 的實際檔案再核對一次。
