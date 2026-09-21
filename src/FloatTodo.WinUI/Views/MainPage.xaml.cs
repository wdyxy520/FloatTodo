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
            _vm.PropertyChanged -= Changed;
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
            _vm.PropertyChanged += Changed;
            _vm.Memos.CollectionChanged += Memos_CollectionChanged;
        }
        UpdateCardsListReorder();
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
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TodayViewModel.IsEditing)) UpdateCardsListReorder();
    }
    private void UpdateCardsListReorder()
    {
        var allow = _vm?.IsEditing != true;
        CardsList.CanReorderItems = allow;
        CardsList.CanDragItems = allow;
        CardsList.AllowDrop = allow;
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
        DragContext.ActiveSource = sender;
    }

    private void CardsList_DragEnter(object sender, DragEventArgs e)
    {
        if (!ReferenceEquals(DragContext.ActiveSource, sender))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
            e.Handled = true;
        }
    }

    private void CardsList_DragOver(object sender, DragEventArgs e)
    {
        if (!ReferenceEquals(DragContext.ActiveSource, sender))
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
