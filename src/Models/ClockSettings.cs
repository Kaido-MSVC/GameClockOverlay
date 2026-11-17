using System.Text.RegularExpressions;

namespace GameClockOverlay.AvaloniaApp.Models;

public sealed partial class ClockSettings
{
    public string Language { get; set; } = "auto";
    public bool ShowSeconds { get; set; }
    public double Opacity { get; set; } = .9;
    public bool ClickThrough { get; set; }
    public double FontSize { get; set; } = 32;
    public string MonitorDevice { get; set; } = "";
    public bool UseMonitorBounds { get; set; }
    public string Position { get; set; } = "TopRight";
    public int? X { get; set; }
    public int? Y { get; set; }
    public bool ClockVisible { get; set; } = true;
    public bool Topmost { get; set; } = true;
    public string FontFamily { get; set; } = "Consolas";
    public string TextColor { get; set; } = "#FFFFFF";
    public string Theme { get; set; } = "System";
    public bool TwelveHour { get; set; }
    public int OffsetX { get; set; } = 16;
    public int OffsetY { get; set; } = 16;
    // Avalonia-2：RTSS 遊戲內時鐘。預設關閉；Avalonia-1 與 v0.2 設定檔缺少此欄位時即為 false。
    // 時間格式（ShowSeconds、TwelveHour）與桌面時鐘共用；連線狀態與錯誤屬於執行期資訊，不保存。
    public bool GameClockEnabled { get; set; }
    // Avalonia-2-POSITION：遊戲內時鐘位置，與桌面時鐘的 Position 互相獨立。"Rtss"＝依 RTSS 自身的 OSD 位置（Avalonia-2 原行為）。
    // 全新設定預設右上角；既有設定檔缺少此欄位時，由 SettingsStore.Read 補為 "Rtss"，不改變既有使用者的畫面。
    public string GameClockPosition { get; set; } = "TopRight";
    // RTSS 連動啟動：程式啟動且遊戲內時鐘已啟用時，若 RTSS 未執行就啟動它。預設開啟；舊設定檔缺少此欄位時同樣為開啟。
    public bool AutoStartRtss { get; set; } = true;
    // 手動指定的 RTSS.exe 完整路徑；空字串＝自動偵測。是否存在於執行期檢查，失效時保留原值供介面提示，不自動清除。
    public string RtssPath { get; set; } = "";
    public void Normalize()
    {
        RtssPath = string.IsNullOrWhiteSpace(RtssPath) || RtssPath.Length > 1024 ? "" : RtssPath.Trim();
        // 不提供獨立的左上：RTSS 預設位置即左上，其他值（含 "TopLeft"）一律回到 "Rtss"。
        if (GameClockPosition is not ("Rtss" or "TopRight" or "BottomLeft" or "BottomRight")) GameClockPosition = "Rtss";
        if (Language is not ("auto" or "zh-TW" or "en" or "ja")) Language = "auto";
        if (Position is not ("Free" or "TopLeft" or "TopRight" or "BottomLeft" or "BottomRight")) Position = "TopRight";
        if (Theme is not ("System" or "Dark" or "Light")) Theme = "System";
        FontSize = double.IsFinite(FontSize) ? Math.Clamp(FontSize,16,96) : 32;
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity,.25,1) : .9;
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) || FontFamily.Length > 128 ? "Consolas" : FontFamily.Trim();
        TextColor = TextColor is not null && HexColor().IsMatch(TextColor) ? TextColor.ToUpperInvariant() : "#FFFFFF";
        MonitorDevice ??= "";
        OffsetX = Math.Clamp(OffsetX,-10000,10000); OffsetY = Math.Clamp(OffsetY,-10000,10000);
        X = X is int x ? Math.Clamp(x,-100000,100000) : null;
        Y = Y is int y ? Math.Clamp(y,-100000,100000) : null;
    }
    public ClockSettings Copy() => (ClockSettings)MemberwiseClone();
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();
}
