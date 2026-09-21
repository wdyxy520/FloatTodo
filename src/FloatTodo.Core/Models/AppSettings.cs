namespace FloatTodo.Core.Models;

public enum DockSide
{
    None,
    Left,
    Right,
    Top
}

public enum DockState
{
    Floating,       // 自由悬浮在桌面
    DockedVisible,  // 边缘吸附且完全展开
    DockedHidden    // 边缘吸附且滑出屏幕隐藏（保留 5px 把手）
}

public enum ViewState
{
    Expanded,       // 完整工作视图
    Peek            // 紧凑预览
}

public sealed class WindowSettings
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 560;
    public DockSide DockSide { get; set; } = DockSide.None;
    public DockState DockState { get; set; } = DockState.Floating;
    public ViewState ViewState { get; set; } = ViewState.Expanded;
    public bool Topmost { get; set; } = true;
    public bool AutoHide { get; set; } = true;
}

public enum BackdropType
{
    Mica,       // 常规 Mica (DWMSBT_MAINWINDOW = 2)
    MicaAlt,    // Mica Alt (DWMSBT_TABBEDWINDOW = 4)
    Acrylic,    // 亚克力毛玻璃 (DWMSBT_TRANSIENTWINDOW = 3)
    Solid       // 纯实色 / 经典 Win10 (DWMSBT_NONE = 1)
}

public enum AppTheme
{
    System,     // 跟随系统
    Light,      // 浅色模式
    Dark        // 深色模式
}

public sealed class AppearanceSettings
{
    public double Opacity { get; set; } = 0.94;
    public double PreviewOpacity { get; set; } = 0.75;
    public bool UseMica { get; set; } = true;
    public BackdropType Backdrop { get; set; } = BackdropType.Mica;
    public AppTheme Theme { get; set; } = AppTheme.System;
}

public sealed class UpdateSettings
{
    public bool AutoCheckUpdate { get; set; } = true;
    public bool AutoDownloadUpdate { get; set; } = true;
    public bool InstallOnExit { get; set; } = true;
}

public sealed class AppSettings
{
    public WindowSettings Window { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public UpdateSettings Update { get; set; } = new();
    /// <summary>语言标签，空字符串表示跟随系统。</summary>
    public string Language { get; set; } = "";
}



