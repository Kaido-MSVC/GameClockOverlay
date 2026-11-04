using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GameClockOverlay;

internal static class Native
{
    internal const int ExStyle = -20, Transparent = 0x20, NoActivate = 0x08000000, ToolWindow = 0x80;
    internal const uint NoSize = 0x0001, NoMove = 0x0002, NoActivateFlag = 0x0010;
    internal static readonly uint RecoveryMessage = RegisterWindowMessage("GameClockOverlay.Recover.v0.1");
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] internal static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint FindWindow(string? className, string name);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostMessage(nint hwnd, uint message, nint wp, nint lp);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyIcon(nint icon);
    internal static void SetClickThrough(nint hwnd, bool enabled)
    {
        long current = GetWindowLongPtr(hwnd, ExStyle).ToInt64();
        long updated = (current | NoActivate | ToolWindow) & ~Transparent;
        if (enabled) updated |= Transparent;
        Marshal.SetLastPInvokeError(0);
        if (SetWindowLongPtr(hwnd, ExStyle, (nint)updated) == 0 && Marshal.GetLastPInvokeError() != 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
}
