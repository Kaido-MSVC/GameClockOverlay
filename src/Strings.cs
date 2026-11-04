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
        ["DependencySummary"] = ["沒有額外 NuGet 套件。EXE 包含 .NET 啟動器（MIT）；WPF／Windows Forms 使用另外安裝的 .NET 10 Windows Desktop Runtime。Runtime 及 Windows 元件各依其條款，並非全部採用 MIT。以下保留原始法律文字。", "No additional NuGet packages. The EXE includes the .NET apphost (MIT); WPF / Windows Forms use a separately installed .NET 10 Windows Desktop Runtime. Runtime and Windows components have their own terms; they are not all MIT. Original legal texts follow.", "追加の NuGet パッケージはありません。EXE は .NET apphost（MIT）を含み、WPF／Windows Forms は別途インストールされた .NET 10 Windows Desktop Runtime を使用します。Runtime と Windows の各コンポーネントには独自の条項があり、すべてが MIT ではありません。以下に法律文書の原文を記載します。"],
        ["Settings"] = ["時鐘設定", "Clock settings", "時計の設定"],
        ["Subtitle"] = ["桌面上的時間，遊戲裡的節奏。", "Keep time in view.", "いつでも、時刻をひと目で。"],
        ["Appearance"] = ["顯示", "Display", "表示"],
        ["Seconds"] = ["顯示秒數", "Show seconds", "秒を表示"],
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
        ["Limit"] = ["視窗模式適用；無邊框全螢幕需逐一實測。\n不保證真正獨佔全螢幕，亦不繞過遊戲限制。", "For windowed apps. Verify borderless games individually.\nExclusive fullscreen is not guaranteed; game restrictions are respected.", "ウィンドウ表示向け。ボーダーレスはゲームごとに確認してください。\n排他フルスクリーンは保証せず、ゲームの制限を回避しません。"],
        ["AlreadyRunning"] = ["程式已在執行。請使用系統匣圖示開啟設定。", "Already running. Open settings from the tray icon.", "起動済みです。トレイアイコンから設定を開いてください。"]
    };
}
