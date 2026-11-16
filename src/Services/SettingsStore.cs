using System.Text.Json;
using GameClockOverlay.AvaloniaApp.Models;

namespace GameClockOverlay.AvaloniaApp.Services;

public sealed class SettingsStore(string path)
{
    public string Path { get; } = path;
    public string? Error { get; private set; }
    public bool Recovered { get; private set; }
    private bool blocked;
    public static string DefaultPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GameClockOverlay-Avalonia","settings.json");
    public static string LegacyPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GameClockOverlay","settings.json");
    public ClockSettings Load()
    {
        if (!File.Exists(Path)) return new();
        try { return Read(Path); }
        catch (Exception ex) when (IsReadError(ex))
        {
            Error = ex.Message;
            try { File.Copy(Path,Path+".invalid-"+Guid.NewGuid().ToString("N"),false); }
            catch (Exception copy) when (IsReadError(copy)) { blocked = true; Error += " / "+copy.Message; }
            try { var backup=Read(Path+".bak"); Recovered=true; return backup; }
            catch (Exception backup) when (IsReadError(backup)) { return new(); }
        }
    }
    public static ClockSettings Read(string path)
    {
        byte[] bytes;
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(input.Length>65536) throw new JsonException("Settings exceed 64 KB.");
            bytes=new byte[input.Length];input.ReadExactly(bytes);
        }
        var value=JsonSerializer.Deserialize<ClockSettings>(bytes) ?? throw new JsonException("Empty settings.");
        // Avalonia-1／Avalonia-2／v0.2 的設定檔沒有遊戲內時鐘位置：保留原本的 RTSS 位置，不套用新預設的右上角。
        using(var document=JsonDocument.Parse(bytes))
            if(document.RootElement.ValueKind==JsonValueKind.Object && !document.RootElement.TryGetProperty(nameof(ClockSettings.GameClockPosition),out _))
                value.GameClockPosition="Rtss";
        value.Normalize(); return value;
    }
    private readonly Lock gate=new();
    private bool finished;
    // 背景保存與結束工作階段時的同步保存可能同時發生；以鎖序列化，最後一次保存後拒絕較舊快照覆寫。
    public bool Save(ClockSettings settings){lock(gate){return SaveCore(settings);}}
    public bool SaveFinal(ClockSettings settings){lock(gate){bool ok=SaveCore(settings);finished=true;return ok;}}
    private bool SaveCore(ClockSettings settings)
    {
        if(blocked || finished) return false;
        var value=settings.Copy(); value.Normalize();
        string temp=Path+".tmp-"+Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            { JsonSerializer.Serialize(output,value,new JsonSerializerOptions{WriteIndented=true}); output.Flush(true); }
            if(File.Exists(Path)) File.Replace(temp,Path,Recovered ? null : Path+".bak");
            else File.Move(temp,Path);
            Recovered=false; Error=null; return true;
        }
        catch(Exception ex) when(IsReadError(ex)){Error=ex.Message;return false;}
        finally { try { if(File.Exists(temp)) File.Delete(temp); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
    }
    private static bool IsReadError(Exception ex) => ex is IOException or UnauthorizedAccessException or JsonException;
}
