using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GameClockOverlay.AvaloniaApp.Services;

internal static class NativeWindow
{
    internal const int ExStyle=-20, Transparent=0x20, Layered=0x80000, NoActivate=0x08000000;
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)] internal static extern nint GetWindowLongPtr(nint hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)] private static extern nint SetWindowLongPtr(nint hwnd,int index,nint value);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetLayeredWindowAttributes(nint hwnd,uint color,byte alpha,uint flags);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(nint hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(nint hwnd,int id);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint hwnd,out Rect rect);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd,out uint processId);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct MonitorInfo {public uint Size;public Rect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device;}
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point,uint flags);
    [DllImport("user32.dll",EntryPoint="GetMonitorInfoW",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    internal static string DeviceForPoint(Avalonia.PixelPoint point)
    {
        var info=new MonitorInfo{Size=(uint)Marshal.SizeOf<MonitorInfo>(),Device=""};
        return GetMonitorInfo(MonitorFromPoint(new(){X=point.X,Y=point.Y},2),ref info)?info.Device:"";
    }
    internal static void ClickThrough(nint hwnd,bool enabled)
    {
        long current=GetWindowLongPtr(hwnd,ExStyle).ToInt64();
        long updated=(current|NoActivate|Layered)&~Transparent;
        if(enabled) updated|=Transparent;
        Marshal.SetLastPInvokeError(0);
        if(SetWindowLongPtr(hwnd,ExStyle,(nint)updated)==0 && Marshal.GetLastPInvokeError()!=0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        if(!SetLayeredWindowAttributes(hwnd,0,255,2)) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
}
