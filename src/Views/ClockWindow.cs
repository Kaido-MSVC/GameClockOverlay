using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using GameClockOverlay.AvaloniaApp.Models;
using GameClockOverlay.AvaloniaApp.Services;

namespace GameClockOverlay.AvaloniaApp.Views;

public sealed class ClockWindow : Window
{
    private readonly Func<ClockSettings> settings;
    private readonly Action changed,recover;
    private readonly DispatcherTimer timer=new();
    private readonly TextBlock digits=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private PointerPressedEventArgs? press;
    private PixelPoint down;
    private bool placing,placementQueued,closed,dragging,dragCancelled,initialized;
    private NativeWindow.Rect before;
    // Win32 RegisterHotKey：MOD_ALT=1、MOD_CONTROL=2、MOD_SHIFT=4、MOD_NOREPEAT=0x4000；VK_F10=0x79。
    // 與 WPF v0.2.0 相同組合；介面文字一律由這兩個常數產生，避免說明與實際註冊不一致。
    public const uint HotkeyModifiers=0x4000|1|2|4, HotkeyKey=0x79;
    public static string HotkeyLabel=>Describe(HotkeyModifiers,HotkeyKey);
    internal static string Describe(uint modifiers,uint key)
    {
        List<string> parts=[];
        if((modifiers&2)!=0)parts.Add("Ctrl");
        if((modifiers&1)!=0)parts.Add("Alt");
        if((modifiers&4)!=0)parts.Add("Shift");
        if((modifiers&8)!=0)parts.Add("Win");
        parts.Add(key is >=0x70 and <=0x87?"F"+(key-0x6F):((char)key).ToString());
        return string.Join(" + ",parts);
    }
    public bool HotkeyAvailable {get;private set;}
    public int HotkeyError {get;private set;}
    public int TickCount {get;private set;}
    public string TimeText => digits.Text??"";
    public bool TimerRunning => timer.IsEnabled;
    public nint Handle => TryGetPlatformHandle()?.Handle??0;
    public ClockWindow(Func<ClockSettings> settings,Action changed,Action recover)
    {
        this.settings=settings;this.changed=changed;this.recover=recover;
        Title="GameClock Avalonia · Desktop clock";
        WindowDecorations=WindowDecorations.None;CanResize=false;ShowInTaskbar=false;ShowActivated=false;
        Background=Brushes.Transparent;TransparencyLevelHint=[WindowTransparencyLevel.Transparent];
        digits.Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=4,OffsetX=1,OffsetY=2,Opacity=.9};
        Content=new Border{Background=new SolidColorBrush(Color.FromArgb(1,0,0,0)),Child=digits};
        Win32Properties.AddWndProcHookCallback(this,Messages);
        Opened+=(_,_)=>
        {
            if(!initialized)
            {
                HotkeyAvailable=NativeWindow.RegisterHotKey(Handle,1,HotkeyModifiers,HotkeyKey);
                if(!HotkeyAvailable)HotkeyError=System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
                initialized=true;
            }
            if(!HotkeyAvailable) settings().ClickThrough=false;
            Apply();QueuePlacement();
        };
        Screens.Changed+=ScreensChanged;
        ScalingChanged+=(_,_)=>QueuePlacement();
        SizeChanged+=(_,_)=>QueuePlacement();
        PointerPressed+=(_,e)=>
        {
            if(settings().ClickThrough)return;
            if(e.GetCurrentPoint(this).Properties.IsRightButtonPressed || e.ClickCount==2){recover();return;}
            if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed){press=e;down=this.PointToScreen(e.GetPosition(this));e.Pointer.Capture(this);}
        };
        PointerReleased+=(_,e)=>{press=null;e.Pointer.Capture(null);};
        PointerCaptureLost+=(_,_)=>press=null;
        PointerMoved+=(_,e)=>
        {
            var start=press;
            if(start is null || dragging || settings().ClickThrough)return;
            var now=this.PointToScreen(e.GetPosition(this));
            if(Math.Abs(now.X-down.X)<4*RenderScaling && Math.Abs(now.Y-down.Y)<4*RenderScaling)return;
            press=null;e.Pointer.Capture(null);
            dragging=true;dragCancelled=false;NativeWindow.GetWindowRect(Handle,out before);
            try { BeginMoveDrag(start); }
            finally
            {
                dragging=false;
                if(NativeWindow.GetWindowRect(Handle,out var after) && !dragCancelled && !settings().ClickThrough &&
                    (Math.Abs(after.Left-before.Left)>=4*RenderScaling || Math.Abs(after.Top-before.Top)>=4*RenderScaling))
                {
                    var s=settings();s.Position="Free";s.MonitorDevice="";s.X=after.Left;s.Y=after.Top;Place();changed();
                }
                else {Position=new(before.Left,before.Top);}
            }
        };
        timer.Tick+=(_,_)=>RefreshTime();
        PropertyChanged+=(_,e)=> { if(e.Property==IsVisibleProperty){if(IsVisible)RefreshTime();else timer.Stop();} };
        Closed+=(_,_)=>
        {
            closed=true;timer.Stop();Screens.Changed-=ScreensChanged;
            if(HotkeyAvailable)NativeWindow.UnregisterHotKey(Handle,1);
            Win32Properties.RemoveWndProcHookCallback(this,Messages);
        };
    }
    private void ScreensChanged(object? sender,EventArgs e){QueuePlacement();DisplaysChanged?.Invoke();}
    public event Action? DisplaysChanged;
    private nint Messages(nint hwnd,uint msg,nint wp,nint lp,ref bool handled)
    {
        if(msg==0x312 && wp==1){Dispatcher.UIThread.Post(recover);handled=true;}
        if(msg is 0x7e or 0x1a){if(dragging)dragCancelled=true;QueuePlacement();}
        if(msg is 0x1e or 0x218)Dispatcher.UIThread.Post(RefreshTime);
        if(dragging && (msg==0x1f || (msg==0x100 && wp==0x1b)))dragCancelled=true;
        return 0;
    }
    public void Apply()
    {
        if(closed)return;
        var s=settings();s.Normalize();
        if(!HotkeyAvailable)s.ClickThrough=false;
        Topmost=s.Topmost;digits.FontFamily=new(s.FontFamily);digits.FontSize=s.FontSize;
        digits.Foreground=Brush.Parse(s.TextColor);digits.Opacity=s.Opacity;
        var typeface=new Typeface(digits.FontFamily);
        FormattedText Measure(string text)=>new(text,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,typeface,s.FontSize,Brushes.White);
        char widest='0';double widestWidth=0;
        for(char d='0';d<='9';d++){double w=Measure(d.ToString()).Width;if(w>widestWidth){widestWidth=w;widest=d;}}
        string meridiem=Measure("PM").Width>Measure("AM").Width?"PM":"AM";
        var measure=Measure(ClockMath.WidestSample(ClockMath.Format(DateTime.Now,s),widest,meridiem));
        Width=Math.Ceiling(measure.Width)+24;Height=Math.Ceiling(measure.Height)+20;
        if(Handle!=0)NativeWindow.ClickThrough(Handle,s.ClickThrough);
        if(s.ClockVisible){if(!IsVisible)Show();RefreshTime();QueuePlacement();}else{timer.Stop();Hide();}
    }
    public void RefreshTime()
    {
        timer.Stop();if(closed || !IsVisible)return;
        var now=DateTime.Now;var s=settings();string text=ClockMath.Format(now,s);
        if(digits.Text!=text)digits.Text=text;
        TickCount++;TimeChanged?.Invoke(text);
        timer.Interval=ClockMath.NextUpdate(now,s.ShowSeconds);timer.Start();
    }
    public event Action<string>? TimeChanged;
    public void QueuePlacement()
    {
        if(closed || placementQueued)return;placementQueued=true;
        Dispatcher.UIThread.Post(()=>{placementQueued=false;if(!closed)Place();},DispatcherPriority.Loaded);
    }
    public void Place()
    {
        if(closed || !IsVisible || placing || dragging)return;
        placing=true;
        try
        {
            var s=settings();var screen=Screens.All.FirstOrDefault(x=>DisplayId(x)==s.MonitorDevice)
                ?? (s.Position=="Free" && s.X.HasValue && s.Y.HasValue?Screens.ScreenFromPoint(new(s.X.Value,s.Y.Value)):Screens.ScreenFromWindow(this))
                ?? Screens.Primary ?? Screens.All.FirstOrDefault();
            if(screen is null)return;
            var area=s.UseMonitorBounds?screen.Bounds:screen.WorkingArea;
            var size=new PixelSize((int)Math.Ceiling(Width*screen.Scaling),(int)Math.Ceiling(Height*screen.Scaling));
            Position=ClockMath.Place(area,size,s,screen.Scaling);
            s.X=Position.X;s.Y=Position.Y;
        }
        finally{placing=false;}
    }
    public static string DisplayId(Avalonia.Platform.Screen screen)=>NativeWindow.DeviceForPoint(screen.Bounds.Position);
}
