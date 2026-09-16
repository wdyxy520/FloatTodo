using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FloatTodo.Wpf.Controls;

public partial class TitleBar : UserControl
{
    public event Action? SettingsRequested;
    public event Action? PinToggled;
    public event Action? MinimizeRequested;
    public event Action? CloseRequested;

    private bool _isPinned = true;

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            _isPinned = value;
            UpdatePinVisual();
        }
    }

    public TitleBar()
    {
        InitializeComponent();
        UpdatePinVisual();
    }

    private void UpdatePinVisual()
    {
        if (PinIcon == null) return;

        if (_isPinned)
        {
            PinIcon.IconVariant = FluentIcons.Common.IconVariant.Filled;
            PinIcon.Foreground = (Brush)Application.Current.FindResource("AccentBrush");
            PinButton.ToolTip = "常驻置顶中 (Topmost: ON)";
        }
        else
        {
            PinIcon.IconVariant = FluentIcons.Common.IconVariant.Regular;
            PinIcon.Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush");
            PinButton.ToolTip = "已取消置顶 (Topmost: OFF)";
        }
    }

    public UIElement DragElement => DragArea;

    private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke();
    }

    private void OnPinButtonClick(object sender, RoutedEventArgs e)
    {
        IsPinned = !IsPinned;
        PinToggled?.Invoke();
    }

    private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
    {
        MinimizeRequested?.Invoke();
        var win = Window.GetWindow(this);
        win?.Hide();
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
        var win = Window.GetWindow(this);
        win?.Close();
    }
}
