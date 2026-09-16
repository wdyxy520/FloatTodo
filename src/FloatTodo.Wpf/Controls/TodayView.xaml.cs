using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FloatTodo.Core.Services;

namespace FloatTodo.Wpf.Controls;

public partial class TodayView : UserControl
{
    private TodoService? _todos;
    private bool _loaded;
    private readonly ObservableCollection<EntryRow> _tasks = [];
    private readonly ObservableCollection<EntryRow> _notes = [];
    private EntryRow? _lastToggledRow;
    private bool _lastCompletedBeforeClick;
    private DateTime _lastToggleTime;

    public TodayView()
    {
        InitializeComponent();
        TaskList.ItemsSource = _tasks;
        NoteList.ItemsSource = _notes;
    }
    public TodayView(TodoService todos) : this() => _todos = todos;
    public bool IsInputActive => IsKeyboardFocusWithin && Keyboard.FocusedElement is TextBoxBase;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        LoadTasks();
    }
    private void OnRetryClick(object sender, RoutedEventArgs e) => LoadTasks();
    private void LoadTasks()
    {
        bool loaded = TrySave(() => _todos ??= new TodoService());
        QuickInput.IsEnabled = NoteInput.IsEnabled = loaded;
        RetryButton.Visibility = loaded ? Visibility.Collapsed : Visibility.Visible;
        if (loaded) RefreshTasks();
        else EmptyText.Visibility = EmptyNotesText.Visibility = Visibility.Collapsed;
    }

    private void RefreshTasks()
    {
        Sync(_tasks, _todos!.GetItems().Select(item => (item.Id, item.Text, item.IsCompleted)), false);
        Sync(_notes, _todos.GetNotes().Select(note => (note.Id, note.Text, false)), true);
        EmptyText.Visibility = Visibility.Collapsed;
        EmptyNotesText.Visibility = Visibility.Collapsed;
    }

    private static void Sync(ObservableCollection<EntryRow> rows, IEnumerable<(Guid Id, string Text, bool Completed)> source, bool isNote)
    {
        var items = source.ToArray();
        var ids = items.Select(item => item.Id).ToHashSet();
        for (int index = rows.Count - 1; index >= 0; index--)
            if (!ids.Contains(rows[index].Id)) rows.RemoveAt(index);
        for (int index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var row = rows.FirstOrDefault(row => row.Id == item.Id);
            if (row is null)
            {
                row = new EntryRow { Id = item.Id, IsNote = isNote };
                rows.Insert(index, row);
            }
            else if (rows.IndexOf(row) != index) rows.Move(rows.IndexOf(row), index);
            // Keep in-progress inline drafts when a different row changes or moves.
            row.Update(item.Text, item.Completed);
        }
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox input) return;
        if (e.Key == Key.Escape)
        {
            input.Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0 && _todos is not null)
        {
            e.Handled = true;
            if (string.IsNullOrWhiteSpace(input.Text)) return;
            if (!TrySave(() => { if (input == NoteInput) _todos.AddNote(input.Text); else _todos.Add(input.Text); })) return;
            input.Clear();
            RefreshTasks();
        }
    }

    private void StartEditing(EntryRow row)
    {
        row.Draft = row.Text;
        row.IsEditing = true;
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (((FrameworkElement)sender).DataContext is EntryRow row) StartEditing(row);
    }

    private void OnTextMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is TextBlock { DataContext: EntryRow row })
        {
            e.Handled = true;
            if (!row.IsNote && ReferenceEquals(_lastToggledRow, row) && (DateTime.UtcNow - _lastToggleTime).TotalMilliseconds < 500)
            {
                _todos?.SetCompleted(row.Id, _lastCompletedBeforeClick);
                row.Update(row.Text, _lastCompletedBeforeClick);
            }
            StartEditing(row);
        }
    }

    private void OnEditorLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: EntryRow row } || !row.IsEditing) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (row.IsEditing) SaveEdit(row);
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnEditorVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox editor && editor.IsVisible)
        {
            editor.Focus();
            editor.SelectAll();
        }
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: EntryRow row }) return;
        if (e.Key == Key.Escape) { row.IsEditing = false; e.Handled = true; }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            SaveEdit(row);
        }
    }

    private void SaveEdit(EntryRow row)
    {
        if (!TrySave(() => { if (row.IsNote) _todos!.EditNote(row.Id, row.Draft); else _todos!.Edit(row.Id, row.Draft); })) return;
        row.IsEditing = false;
        RefreshTasks();
    }
    private void OnSaveEditClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (((FrameworkElement)sender).DataContext is EntryRow row) SaveEdit(row);
    }
    private void OnCancelEditClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (((FrameworkElement)sender).DataContext is EntryRow row) row.IsEditing = false;
    }
    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (((FrameworkElement)sender).DataContext is not EntryRow row || row.IsEditing) return;
        if (TrySave(() => { if (row.IsNote) _todos!.DeleteNote(row.Id); else _todos!.Delete(row.Id); })) RefreshTasks();
    }
    private void OnConvertClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (((FrameworkElement)sender).DataContext is not EntryRow row || row.IsEditing) return;
        if (TrySave(() => { if (row.IsNote) _todos!.ConvertToTodo(row.Id); else _todos!.ConvertToNote(row.Id); })) RefreshTasks();
    }

    private async void OnRowClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: EntryRow row } card || IsInteractiveChild(e.OriginalSource as DependencyObject, card)) return;
        if (row.IsEditing) return;
        e.Handled = true;
        card.Focus();
        _lastToggledRow = row;
        _lastCompletedBeforeClick = row.IsCompleted;
        _lastToggleTime = DateTime.UtcNow;
        await ToggleRow(row, FindChild<CheckBox>(card));
    }
    private async void OnRowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || !ReferenceEquals(e.OriginalSource, sender) || sender is not Border { DataContext: EntryRow row } card) return;
        e.Handled = true;
        await ToggleRow(row, FindChild<CheckBox>(card));
    }
    private async void OnCompletedClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is CheckBox { DataContext: EntryRow row } checkbox) await ToggleRow(row, checkbox);
    }
    private async Task ToggleRow(EntryRow row, CheckBox? checkbox)
    {
        if (row.IsNote || row.IsEditing || !row.IsAvailable) return;
        bool completed = !row.IsCompleted;
        if (!TrySave(() => _todos!.SetCompleted(row.Id, completed)))
        {
            checkbox?.SetCurrentValue(CheckBox.IsCheckedProperty, row.IsCompleted);
            return;
        }
        row.Update(row.Text, completed);
        row.IsAvailable = false;
        try
        {
            if (completed && checkbox is not null && SystemParameters.ClientAreaAnimation)
            {
                var animation = new DoubleAnimation(1, 1.22, TimeSpan.FromMilliseconds(100))
                { AutoReverse = true, FillBehavior = FillBehavior.Stop };
                var scale = new ScaleTransform(1, 1);
                checkbox.RenderTransform = scale;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
                await Task.Delay(210);
            }
        }
        finally { row.IsAvailable = true; RefreshTasks(); }
    }

    private static bool IsInteractiveChild(DependencyObject? source, DependencyObject card)
    {
        while (source is not null && source != card)
        {
            if (source is ButtonBase or TextBoxBase) return true;
            source = source is FrameworkContentElement content ? content.Parent : VisualTreeHelper.GetParent(source);
        }
        return false;
    }
    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } descendant) return descendant;
        }
        return null;
    }
    private bool TrySave(Action action)
    {
        try { action(); ErrorText.Visibility = Visibility.Collapsed; return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or KeyNotFoundException)
        {
            ErrorText.Text = $"保存或读取失败：{ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
            return false;
        }
    }
}
