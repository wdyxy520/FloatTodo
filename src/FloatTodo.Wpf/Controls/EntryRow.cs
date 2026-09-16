using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FloatTodo.Wpf.Controls;

public sealed class EntryRow : INotifyPropertyChanged
{
    public Guid Id { get; init; }
    public bool IsNote { get; init; }
    public string Text { get; private set; } = "";
    public bool IsCompleted { get; private set; }
    private string _draft = "";
    private bool _editing;
    private bool _busy;
    public string Draft { get => _draft; set { _draft = value; Changed(); } }
    public bool IsEditing { get => _editing; set { _editing = value; Changed(); } }
    public bool IsAvailable { get => !_busy; set { _busy = !value; Changed(); } }
    public string EditHint => IsNote ? "编辑笔记" : "编辑任务";
    public string DeleteHint => IsNote ? "删除笔记" : "删除任务";
    public string ConvertHint => IsNote ? "转为待办" : "转为笔记";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public void Update(string text, bool completed)
    {
        Text = text;
        IsCompleted = completed;
        Changed(nameof(Text));
        Changed(nameof(IsCompleted));
    }
}
