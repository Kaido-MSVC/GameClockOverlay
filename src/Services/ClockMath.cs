using System.Globalization;
using Avalonia;
using GameClockOverlay.AvaloniaApp.Models;

namespace GameClockOverlay.AvaloniaApp.Services;

public static class ClockMath
{
    public static string Format(DateTime now, ClockSettings s) => Format(now,s.ShowSeconds,s.TwelveHour);
    // 桌面時鐘與 RTSS 遊戲內時鐘共用同一格式；InvariantCulture 確保 AM／PM 為 ASCII，符合 RTSS 寫入限制。
    public static string Format(DateTime now, bool seconds, bool twelve) => now.ToString(twelve ? (seconds?"hh:mm:ss tt":"hh:mm tt") : (seconds?"HH:mm:ss":"HH:mm"),CultureInfo.InvariantCulture);
    // 比例字型的數字寬度不一；以最寬數字與最寬 AM／PM 組成樣板量測，避免時間變寬時裁切。
    public static string WidestSample(string formatted,char widestDigit,string widestMeridiem)
    {
        var chars=formatted.ToCharArray();
        for(int i=0;i<chars.Length;i++)if(char.IsAsciiDigit(chars[i]))chars[i]=widestDigit;
        string sample=new(chars);
        return sample.EndsWith(" AM",StringComparison.Ordinal)||sample.EndsWith(" PM",StringComparison.Ordinal)?sample[..^2]+widestMeridiem:sample;
    }
    // WCAG 相對亮度對比；供預覽框依文字色選擇底色。
    public static double ContrastRatio(Avalonia.Media.Color a,Avalonia.Media.Color b)
    {
        static double Channel(byte v){double x=v/255d;return x<=.03928?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
        static double Luminance(Avalonia.Media.Color c)=>.2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);
        double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
    }
    public static TimeSpan NextUpdate(DateTime now,bool seconds)
    {
        long unit=seconds?TimeSpan.TicksPerSecond:TimeSpan.TicksPerMinute;
        return TimeSpan.FromTicks(unit-now.Ticks%unit+TimeSpan.TicksPerMillisecond*15);
    }
    public static PixelPoint Place(PixelRect area, PixelSize size, ClockSettings s, double scale)
    {
        int dx=(int)Math.Round(s.OffsetX*scale),dy=(int)Math.Round(s.OffsetY*scale);
        int x=s.Position=="Free" ? s.X??area.X+dx : s.Position.EndsWith("Right",StringComparison.Ordinal)?area.Right-size.Width-dx:area.X+dx;
        int y=s.Position=="Free" ? s.Y??area.Y+dy : s.Position.StartsWith("Bottom",StringComparison.Ordinal)?area.Bottom-size.Height-dy:area.Y+dy;
        return new(Math.Clamp(x,area.X,Math.Max(area.X,area.Right-size.Width)),Math.Clamp(y,area.Y,Math.Max(area.Y,area.Bottom-size.Height)));
    }
}
