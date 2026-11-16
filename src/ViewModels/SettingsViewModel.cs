using System.ComponentModel;
using System.Runtime.CompilerServices;
using GameClockOverlay.AvaloniaApp.Models;

namespace GameClockOverlay.AvaloniaApp.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    public ClockSettings Settings { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? Changed;
    public SettingsViewModel(ClockSettings settings) => Settings=settings;
    private void Change(Action change,[CallerMemberName]string name="") { change();Settings.Normalize();PropertyChanged?.Invoke(this,new(name));Changed?.Invoke(name); }
    public void Replace(ClockSettings value){Settings=value;PropertyChanged?.Invoke(this,new(null));Changed?.Invoke("Import");}
    public void Recover(){Settings.ClickThrough=false;Settings.ClockVisible=true;Settings.Normalize();PropertyChanged?.Invoke(this,new(null));Changed?.Invoke("Recover");}
    public void NotifyExternal(){PropertyChanged?.Invoke(this,new(null));Changed?.Invoke("External");}
    public bool ClockVisible {get=>Settings.ClockVisible;set=>Change(()=>Settings.ClockVisible=value);}
    public bool ShowSeconds {get=>Settings.ShowSeconds;set=>Change(()=>Settings.ShowSeconds=value);}
    public bool TwelveHour {get=>Settings.TwelveHour;set=>Change(()=>Settings.TwelveHour=value);}
    public bool Topmost {get=>Settings.Topmost;set=>Change(()=>Settings.Topmost=value);}
    public bool ClickThrough {get=>Settings.ClickThrough;set=>Change(()=>Settings.ClickThrough=value);}
    public bool GameClockEnabled {get=>Settings.GameClockEnabled;set=>Change(()=>Settings.GameClockEnabled=value);}
    public string GameClockPosition {get=>Settings.GameClockPosition;set=>Change(()=>Settings.GameClockPosition=value);}
    public bool UseMonitorBounds{get=>Settings.UseMonitorBounds;set=>Change(()=>Settings.UseMonitorBounds=value);}
    public double FontSize {get=>Settings.FontSize;set=>Change(()=>Settings.FontSize=value);}
    public double Opacity {get=>Settings.Opacity;set=>Change(()=>Settings.Opacity=value);}
    public string FontFamily {get=>Settings.FontFamily;set=>Change(()=>Settings.FontFamily=value);}
    public string TextColor {get=>Settings.TextColor;set=>Change(()=>Settings.TextColor=value);}
    public string Position {get=>Settings.Position;set=>Change(()=>Settings.Position=value);}
    public string MonitorDevice {get=>Settings.MonitorDevice;set=>Change(()=>Settings.MonitorDevice=value);}
    public int OffsetX {get=>Settings.OffsetX;set=>Change(()=>Settings.OffsetX=value);}
    public int OffsetY {get=>Settings.OffsetY;set=>Change(()=>Settings.OffsetY=value);}
    public string Language {get=>Settings.Language;set=>Change(()=>Settings.Language=value);}
    public string Theme {get=>Settings.Theme;set=>Change(()=>Settings.Theme=value);}
}
