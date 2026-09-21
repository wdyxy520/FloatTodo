using System.ComponentModel;
using FloatTodo.ViewModels;

namespace FloatTodo.WinUI.Views;

public sealed partial class MainPage : Page
{
    private TodayViewModel? _vm;
    public MainPage()
    {
        InitializeComponent();
        Animation.ControlTransitions.Attach(CardsList);
        DataContextChanged += (_, _) => Attach(); Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }
    private void Detach()
    {
        if (_vm is not null)
        {
            _vm.Memos.CollectionChanged -= Memos_CollectionChanged;
            _vm = null;
        }
    }
    private void Attach()
    {
        if (_vm == DataContext) return;
        Detach();
        _vm = DataContext as TodayViewModel;
        if (_vm is not null)
        {
            _vm.Memos.CollectionChanged += Memos_CollectionChanged;
        }
    }
    private void Memos_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewStartingIndex == 0)
        {
            ScrollToTop();
        }
    }
    public void ScrollToTop()
    {
        if (CardsList.Items.Count > 0)
        {
            CardsList.ScrollIntoView(CardsList.Items[0]);
        }
    }

    private void CardLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MemoCard card)
        {
            Animation.ControlTransitions.Attach(card);
        }
    }

    private void CardUnloaded(object sender, RoutedEventArgs e)
    {
    }

    private void CardsList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (_vm?.IsEditing == true)
        {
            e.Cancel = true;
            return;
        }
        DragContext.ActiveSource = sender;
    }

    private void CardsList_DragEnter(object sender, DragEventArgs e)
    {
        if (_vm?.IsEditing == true || !ReferenceEquals(DragContext.ActiveSource, sender))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
            e.Handled = true;
        }
    }

    private void CardsList_DragOver(object sender, DragEventArgs e)
    {
        if (_vm?.IsEditing == true || !ReferenceEquals(DragContext.ActiveSource, sender))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
            e.Handled = true;
        }
    }

    private void CardsList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        DragContext.ActiveSource = null;
        _vm?.SyncSavedMemosOrder();
    }
}
