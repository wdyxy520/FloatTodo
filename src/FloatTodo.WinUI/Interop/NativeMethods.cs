using System.Runtime.InteropServices;
namespace FloatTodo.WinUI.Interop;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    internal delegate nint SubclassProc(nint hwnd, uint msg, nuint wp, nint lp, nuint id, nuint data);
    [DllImport("comctl32.dll")] internal static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] internal static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] internal static extern nint DefSubclassProc(nint hwnd, uint msg, nuint wp, nint lp);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint SetCapture(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetCapture();
    [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint CreateWindowEx(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] internal static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] internal static extern bool PostMessage(nint hwnd, uint msg, nuint wp, nint lp);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] internal static extern nint SendMessage(nint hwnd, uint msg, nuint wp, nint lp);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] internal static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    internal delegate bool MonitorEnumProc(nint monitor, nint hdc, ref Rect rect, nint data);
    [DllImport("user32.dll")] internal static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc callback, nint data);
    [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
    [StructLayout(LayoutKind.Sequential)] internal struct Margins { public int Left, Right, Top, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct BlurBehind { public uint Flags; public int Enable; public nint Region; public int Transition; }
    [DllImport("dwmapi.dll")] internal static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);
    [DllImport("dwmapi.dll")] internal static extern int DwmEnableBlurBehindWindow(nint hwnd, ref BlurBehind blur);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint hwnd, uint attribute, ref uint value, int size);
    [DllImport("gdi32.dll")] internal static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] internal static extern nint GetStockObject(int index);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("user32.dll")] internal static extern int FillRect(nint dc, ref Rect rect, nint brush);
    [DllImport("user32.dll")] internal static extern nint LoadCursor(nint hInstance, int lpCursorName);
    [DllImport("user32.dll")] internal static extern nint SetCursor(nint hCursor);
    [DllImport("user32.dll")] internal static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, nuint dwExtraInfo);
    internal static void ResetCursor()
    {
        SetCursor(LoadCursor(0, 32512));
        mouse_event(0x0001, 0, 0, 0, 0);
    }
}
internal sealed class WindowMessageHook : IDisposable
{
    private static long _nextId; private readonly nuint _id = (nuint)Interlocked.Increment(ref _nextId); private readonly nint _hwnd; private readonly NativeMethods.SubclassProc _proc;
    public event Action<uint, nuint, nint>? Message;
    public Func<uint, nuint, nint, nint?>? Filter { get; set; }
    public WindowMessageHook(nint hwnd) { _hwnd = hwnd; _proc = Handle; if (!NativeMethods.SetWindowSubclass(hwnd, _proc, _id, 0)) throw new InvalidOperationException("Window hook failed"); }
    private nint Handle(nint hwnd, uint msg, nuint wp, nint lp, nuint id, nuint data)
    {
        if (Filter?.Invoke(msg, wp, lp) is nint result) return result;
        Message?.Invoke(msg, wp, lp);
        return NativeMethods.DefSubclassProc(hwnd, msg, wp, lp);
    }
    public void Dispose() => NativeMethods.RemoveWindowSubclass(_hwnd, _proc, _id);
}


