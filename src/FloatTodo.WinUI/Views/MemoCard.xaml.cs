using System.ComponentModel;
using FloatTodo.ViewModels;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Microsoft.UI.Input;
using Windows.UI.Core;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;
using Microsoft.UI.Xaml.Hosting;

namespace FloatTodo.WinUI.Views;

public sealed partial class MemoCard : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(MemoViewModel),
            typeof(MemoCard),
            new PropertyMetadata(null, (d, e) => ((MemoCard)d).OnViewModelChanged(e.OldValue as MemoViewModel, e.NewValue as MemoViewModel)));

    public MemoViewModel? ViewModel
    {
        get => (MemoViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged(MemoViewModel? oldVm, MemoViewModel? newVm)
    {
        DataContext = newVm;
        Attach();
    }

    private MemoViewModel? _vm;
    private readonly Animation.EditAreaTransition _editTransition;
    private readonly Dictionary<ChecklistItemViewModel, TextBox> _editors = [];
    private ChecklistItemViewModel? _pendingFocusItem;

    public MemoCard()
    {
        InitializeComponent();
        _editTransition = new(EditorActions);
        DataContextChanged += (_, _) => Attach();
        Unloaded += (_, _) => { _editTransition.Stop(); Detach(); };
        Loaded += (_, _) => Attach();
        AddHandler(TappedEvent, new TappedEventHandler(OnCardTapped), handledEventsToo: true);
        ToolTipService.SetToolTip(DeleteCardButton, Shell.Loc.Get("CardDeleteMemo"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(DeleteCardButton, Shell.Loc.Get("CardDeleteMemo"));
        ToolTipService.SetToolTip(DoneButton, Shell.Loc.Get("CardDoneTooltip"));
    }

    private void Detach()
    {
        if (_vm is null) return;
        _vm.PropertyChanged -= Update;
        _vm.FocusRequested -= FocusItem;
        _vm.ReuseFeedbackRequested -= OnReuseFeedback;
        _vm = null;
    }

    private void Attach()
    {
        var target = ViewModel ?? DataContext as MemoViewModel;
        if (_vm == target) return;
        Detach();
        _vm = target;
        if (_vm is null) return;
        _vm.PropertyChanged += Update;
        _vm.FocusRequested += FocusItem;
        _vm.ReuseFeedbackRequested += OnReuseFeedback;
        Refresh();
        if (_vm.IsEditing) DispatcherQueue.TryEnqueue(() => FocusItem(_vm?.Items.FirstOrDefault()));
        Bindings.Update();
    }

    private void OnReuseFeedback()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_vm is null) return;
            FocusItem(_vm.Items.FirstOrDefault());
            PlayReuseFeedback();
        });
    }

    private void PlayReuseFeedback()
    {
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(CardSurface);
            var compositor = visual.Compositor;
            var anim = compositor.CreateScalarKeyFrameAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(300);
            anim.InsertKeyFrame(0.0f, 1.0f);
            anim.InsertKeyFrame(0.5f, 0.45f);
            anim.InsertKeyFrame(1.0f, 1.0f);
            visual.StartAnimation("Opacity", anim);
        }
        catch
        {
        }
    }

    private void Update(object? sender, PropertyChangedEventArgs e)
    {
        Refresh();
        if (e.PropertyName == nameof(MemoViewModel.IsEditing) && _vm?.IsEditing == true)
            DispatcherQueue.TryEnqueue(() => FocusItem(_vm.Items.FirstOrDefault()));
    }

    private void Refresh()
    {
        var editing = _vm?.IsEditing == true;
        Editor.IsTabStop = !editing;
        _editTransition.SetExpanded(editing);
    }

    private void OnCardFlyoutOpened(object? sender, object e) => Interop.NativeMethods.ResetCursor();

    private void OnCardTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_vm?.IsEditing != false) return;

        for (var node = e.OriginalSource as DependencyObject; node is not null && node != this;
             node = VisualTreeHelper.GetParent(node))
            if (node is CheckBox || node is ButtonBase) return;

        var clicked = (e.OriginalSource as FrameworkElement)?.DataContext as ChecklistItemViewModel;
        _vm.BeginEditCommand.Execute(null);
        e.Handled = true;
        if (clicked is not null) FocusItem(clicked);
    }

    private void OnEditorPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_vm?.IsEditing != true) return;
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            _vm.FinishEditCommand.Execute(null);
            return;
        }
        if (e.Key == VirtualKey.Enter &&
            InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
        {
            e.Handled = true;
            _vm.FinishEditCommand.Execute(null);
            return;
        }
        if (e.Key == VirtualKey.Enter && ReferenceEquals(e.OriginalSource, TitleEditor))
        {
            e.Handled = true;
            if (_vm.IsChecklist) FocusItem(_vm.Items.FirstOrDefault());
            else
            {
                BodyEditor.Focus(FocusState.Programmatic);
                BodyEditor.Select(BodyEditor.Text.Length, 0);
            }
            return;
        }
        if (e.Key == VirtualKey.Down && ReferenceEquals(e.OriginalSource, TitleEditor))
        {
            if (_vm.IsChecklist && _vm.Items.Count > 0)
            {
                e.Handled = true;
                NavigateToItem(_vm.Items[0], TitleEditor.SelectionStart);
                return;
            }
        }
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Editor) && _vm?.IsEditing == false && (e.Key is VirtualKey.Enter or VirtualKey.Space))
        {
            _vm.BeginEditCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void CheckboxTapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    private void ItemLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box && box.DataContext is ChecklistItemViewModel item)
        {
            box.PlaceholderText = Shell.Loc.Get("CardAddItemPlaceholder");
            _editors[item] = box;

            if (_pendingFocusItem == item && _vm?.IsEditing == true)
            {
                _pendingFocusItem = null;
                DispatcherQueue.TryEnqueue(() =>
                {
                    box.Focus(FocusState.Programmatic);
                    box.Select(0, 0);
                });
            }
        }
    }

    private void ItemUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box && box.DataContext is ChecklistItemViewModel item) _editors.Remove(item);
    }


    private void EditableItems_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        DragContext.ActiveSource = EditableItems;
    }

    private void EditableItems_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        DragContext.ActiveSource = null;
        _vm?.SyncOrderAfterReorder();
    }

    private void OnHandleTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement handle && handle.ContextFlyout is MenuFlyout flyout)
        {
            flyout.ShowAt(handle);
            e.Handled = true;
        }
    }

    private void OnDragHandlePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
            border.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
    }

    private void OnDragHandlePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
            border.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private void FocusItem(ChecklistItemViewModel? item) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_vm?.IsEditing != true) return;
        _pendingFocusItem = item;
        if (item is not null && _editors.TryGetValue(item, out var box))
        {
            _pendingFocusItem = null;
            box.Focus(FocusState.Programmatic);
            box.Select(box.Text.Length, 0);
        }
        else if (!_vm.IsChecklist)
        {
            _pendingFocusItem = null;
            void FocusBody()
            {
                if (_vm?.IsEditing == true && !_vm.IsChecklist)
                {
                    BodyEditor.Focus(FocusState.Programmatic);
                    BodyEditor.Select(BodyEditor.Text.Length, 0);
                }
            }

            if (!BodyEditor.IsLoaded)
            {
                RoutedEventHandler onLoaded = null!;
                onLoaded = (_, _) =>
                {
                    BodyEditor.Loaded -= onLoaded;
                    DispatcherQueue.TryEnqueue(FocusBody);
                };
                BodyEditor.Loaded += onLoaded;
            }
            else
            {
                FocusBody();
            }
        }
    });

    private void RowLoaded(object sender, RoutedEventArgs e)
    {
    }

    private void RowUnloaded(object sender, RoutedEventArgs e)
    {
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is ChecklistItemViewModel item && _vm is not null)
        {
            _vm.MoveItem(item, -1);
            FocusItem(item);
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is ChecklistItemViewModel item && _vm is not null)
        {
            _vm.MoveItem(item, 1);
            FocusItem(item);
        }
    }

    private void NavigateToItem(ChecklistItemViewModel targetItem, int preferredSelectionStart)
    {
        if (_editors.TryGetValue(targetItem, out var targetBox))
        {
            targetBox.Focus(FocusState.Programmatic);
            targetBox.Select(Math.Min(preferredSelectionStart, targetBox.Text.Length), 0);
        }
        else
        {
            FocusItem(targetItem);
        }
    }

    private void ItemPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not ChecklistItemViewModel item || _vm is null) return;
        var alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);

        if (alt && e.Key == VirtualKey.Up)
        {
            _vm.MoveItem(item, -1);
            e.Handled = true;
            FocusItem(item);
            return;
        }
        if (alt && e.Key == VirtualKey.Down)
        {
            _vm.MoveItem(item, 1);
            e.Handled = true;
            FocusItem(item);
            return;
        }
        if (!alt && e.Key == VirtualKey.Up)
        {
            e.Handled = true;
            var index = _vm.Items.IndexOf(item);
            if (index > 0)
            {
                NavigateToItem(_vm.Items[index - 1], box.SelectionStart);
            }
            else if (_vm.ShowTitle && TitleEditor.Visibility == Visibility.Visible)
            {
                TitleEditor.Focus(FocusState.Programmatic);
                TitleEditor.Select(Math.Min(box.SelectionStart, TitleEditor.Text.Length), 0);
            }
            else
            {
                box.Select(0, 0);
            }
            return;
        }
        if (!alt && e.Key == VirtualKey.Down)
        {
            e.Handled = true;
            var index = _vm.Items.IndexOf(item);
            if (index >= 0 && index < _vm.Items.Count - 1)
            {
                NavigateToItem(_vm.Items[index + 1], box.SelectionStart);
            }
            else
            {
                box.Select(box.Text.Length, 0);
            }
            return;
        }
        if (e.Key == VirtualKey.Enter)
        {
            var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            if (ctrl) return;

            e.Handled = true;
            _vm.SplitItem(item, box.SelectionStart, box.SelectionLength);
            return;
        }
        if (e.Key == VirtualKey.Back && !item.HasText && _vm.Items.Count > 1)
        {
            e.Handled = true;
            _vm.RemoveItem(item);
            return;
        }
    }
}
