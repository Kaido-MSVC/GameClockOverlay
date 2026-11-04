using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace GameClockOverlay;

public sealed class ClockSettings
{
    public string Language { get; set; } = "auto";
    public bool ShowSeconds { get; set; }
    public double Opacity { get; set; } = 0.9;
    public bool ClickThrough { get; set; }
    public string Position { get; set; } = "TopRight";
    public int? X { get; set; }
    public int? Y { get; set; }
    public void Normalize()
    {
        if (Language is not ("auto" or "zh-TW" or "en" or "ja")) Language = "auto";
        if (Position is not ("Free" or "TopLeft" or "TopRight" or "BottomLeft" or "BottomRight")) Position = "TopRight";
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0.25, 1) : 0.9;
    }
}

public sealed class SettingsStore(string path)
{
    public string Path { get; } = path;
    public string? Error { get; private set; }
    public bool Recovered { get; private set; }
    public ClockSettings Load()
    {
        if (!File.Exists(Path)) return new();
        try { return Read(Path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Error = ex.Message;
            // Keep the original malformed file for manual recovery before the next save.
            try { File.Copy(Path, Path + ".invalid-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture), false); }
            catch (Exception copyEx) when (copyEx is IOException or UnauthorizedAccessException) { Error += " / " + copyEx.Message; }
            try { var backup = Read(Path + ".bak"); Recovered = true; return backup; }
            catch (Exception backupEx) when (backupEx is IOException or UnauthorizedAccessException or JsonException) { return new(); }
        }
    }
    private static ClockSettings Read(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 65536) throw new JsonException("Settings file exceeds 64 KB.");
        var settings = JsonSerializer.Deserialize<ClockSettings>(input) ?? throw new JsonException("Empty settings.");
        settings.Normalize();
        return settings;
    }
    public bool Save(ClockSettings settings)
    {
        settings.Normalize();
        var temp = Path + ".tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(output, settings, new JsonSerializerOptions { WriteIndented = true });
                output.Flush(true);
            }
            if (File.Exists(Path)) File.Replace(temp, Path, Path + ".bak");
            else File.Move(temp, Path);
            Error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Error = ex.Message; return false; }
    }
}
