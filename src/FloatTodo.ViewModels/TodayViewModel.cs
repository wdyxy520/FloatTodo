using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatTodo.Core.Models;

namespace FloatTodo.ViewModels;

public sealed partial class TodayViewModel : ObservableObject
{
    public ObservableCollection<MemoViewModel> Memos { get; } = [];
    public event Action? SaveRequested;
    public ObservableCollection<MemoViewModel> SavedMemos => Memos;
    public void FinishEditingExcept(MemoViewModel? selected = null) { foreach (var memo in Memos.Where(m => m.IsEditing && m != selected).ToArray()) memo.FinishEdit(); }
    [ObservableProperty] private string errorMessage = "";
    public bool IsEditing => Memos.Any(m => m.IsEditing);
    public TodayViewModel(IEnumerable<Memo> memos)
    {
        foreach (var memo in memos) Memos.Add(Wrap(memo));
    }
    private MemoViewModel Wrap(Memo memo)
    {
        var vm = new MemoViewModel(memo, () => SaveRequested?.Invoke(), Edit, Delete);
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MemoViewModel.IsEditing)) OnPropertyChanged(nameof(IsEditing)); };
        return vm;
    }
    public void Edit(MemoViewModel selected)
    {
        foreach (var memo in Memos.Where(m => m != selected && m.IsEditing).ToArray()) memo.FinishEdit();
        selected.ViewMode = MemoViewMode.Editing;
    }
    private void Delete(MemoViewModel memo) { Memos.Remove(memo); OnPropertyChanged(nameof(IsEditing)); SaveRequested?.Invoke(); }
    [RelayCommand] private void NewText() => Add(false);
    [RelayCommand] private void NewChecklist() => Add(true);
    private void Add(bool checklist)
    {
        var memo = Wrap(new() { IsChecklist = checklist });
        memo.IsNew = true;
        FinishEditingExcept(); Memos.Insert(0, memo); Edit(memo);
        if (checklist) memo.AddItem();
        SaveRequested?.Invoke();
    }
    public void MoveMemo(MemoViewModel memo, int targetIndex)
    {
        var oldIndex = Memos.IndexOf(memo);
        if (oldIndex < 0 || targetIndex < 0 || targetIndex >= Memos.Count || oldIndex == targetIndex) return;
        Memos.Move(oldIndex, targetIndex);
        SaveRequested?.Invoke();
    }
    public void SyncSavedMemosOrder()
    {
        SaveRequested?.Invoke();
    }
    public List<Memo> Snapshot() => Memos.Select(m => m.Snapshot()).ToList();
}


