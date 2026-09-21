using System.Runtime.InteropServices;
using FloatTodo.WinUI.Interop;

namespace FloatTodo.WinUI.Shell;

internal sealed class TrayIconService : IDisposable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct IconData
    {
        public int Size; public nint Window; public uint Id, Flags, Callback; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint message, ref IconData data);
    [DllImport("user32.dll")] private static extern nint LoadIcon(nint instance, nint name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadImage(nint hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint hIcon);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, out nint phiconLarge, out nint phiconSmall, uint nIcons);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);

    private IconData _data;
    private readonly WindowMessageHook _hook;
    private readonly uint _taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");
    private readonly bool _ownsIcon;

    public event Action? OpenRequested, HideRequested, SettingsRequested, ExitRequested, PreviewRequested;
    public Func<bool>? IsPreview { get; set; }

    public TrayIconService(nint hwnd)
    {
        var icon = LoadTrayIcon(out _ownsIcon);
        _data = new() { Size = Marshal.SizeOf<IconData>(), Window = hwnd, Id = 1, Flags = 7, Callback = 0x8001, Icon = icon, Tip = "FloatTodo", Info = "", InfoTitle = "" };
        Add();
        _hook = new(hwnd);
        _hook.Message += (msg, _, lp) =>
        {
            if (msg == _taskbarCreated) Add();
            else if (msg == 0x8001)
            {
                if (lp == 0x202) OpenRequested?.Invoke();
                else if (lp == 0x205) Menu();
            }
        };
    }

    private static nint LoadTrayIcon(out bool ownsIcon)
    {
        ownsIcon = false;
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (System.IO.File.Exists(iconPath))
        {
            var cx = GetSystemMetrics(49 /*SM_CXSMICON*/);
            var cy = GetSystemMetrics(50 /*SM_CYSMICON*/);
            if (cx <= 0) cx = 16;
            if (cy <= 0) cy = 16;
            var hIcon = LoadImage(0, iconPath, 1 /*IMAGE_ICON*/, cx, cy, 0x00000010 /*LR_LOADFROMFILE*/);
            if (hIcon != 0)
            {
                ownsIcon = true;
                return hIcon;
            }
        }
        var procPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(procPath) && System.IO.File.Exists(procPath))
        {
            if (ExtractIconEx(procPath, 0, out _, out var hSmall, 1) > 0 && hSmall != 0)
            {
                ownsIcon = true;
                return hSmall;
            }
        }
        return LoadIcon(0, 32512);
    }

    private void Add()
    {
        if (!Shell_NotifyIcon(0, ref _data)) System.Diagnostics.Debug.WriteLine("Tray icon unavailable");
    }

    private void Menu()
    {
        var menu = CreatePopupMenu();
        try
        {
            AppendMenu(menu, 0, 1, Loc.Get("TrayOpen"));
            AppendMenu(menu, 0, 5, IsPreview?.Invoke() == true ? Loc.Get("TrayExitPreview") : Loc.Get("TrayEnterPreview"));
            if (IsPreview?.Invoke() != true) AppendMenu(menu, 0, 2, Loc.Get("TrayHide"));
            AppendMenu(menu, 0, 3, Loc.Get("TraySettings"));
            AppendMenu(menu, 0x800, 0, "");
            AppendMenu(menu, 0, 4, Loc.Get("TrayExit"));
            NativeMethods.GetCursorPos(out var p);
            NativeMethods.SetForegroundWindow(_data.Window);
            var result = TrackPopupMenu(menu, 0x100 | 0x2, p.X, p.Y, 0, _data.Window, 0);
            switch (result)
            {
                case 1: OpenRequested?.Invoke(); break;
                case 2: HideRequested?.Invoke(); break;
                case 3: SettingsRequested?.Invoke(); break;
                case 4: ExitRequested?.Invoke(); break;
                case 5: PreviewRequested?.Invoke(); break;
            }
            NativeMethods.PostMessage(_data.Window, 0, 0, 0);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        Shell_NotifyIcon(2, ref _data);
        if (_ownsIcon && _data.Icon != 0)
        {
            DestroyIcon(_data.Icon);
            _data.Icon = 0;
        }
        _hook.Dispose();
    }
}

internal sealed class GlobalHotKeyService : IDisposable
{
    private readonly nint _hwnd;
    private readonly WindowMessageHook _hook;
    public bool IsRegistered { get; }

    public GlobalHotKeyService(nint hwnd, Action activated)
    {
        _hwnd = hwnd;
        IsRegistered = NativeMethods.RegisterHotKey(hwnd, 0x4654, 0x4000 | 0x2 | 0x1, 0x4E);
        _hook = new(hwnd);
        _hook.Message += (m, w, _) =>
        {
            if (m == 0x312 && w == 0x4654) activated();
        };
    }

    public void Dispose()
    {
        if (IsRegistered) NativeMethods.UnregisterHotKey(_hwnd, 0x4654);
        _hook.Dispose();
    }
}
