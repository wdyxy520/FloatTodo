using FloatTodo.ViewModels;

namespace FloatTodo.WinUI.Views;

public sealed partial class DesktopPreviewView : UserControl
{
    public TodayViewModel? ViewModel => DataContext as TodayViewModel;

    public DesktopPreviewView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }
}
