using FloatTodo.Core.Models;
using FloatTodo.Core.Windowing;
using FloatTodo.WinUI.Interop;
namespace FloatTodo.WinUI.Windowing;

internal sealed class EdgeHandleController : IDisposable
{
    public nint Handle { get; }
    private readonly WindowMessageHook _hook;
    public event Action? Hovered;
    public bool IsVisible { get; private set; }
    public EdgeHandleController()
    {
        Handle = NativeMethods.CreateWindowEx(0x08000080, "STATIC", "FloatTodo Edge", 0x80000000 | 0x00000107, -100, -100, 6, 200, 0, 0, 0, 0);
        if (Handle == 0) throw new InvalidOperationException("无法创建边缘把手");
        _hook = new(Handle); _hook.Message += (msg, _, _) => { if (msg == 0x200) Hovered?.Invoke(); };
    }
    public void Show(PixelRect main, DockSide side)
    {
        if (side == DockSide.None) return;
        const int thickness = 6;
        if (side == DockSide.Top)
        {
            NativeMethods.SetWindowPos(Handle, -1, main.Left, main.Top, main.Width, thickness, 0x50);
        }
        else
        {
            NativeMethods.SetWindowPos(Handle, -1, side == DockSide.Left ? main.Left : main.Right - thickness, main.Top, thickness, main.Height, 0x50);
        }
        IsVisible = true;
    }
    public void Hide()
    {
        NativeMethods.ShowWindow(Handle, 0);
        IsVisible = false;
    }
    public bool Contains(int x, int y)
    {
        if (!IsVisible) return false;
        NativeMethods.GetWindowRect(Handle, out var r);
        return x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom;
    }
    public void Dispose() { _hook.Dispose(); NativeMethods.DestroyWindow(Handle); }
}
