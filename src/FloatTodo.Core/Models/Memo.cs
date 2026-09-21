using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace FloatTodo.Core.Models;

public abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; Changed(name); }
}

public sealed class Memo : ObservableModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    private string _title = "", _text = "";
    private bool _isChecklist, _showTitle;
    public bool? TitleVisible { get; set; }
    public string Title { get => _title; set => Set(ref _title, value); }
    public string Text { get => _text; set => Set(ref _text, value); }
    public bool IsChecklist { get => _isChecklist; set { Set(ref _isChecklist, value); Changed(nameof(ConvertHint)); } }
    public ObservableCollection<ChecklistItem> Items { get; set; } = [];
    [JsonIgnore] public bool ShowTitle { get => _showTitle || Title.Length > 0; set => Set(ref _showTitle, value); }
    [JsonIgnore] public string ConvertHint => IsChecklist
        ? (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "Convert to Text" : "转为文本")
        : (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "Convert to Checklist" : "转为清单");

    public void SortItems()
    {
        var sorted = Items.OrderBy(i => i.IsCompleted).ThenBy(i => i.Order).ToArray();
        for (int index = 0; index < sorted.Length; index++)
            if (!ReferenceEquals(Items[index], sorted[index])) Items.Move(Items.IndexOf(sorted[index]), index);
    }

    public void Convert()
    {
        if (IsChecklist)
        {
            Text = string.Join("\n", Items.Select(i => i.Text));
            Items.Clear();
        }
        else
        {
            var lines = Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            Items = new(lines.Select((line, index) => new ChecklistItem { Text = line, Order = index }));
            Changed(nameof(Items));
            Text = "";
        }
        IsChecklist = !IsChecklist;
    }
}

public sealed class ChecklistItem : ObservableModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Order { get; set; }
    private string _text = "";
    private bool _isCompleted;
    public string Text { get => _text; set => Set(ref _text, value); }
    public bool IsCompleted { get => _isCompleted; set => Set(ref _isCompleted, value); }
}
