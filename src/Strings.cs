using System.Globalization;
using System.Collections.Generic;

namespace GameClockOverlay;

public static class Strings
{
    public static string Resolve(string choice, string systemLanguage)
    {
        if (choice is "zh-TW" or "en" or "ja") return choice;
        var name = systemLanguage.ToLowerInvariant();
        return name.StartsWith("zh") ? "zh-TW" : name.StartsWith("ja") ? "ja" : "en";
    }
    public static string Language { get; set; } = "en";
    public static string Get(string key) => Values[key][Language == "zh-TW" ? 0 : Language == "ja" ? 2 : 1];
    public static void Use(string choice) => Language = Resolve(choice, CultureInfo.CurrentUICulture.Name);
    public static readonly Dictionary<string, string[]> Values = new()
    {
        ["About"] = ["關於／授權", "About / licenses", "バージョン情報・ライセンス"],
        ["AboutClose"] = ["關閉", "Close", "閉じる"],
        ["Version"] = ["版本", "Version", "バージョン"],
        ["Author"] = ["作者", "Author", "作者"],
        ["MitLicense"] = ["MIT 授權全文", "MIT license", "MIT ライセンス全文"],
        ["ThirdParty"] = ["第三方授權", "Third-party notices", "サードパーティのライセンス"],
        ["LicenseSummary"] = ["本專案採用 MIT 授權。下方可離線閱讀及複製授權原文。", "This project is licensed under MIT. Read and copy the original license text below, offline.", "本プロジェクトは MIT ライセンスです。以下の原文はオフラインで閲覧・コピーできます。"],
        ["DependencySummary"] = ["沒有額外 NuGet 套件。EXE 包含 .NET 啟動器（MIT）；WPF／Windows Forms 使用另外安裝的 .NET 11 Windows Desktop Runtime。Runtime 及 Windows 元件各依其條款，並非全部採用 MIT。以下保留原始法律文字。", "No additional NuGet packages. The EXE includes the .NET apphost (MIT); WPF / Windows Forms use a separately installed .NET 11 Windows Desktop Runtime. Runtime and Windows components have their own terms; they are not all MIT. Original legal texts follow.", "追加の NuGet パッケージはありません。EXE は .NET apphost（MIT）を含み、WPF／Windows Forms は別途インストールされた .NET 11 Windows Desktop Runtime を使用します。Runtime と Windows の各コンポーネントには独自の条項があり、すべてが MIT ではありません。以下に法律文書の原文を記載します。"],
        ["Settings"] = ["時鐘設定", "Clock settings", "時計の設定"],
        ["Subtitle"] = ["桌面上的時間，遊戲裡的節奏。", "Keep time in view.", "いつでも、時刻をひと目で。"],
        ["Appearance"] = ["顯示", "Display", "表示"],
        ["Seconds"] = ["顯示秒數", "Show seconds", "秒を表示"],
        ["FontSize"] = ["字體大小", "Font size", "文字サイズ"],
        ["Monitor"] = ["顯示螢幕", "Display monitor", "表示モニター"],
        ["CurrentMonitor"] = ["目前位置所在螢幕（可拖曳移動）", "Monitor at current position (drag to move)", "現在位置のモニター（ドラッグで移動）"],
        ["DisconnectedMonitor"] = ["未連接；暫用最近的螢幕：", "Disconnected; using nearest monitor:", "未接続・最寄りのモニターを使用："],
        ["MonitorBounds"] = ["使用整個螢幕邊界（可覆蓋工作列）", "Use full monitor bounds (may cover taskbar)", "画面全体の境界を使用（タスクバーと重なる場合あり）"],
        ["Opacity"] = ["不透明度", "Opacity", "不透明度"],
        ["Position"] = ["位置（目前螢幕）", "Position (current monitor)", "位置（現在の画面）"],
        ["Free"] = ["自由拖曳", "Free drag", "自由にドラッグ"],
        ["TopLeft"] = ["左上", "Top left", "左上"],
        ["TopRight"] = ["右上", "Top right", "右上"],
        ["BottomLeft"] = ["左下", "Bottom left", "左下"],
        ["BottomRight"] = ["右下", "Bottom right", "右下"],
        ["Interaction"] = ["操作", "Interaction", "操作"],
        ["ClickThrough"] = ["滑鼠穿透（鎖定拖曳）", "Click through (lock dragging)", "マウスを透過（ドラッグをロック）"],
        ["Recovery"] = ["Ctrl + Alt + Shift + F10：解除穿透並開啟設定。\n或右鍵點系統匣時鐘圖示，選「解除穿透並設定」。", "Ctrl + Alt + Shift + F10: unlock and open settings.\nOr right-click the tray clock and choose Unlock & settings.", "Ctrl + Alt + Shift + F10：透過を解除して設定を開きます。\nまたはトレイの時計を右クリック →「透過解除・設定」。"],
        ["HotkeyFailed"] = ["快捷鍵已被其他程式使用，滑鼠穿透已停用。", "Hotkey unavailable. Click through has been disabled.", "ショートカットを登録できないため、マウス透過は無効です。"],
        ["Language"] = ["介面語言", "Language", "表示言語"],
        ["Auto"] = ["依系統語言", "System language", "システム言語"],
        ["Saved"] = ["設定已自動儲存", "Changes saved automatically", "設定は自動保存されます"],
        ["SaveFailed"] = ["設定無法儲存；請檢查資料夾寫入權限。", "Cannot save settings. Check folder write permissions.", "保存できません。フォルダーの書き込み権限をご確認ください。"],
        ["LoadFailed"] = ["設定檔無法讀取；已嘗試備份或預設設定。原檔會保留供檢查。", "Settings could not be read. A backup or defaults were loaded; the original is retained for inspection.", "設定を読み込めません。バックアップまたは初期設定を使用します。元のファイルは確認用に保持されます。"],
        ["Close"] = ["關閉設定", "Close settings", "設定を閉じる"],
        ["Exit"] = ["結束程式", "Exit", "終了"],
        ["Unlock"] = ["解除穿透並設定", "Unlock & settings", "透過解除・設定"],
        ["Hint"] = ["拖曳時鐘可移動，右鍵可開啟設定。最小化或關閉設定後，時鐘仍顯示並常駐系統匣；雙擊系統匣可解除穿透並重開設定。", "Drag the clock to move it; right-click for settings. Minimize or close settings to keep the clock running in the tray. Double-click the tray icon to unlock and reopen settings.", "時計をドラッグして移動、右クリックで設定。設定を最小化・閉じても時計は表示され、トレイで動作します。トレイをダブルクリックして透過解除・設定。"],
        ["Limit"] = ["支援桌面／視窗模式；無邊框及 Windows 全螢幕最佳化需逐款驗收。\n不支援可靠覆蓋真正獨佔全螢幕。遊戲的「全螢幕」選項不代表真正獨佔模式。", "Supports desktop / windowed apps. Verify borderless and Windows Fullscreen Optimizations per game.\nReliable overlay in true exclusive fullscreen is unsupported. A game's Fullscreen option does not prove exclusive mode.", "デスクトップ・ウィンドウ表示に対応。ボーダーレスと Windows 全画面表示の最適化はゲームごとに要確認。\n真の排他フルスクリーンへの確実な重ね表示は非対応です。ゲームの「全画面」設定だけでは排他モードを証明できません。"],
        ["OperationFailed"] = ["操作未完成，請重試。錯誤診斷已嘗試寫入設定資料夾的 errors.log。", "The operation could not be completed. Please try again. Diagnostics were attempted in errors.log beside the settings file.", "操作を完了できませんでした。再試行してください。設定フォルダーの errors.log に診断情報の保存を試みました。"],
        ["AlreadyRunning"] = ["程式已在執行。請使用系統匣圖示開啟設定。", "Already running. Open settings from the tray icon.", "起動済みです。トレイアイコンから設定を開いてください。"]
    };
}
