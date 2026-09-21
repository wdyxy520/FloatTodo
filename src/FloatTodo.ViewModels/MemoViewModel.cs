using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatTodo.Core.Models;

namespace FloatTodo.ViewModels;

public enum MemoViewMode { Preview, Editing }
public enum MemoType { Text, Checklist }

public sealed partial class ChecklistItemViewModel : ObservableObject
{
    internal ChecklistItem Model { get; }
    private readonly MemoViewModel _owner;
    public Guid Id => Model.Id;
    [ObservableProperty] private bool showsTopDivider;
    public string Text
    {
        get => Model.Text;
        set { if (value == Model.Text) return; Model.Text = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); _owner.Changed(); }
    }
    public bool HasText => !string.IsNullOrEmpty(Text);
    public bool IsCompleted
    {
        get => Model.IsCompleted;
        set
        {
            if (value == Model.IsCompleted) return;
            Model.IsCompleted = value;
            OnPropertyChanged();
            _owner.ReorderItemOnCompletionChange(this);
            _owner.Changed();
        }
    }
    public bool IsEditing => _owner.IsEditing;
    public bool IsReordering => _owner.IsReordering;
    internal void NotifyEditingChanged() => OnPropertyChanged(nameof(IsEditing));
    internal void NotifyReorderingChanged() => OnPropertyChanged(nameof(IsReordering));
    internal ChecklistItemViewModel(ChecklistItem model, MemoViewModel owner) { Model = model; _owner = owner; }
    [RelayCommand] private void Remove() => _owner.RemoveItem(this);
}

public sealed partial class MemoViewModel : ObservableObject
{
    private readonly Memo _model;
    private readonly Action _changed;
    private readonly Action<MemoViewModel> _edit;
    private readonly Action<MemoViewModel> _delete;
    internal bool IsNew { get; set; }
    public Guid Id => _model.Id;
    public ObservableCollection<ChecklistItemViewModel> Items { get; } = [];
    [ObservableProperty] private MemoViewMode viewMode;
    [ObservableProperty] private bool showTitle;
    [ObservableProperty] private bool isReordering;
    public bool IsEditing => ViewMode == MemoViewMode.Editing;
    public MemoType Type => _model.IsChecklist ? MemoType.Checklist : MemoType.Text;
    public bool IsChecklist => Type == MemoType.Checklist;
    public string ConvertHint => IsChecklist
        ? (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "Convert to Text" : "转为文本")
        : (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "Convert to Checklist" : "转为清单");
    public string Title { get => _model.Title; set { if (Title == value) return; _model.Title = value; OnPropertyChanged(); Changed(); } }
    public string Text { get => _model.Text; set { if (Text == value) return; _model.Text = value; OnPropertyChanged(); Changed(); } }
    public event Action<ChecklistItemViewModel?>? FocusRequested;
    public MemoViewModel(Memo model, Action changed, Action<MemoViewModel> edit, Action<MemoViewModel> delete)
    {
        _model = model; _changed = changed; _edit = edit; _delete = delete;
        ShowTitle = model.TitleVisible ?? !string.IsNullOrEmpty(Title);
        ReloadItems();
    }
    partial void OnViewModeChanged(MemoViewMode value)
    {
        OnPropertyChanged(nameof(IsEditing));
        if (value != MemoViewMode.Editing) IsReordering = false;
        foreach (var item in Items) item.NotifyEditingChanged();
    }
    partial void OnIsReorderingChanged(bool value)
    {
        OnPropertyChanged(nameof(ReorderToggleLabel));
        foreach (var item in Items) item.NotifyReorderingChanged();
    }
    internal void Changed() => _changed();
    internal void ReorderItemOnCompletionChange(ChecklistItemViewModel item)
    {
        var currentIndex = Items.IndexOf(item);
        if (currentIndex < 0) return;

        int targetIndex = 0;
        foreach (var other in Items)
        {
            if (other == item) continue;

            if (!item.IsCompleted)
            {
                // 未完成项排在所有已完成项之前；未完成项之间按 Order 升序
                if (!other.IsCompleted && other.Model.Order <= item.Model.Order)
                {
                    targetIndex++;
                }
                else
                {
                    break;
                }
            }
            else
            {
                // 已完成项排在所有未完成项之后；已完成项之间按 Order 升序
                if (!other.IsCompleted)
                {
                    targetIndex++;
                }
                else if (other.Model.Order <= item.Model.Order)
                {
                    targetIndex++;
                }
                else
                {
                    break;
                }
            }
        }

        if (currentIndex != targetIndex)
        {
            Items.Move(currentIndex, targetIndex);
        }

        UpdateDividers();
    }

    internal void SortItems()
    {
        var sorted = Items.OrderBy(i => i.IsCompleted).ThenBy(i => i.Model.Order).ToArray();
        for (int n = 0; n < sorted.Length; n++)
            if (Items[n] != sorted[n]) Items.Move(Items.IndexOf(sorted[n]), n);

        UpdateDividers();
    }

    internal void UpdateDividers()
    {
        bool hasIncomplete = Items.Any(i => !i.IsCompleted);
        bool firstCompletedFound = false;
        foreach (var item in Items)
        {
            if (item.IsCompleted && !firstCompletedFound && hasIncomplete)
            {
                item.ShowsTopDivider = true;
                firstCompletedFound = true;
            }
            else
            {
                item.ShowsTopDivider = false;
            }
        }
    }
    private void ReloadItems()
    {
        Items.Clear();
        foreach (var item in _model.Items) Items.Add(new(item, this));
        SortItems();
    }
    [RelayCommand] private void BeginEdit() => _edit(this);
    public bool IsEmptyNew => IsNew && string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Text) && Items.All(i => string.IsNullOrWhiteSpace(i.Text));
    [RelayCommand]
    public void FinishEdit()
    {
        IsReordering = false;
        ViewMode = MemoViewMode.Preview;
        if (IsEmptyNew) _delete(this);
        else { IsNew = false; Changed(); }
    }
    [RelayCommand] private void Delete() => _delete(this);
    public string TitleToggleLabel => ShowTitle
        ? (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "Hide Title" : "隐藏标题")
        : (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "Show Title" : "显示标题");
    partial void OnShowTitleChanged(bool value) { _model.TitleVisible = value; OnPropertyChanged(nameof(TitleToggleLabel)); Changed(); }
    [RelayCommand] private void AddTitle() { ShowTitle = !ShowTitle; _edit(this); }
    public string ReorderToggleLabel => IsReordering
        ? (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "Done Reordering" : "完成排序")
        : (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "Reorder Items" : "调整顺序");
    [RelayCommand] private void ToggleReorder() => IsReordering = !IsReordering;
    [RelayCommand]
    private void Convert()
    {
        IsReordering = false;
        // Snapshot visible ordering, including completed items, before converting to text.
        _model.Items = new(Items.Select(i => i.Model));
        _model.Convert(); ReloadItems();
        OnPropertyChanged(nameof(Text)); OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(IsChecklist)); OnPropertyChanged(nameof(ConvertHint)); Changed();
    }
    [RelayCommand]
    public void AddItem()
    {
        var order = Items.Count == 0 ? 0 : Items.Max(i => i.Model.Order) + 1;
        var item = new ChecklistItemViewModel(new() { Order = order }, this);
        Items.Add(item); SortItems(); Changed(); FocusRequested?.Invoke(item);
    }
    public void SplitItem(ChecklistItemViewModel item, int selectionStart, int selectionLength)
    {
        var text = item.Text ?? "";
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var end = Math.Clamp(start + selectionLength, start, text.Length);
        var trailing = text[end..];
        item.Text = text[..start];
        foreach (var sibling in Items.Where(i => i.Model.Order > item.Model.Order)) sibling.Model.Order++;
        var added = new ChecklistItemViewModel(new() { Text = trailing, Order = item.Model.Order + 1 }, this);
        Items.Add(added); SortItems(); Changed(); FocusRequested?.Invoke(added);
    }
    public void MoveItem(ChecklistItemViewModel item, int delta)
    {
        var index = Items.IndexOf(item);
        var target = index + delta;
        if (target < 0 || target >= Items.Count) return;
        if (Items[target].IsCompleted != item.IsCompleted) return;
        Items.Move(index, target);
        for (int i = 0; i < Items.Count; i++) Items[i].Model.Order = i;
        SortItems();
        Changed();
    }
    public void MoveItemToIndex(ChecklistItemViewModel item, int targetIndex)
    {
        var index = Items.IndexOf(item);
        if (index < 0 || targetIndex < 0 || targetIndex >= Items.Count || index == targetIndex) return;
        if (Items[targetIndex].IsCompleted != item.IsCompleted) return;
        Items.Move(index, targetIndex);
        for (int i = 0; i < Items.Count; i++) Items[i].Model.Order = i;
        SortItems();
        Changed();
    }
    public void SyncOrderAfterReorder()
    {
        for (int i = 0; i < Items.Count; i++) Items[i].Model.Order = i;
        UpdateDividers();
        Changed();
    }
    public void RemoveItem(ChecklistItemViewModel item)
    {
        var index = Items.IndexOf(item);
        if (index < 0) return;
        Items.Remove(item); Changed();
        FocusRequested?.Invoke(Items.Count == 0 ? null : Items[Math.Clamp(index - 1, 0, Items.Count - 1)]);
    }
    public Memo Snapshot() => new()
    {
        Id = Id,
        CreatedAt = _model.CreatedAt,
        Title = Title,
        TitleVisible = ShowTitle,
        Text = Text,
        IsChecklist = IsChecklist,
        Items = new(Items.Select(i => new ChecklistItem { Id = i.Id, Order = i.Model.Order, Text = i.Text, IsCompleted = i.IsCompleted }))
    };
}

