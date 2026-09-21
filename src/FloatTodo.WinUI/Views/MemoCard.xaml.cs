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
    private MemoViewModel? _vm;
    private readonly Animation.EditAreaTransition _editTransition;
    private readonly Dictionary<ChecklistItemViewModel, TextBox> _editors = [];
    private ChecklistItemViewModel? _pendingFocusItem;

    private bool _isPointerDown;
    private bool _isDragging;
    private Point _dragStartPointerPos;
    private double _accumulatedPointerY;
    private ChecklistItemViewModel? _draggedItem;
    private ListViewItem? _draggedContainer;
    private CompositeTransform? _dragTransform;
    private FrameworkElement? _activeHandle;

    public MemoCard()
    {
        InitializeComponent();
        _editTransition = new(EditorActions);
        EditorActions.SizeChanged += (_, e) => EditorActions.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry { Rect = new(0, 0, e.NewSize.Width, e.NewSize.Height) };
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
        _vm.Items.CollectionChanged -= ItemsChanged;
        foreach (var i in _vm.Items) i.PropertyChanged -= ItemChanged;
        _vm = null;
    }

    private void Attach()
    {
        if (_vm == DataContext) return;
        Detach();
        _vm = DataContext as MemoViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += Update;
        _vm.FocusRequested += FocusItem;
        _vm.Items.CollectionChanged += ItemsChanged;
        foreach (var i in _vm.Items) i.PropertyChanged += ItemChanged;
        Refresh();
        if (_vm.IsEditing) DispatcherQueue.TryEnqueue(() => FocusItem(_vm?.Items.FirstOrDefault()));
    }

    private void ItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move) return;
        if (e.OldItems is not null) foreach (ChecklistItemViewModel i in e.OldItems) i.PropertyChanged -= ItemChanged;
        if (e.NewItems is not null) foreach (ChecklistItemViewModel i in e.NewItems) { i.PropertyChanged -= ItemChanged; i.PropertyChanged += ItemChanged; }
        Refresh();
    }

    private void ItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChecklistItemViewModel.HasText))
        {
            if (sender is ChecklistItemViewModel item && _editors.TryGetValue(item, out var box))
                UpdateItemVisuals(item, box, _vm?.IsEditing == true);
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

        SetEditing(TitleEditor, editing);
        SetEditing(BodyEditor, editing);

        foreach (var kvp in _editors)
        {
            var item = kvp.Key;
            var box = kvp.Value;
            SetEditing(box, editing);
            UpdateItemVisuals(item, box, editing);
        }

        AddItemButton.Visibility = _vm is { IsEditing: true, IsChecklist: true } ? Visibility.Visible : Visibility.Collapsed;
        _editTransition.SetExpanded(editing);
    }

    private void UpdateItemVisuals(ChecklistItemViewModel item, TextBox box, bool editing)
    {
        // 查找外层单项 Grid 控制整行显示（未编辑状态下隐藏空白项）
        DependencyObject? parent = box;
        while (parent is not null && parent is not Grid) parent = VisualTreeHelper.GetParent(parent);
        if (parent is not null)
        {
            var container = VisualTreeHelper.GetParent(parent);
            if (container is Grid outerRow)
            {
                outerRow.Visibility = editing || item.HasText ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private static void SetEditing(TextBox box, bool editing)
    {
        box.IsReadOnly = !editing;
        box.IsTabStop = editing;
        box.IsHitTestVisible = editing;
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
            // Tunnel before TextBox inserts a newline or the checklist splits a row.
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
            SetEditing(box, _vm?.IsEditing == true);
            UpdateItemVisuals(item, box, _vm?.IsEditing == true);

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

    private async void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb) return;
        if (!cb.IsLoaded)
        {
            cb.Opacity = 0.55;
            return;
        }

        try
        {
            // 留出打勾矢量路径动画的播放时间（约 200ms）
            await Task.Delay(200);
            if (!cb.IsLoaded || cb.IsChecked != true) return;

            var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = cb.Opacity,
                To = 0.55,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase
                {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut
                }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, cb);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Opacity");
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            sb.Children.Add(anim);
            sb.Begin();
        }
        catch
        {
            cb.Opacity = 0.55;
        }
    }

    private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb) return;
        cb.Opacity = 1.0;
    }

    private void CheckBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb)
        {
            if (cb.DataContext is ChecklistItemViewModel item)
            {
                cb.Opacity = item.IsCompleted ? 0.55 : 1.0;
            }
            cb.DataContextChanged += (s, _) =>
            {
                if (s is CheckBox c && c.DataContext is ChecklistItemViewModel it)
                {
                    c.Opacity = it.IsCompleted ? 0.55 : 1.0;
                }
            };
        }
    }

    private void OnHandlePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_vm?.IsEditing != true || _vm.Items.Count <= 1) return;
        if (sender is not FrameworkElement handle || handle.DataContext is not ChecklistItemViewModel item) return;

        var point = e.GetCurrentPoint(handle);
        if (!point.Properties.IsLeftButtonPressed) return;

        _activeHandle = handle;
        _draggedItem = item;
        _draggedContainer = EditableItems.ContainerFromItem(item) as ListViewItem;
        if (_draggedContainer is null) return;

        _isPointerDown = true;
        _isDragging = false;
        _dragStartPointerPos = e.GetCurrentPoint(EditableItems).Position;
        _accumulatedPointerY = _dragStartPointerPos.Y;

        handle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnHandlePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerDown || _draggedItem is null || _draggedContainer is null || _vm is null) return;

        var currentPoint = e.GetCurrentPoint(EditableItems).Position;
        var deltaFromStart = currentPoint.Y - _dragStartPointerPos.Y;

        if (!_isDragging)
        {
            if (Math.Abs(deltaFromStart) > 3)
            {
                _isDragging = true;
                try { ElementCompositionPreview.GetElementVisual(_draggedContainer).ImplicitAnimations = null; } catch { }
                Canvas.SetZIndex(_draggedContainer, 100);
                if (_draggedContainer.RenderTransform is not CompositeTransform ct)
                {
                    ct = new CompositeTransform();
                    _draggedContainer.RenderTransform = ct;
                }
                _dragTransform = ct;
                _dragTransform.TranslateX = 0; // 严格锁定 X 轴为 0
                _dragTransform.TranslateY = deltaFromStart;
            }
            e.Handled = true;
            return;
        }

        var deltaY = currentPoint.Y - _accumulatedPointerY;
        var currentIndex = _vm.Items.IndexOf(_draggedItem);
        if (currentIndex < 0) return;

        var currentHeight = _draggedContainer.ActualHeight > 0 ? _draggedContainer.ActualHeight : 32.0;

        // 向下拖拽换位
        while (deltaY > currentHeight * 0.5 && currentIndex < _vm.Items.Count - 1)
        {
            var nextItem = _vm.Items[currentIndex + 1];
            if (nextItem.IsCompleted != _draggedItem.IsCompleted) break;
            var targetContainer = EditableItems.ContainerFromItem(nextItem) as ListViewItem;
            var step = targetContainer?.ActualHeight > 0 ? targetContainer.ActualHeight : currentHeight;
            _accumulatedPointerY += step;
            _vm.MoveItem(_draggedItem, 1);
            currentIndex++;
            _draggedContainer = EditableItems.ContainerFromItem(_draggedItem) as ListViewItem;
            if (_draggedContainer is not null)
            {
                try { ElementCompositionPreview.GetElementVisual(_draggedContainer).ImplicitAnimations = null; } catch { }
                Canvas.SetZIndex(_draggedContainer, 100);
                _draggedContainer.RenderTransform = _dragTransform;
            }
            deltaY = currentPoint.Y - _accumulatedPointerY;
        }

        // 向上拖拽换位
        while (deltaY < -currentHeight * 0.5 && currentIndex > 0)
        {
            var prevItem = _vm.Items[currentIndex - 1];
            if (prevItem.IsCompleted != _draggedItem.IsCompleted) break;
            var targetContainer = EditableItems.ContainerFromItem(prevItem) as ListViewItem;
            var step = targetContainer?.ActualHeight > 0 ? targetContainer.ActualHeight : currentHeight;
            _accumulatedPointerY -= step;
            _vm.MoveItem(_draggedItem, -1);
            currentIndex--;
            _draggedContainer = EditableItems.ContainerFromItem(_draggedItem) as ListViewItem;
            if (_draggedContainer is not null)
            {
                try { ElementCompositionPreview.GetElementVisual(_draggedContainer).ImplicitAnimations = null; } catch { }
                Canvas.SetZIndex(_draggedContainer, 100);
                _draggedContainer.RenderTransform = _dragTransform;
            }
            deltaY = currentPoint.Y - _accumulatedPointerY;
        }

        // 边界阻尼
        var maxDamp = currentHeight * 0.6;
        if (deltaY > maxDamp && (currentIndex == _vm.Items.Count - 1 || _vm.Items[currentIndex + 1].IsCompleted != _draggedItem.IsCompleted))
        {
            deltaY = maxDamp + (deltaY - maxDamp) * 0.2;
        }
        else if (deltaY < -maxDamp && (currentIndex == 0 || _vm.Items[currentIndex - 1].IsCompleted != _draggedItem.IsCompleted))
        {
            deltaY = -maxDamp + (deltaY + maxDamp) * 0.2;
        }

        if (_dragTransform is not null)
        {
            _dragTransform.TranslateX = 0;
            _dragTransform.TranslateY = deltaY;
        }

        e.Handled = true;
    }

    private void OnHandlePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerDown) return;
        FinishHandleDrag(sender as FrameworkElement, e.Pointer);
        e.Handled = true;
    }

    private void OnHandlePointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerDown) return;
        CancelHandleDrag(sender as FrameworkElement, e.Pointer);
        e.Handled = true;
    }

    private void OnHandlePointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerDown) return;
        CancelHandleDrag(sender as FrameworkElement, e.Pointer);
        e.Handled = true;
    }

    private void FinishHandleDrag(FrameworkElement? handle, Pointer? pointer)
    {
        if (handle is not null && pointer is not null)
        {
            try { handle.ReleasePointerCapture(pointer); } catch { }
        }

        if (_isDragging)
        {
            if (_dragTransform is not null)
            {
                _dragTransform.TranslateY = 0;
                _dragTransform.TranslateX = 0;
            }
            if (_draggedContainer is not null)
            {
                _draggedContainer.RenderTransform = null;
                Canvas.SetZIndex(_draggedContainer, 0);
                Animation.ControlTransitions.Attach(_draggedContainer);
            }
            _vm?.SyncOrderAfterReorder();
        }
        else
        {
            if (_draggedContainer is not null)
            {
                _draggedContainer.RenderTransform = null;
                Canvas.SetZIndex(_draggedContainer, 0);
                Animation.ControlTransitions.Attach(_draggedContainer);
            }
            if (handle?.ContextFlyout is MenuFlyout flyout)
            {
                flyout.ShowAt(handle);
            }
        }

        _isPointerDown = false;
        _isDragging = false;
        _draggedItem = null;
        _draggedContainer = null;
        _dragTransform = null;
        _activeHandle = null;
        try { ProtectedCursor = null; } catch { }
    }

    private void CancelHandleDrag(FrameworkElement? handle, Pointer? pointer)
    {
        if (handle is not null && pointer is not null)
        {
            try { handle.ReleasePointerCapture(pointer); } catch { }
        }

        if (_dragTransform is not null)
        {
            _dragTransform.TranslateY = 0;
            _dragTransform.TranslateX = 0;
        }
        if (_draggedContainer is not null)
        {
            _draggedContainer.RenderTransform = null;
            Canvas.SetZIndex(_draggedContainer, 0);
            Animation.ControlTransitions.Attach(_draggedContainer);
        }

        _isPointerDown = false;
        _isDragging = false;
        _draggedItem = null;
        _draggedContainer = null;
        _dragTransform = null;
        _activeHandle = null;
        try { ProtectedCursor = null; } catch { }
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

    private void OnHandlePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        try { ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeNorthSouth); } catch { }
    }

    private void OnHandlePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) { try { ProtectedCursor = null; } catch { } }
    }

    private void RowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement row)
        {
            for (DependencyObject? p = row; p is not null; p = VisualTreeHelper.GetParent(p))
            {
                if (p is ListViewItem itemContainer)
                {
                    Animation.ControlTransitions.Attach(itemContainer);
                    break;
                }
            }
        }
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

    private void ItemKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not ChecklistItemViewModel item || _vm is null) return;
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
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
        if (e.Key == VirtualKey.Enter)
        {
            if (alt || shift)
            {
                e.Handled = true;
                var start = box.SelectionStart;
                var len = box.SelectionLength;
                var text = box.Text ?? "";
                var before = text[..start];
                var after = text[(start + len)..];
                box.Text = before + "\r\n" + after;
                box.SelectionStart = start + 2;
                box.SelectionLength = 0;
            }
            else
            {
                e.Handled = true;
                _vm.SplitItem(item, box.SelectionStart, box.SelectionLength);
            }
            return;
        }
        else if (e.Key == VirtualKey.Back && !item.HasText && _vm.Items.Count > 1) { _vm.RemoveItem(item); e.Handled = true; }
    }
}
