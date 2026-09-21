using System.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using FloatTodo.ViewModels;
using Microsoft.UI.Xaml.Media;

namespace FloatTodo.WinUI.Views;

public sealed partial class MainPage : Page
{
    private TodayViewModel? _vm;
    public TodayViewModel? ViewModel => _vm;

    public MainPage()
    {
        InitializeComponent();
        Animation.ControlTransitions.Attach(CardsList);
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }
    private void Detach()
    {
        if (_vm is not null)
        {
            _vm.Memos.CollectionChanged -= Memos_CollectionChanged;
            _vm = null;
            Bindings.Update();
        }
    }
    private void Attach()
    {
        var target = DataContext as TodayViewModel ?? Ioc.Default.GetService<TodayViewModel>();
        if (_vm == target) return;
        Detach();
        _vm = target;
        if (_vm is not null)
        {
            _vm.Memos.CollectionChanged += Memos_CollectionChanged;
        }
        Bindings.Update();
    }
    private void Memos_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewStartingIndex == 0)
        {
            ScrollToTop();
        }
    }
    private ScrollViewer? _scrollViewer;
    private ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv) return sv;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var result = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (result is not null) return result;
        }
        return null;
    }

    public async Task EnsureAtTopAsync()
    {
        _scrollViewer ??= FindScrollViewer(CardsList);
        if (_scrollViewer is null || _scrollViewer.VerticalOffset <= 0.5) return;

        if (_scrollViewer.VerticalOffset <= 16)
        {
            _scrollViewer.ChangeView(null, 0, null, disableAnimation: true);
            return;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (!e.IsIntermediate && _scrollViewer.VerticalOffset <= 1)
            {
                _scrollViewer.ViewChanged -= OnViewChanged;
                tcs.TrySetResult(true);
            }
        }

        _scrollViewer.ViewChanged += OnViewChanged;
        _scrollViewer.ChangeView(null, 0, null, disableAnimation: false);

        var timeout = Task.Delay(800);
        await Task.WhenAny(tcs.Task, timeout);
        _scrollViewer.ViewChanged -= OnViewChanged;

        if (_scrollViewer.VerticalOffset > 0)
        {
            _scrollViewer.ChangeView(null, 0, null, disableAnimation: true);
        }
    }

    public void ScrollToTop()
    {
        if (CardsList.Items.Count == 0) return;
        _scrollViewer ??= FindScrollViewer(CardsList);
        if (_scrollViewer is not null)
        {
            if (_scrollViewer.VerticalOffset > 0.5)
            {
                _scrollViewer.ChangeView(null, 0, null, disableAnimation: true);
            }
        }
        else
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
