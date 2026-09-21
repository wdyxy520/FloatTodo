using FloatTodo.ViewModels;

namespace FloatTodo.WinUI.Views;

public sealed partial class CompactMemoCard : UserControl
{
    public MemoViewModel? ViewModel => DataContext as MemoViewModel;

    public CompactMemoCard()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }
}
