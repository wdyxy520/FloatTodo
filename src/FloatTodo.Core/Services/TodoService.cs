using System.Text.Json;
using FloatTodo.Core.Models;

namespace FloatTodo.Core.Services;

/// <summary>Persists each mutation before publishing the new task snapshot.</summary>
public sealed class TodoService
{
    private readonly string _path;
    private List<TodoItem> _items;
    private List<Snippet> _notes;

    public sealed class Document
    {
        public List<TodoItem> Todos { get; set; } = [];
        public List<Snippet> Notes { get; set; } = [];
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public TodoService(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloatTodo", "todos.json");
        var document = new Document();
        if (File.Exists(_path))
        {
            string json = File.ReadAllText(_path);
            using var parsed = JsonDocument.Parse(json);
            // Phase 5 stored an array. Upgrade it in place only after a successful mutation.
            document = parsed.RootElement.ValueKind == JsonValueKind.Array
                ? new Document { Todos = JsonSerializer.Deserialize<List<TodoItem>>(json, JsonOptions)! }
                : JsonSerializer.Deserialize<Document>(json, JsonOptions)
                    ?? throw new InvalidDataException("数据文件内容为空。");
            if (parsed.RootElement.ValueKind == JsonValueKind.Object &&
                !parsed.RootElement.EnumerateObject().Any(p => p.Name.Equals("Todos", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("数据文件缺少待办列表。");
        }
        _items = document.Todos ?? throw new InvalidDataException("待办列表无效。");
        _notes = document.Notes ?? throw new InvalidDataException("笔记列表无效。");
        if (_items.Any(item => item is null || item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Text)) ||
            _items.Select(item => item.Id).Distinct().Count() != _items.Count)
            throw new InvalidDataException("任务文件包含无效记录。");
        if (_notes.Any(note => note is null || note.Id == Guid.Empty || string.IsNullOrWhiteSpace(note.Text)) ||
            _items.Select(item => item.Id).Concat(_notes.Select(note => note.Id)).Distinct().Count() != _items.Count + _notes.Count)
            throw new InvalidDataException("笔记文件包含无效或重复记录。");
    }

    public IReadOnlyList<TodoItem> GetItems() => _items.OrderBy(item => item.IsCompleted)
        .ThenBy(item => item.Order).ThenBy(item => item.CreatedAt).Select(Clone).ToArray();

    public void Add(string text)
    {
        var next = _items.Select(Clone).ToList();
        // Normalize sequence numbers so repeated additions cannot overflow Order.
        next = next.OrderBy(item => item.Order).ThenBy(item => item.CreatedAt).ToList();
        for (var index = 0; index < next.Count; index++) next[index].Order = index;
        next.Add(new TodoItem { Text = ValidateText(text), Order = next.Count });
        Commit(next);
    }

    public void Edit(Guid id, string text) => Update(id, item => item.Text = ValidateText(text));
    public void SetCompleted(Guid id, bool completed) => Update(id, item => item.IsCompleted = completed);

    public void Delete(Guid id)
    {
        var next = _items.Select(Clone).ToList();
        if (next.RemoveAll(item => item.Id == id) == 0) throw new KeyNotFoundException("任务不存在。");
        Commit(next);
    }

    public IReadOnlyList<Snippet> GetNotes() => _notes.OrderBy(note => note.Order)
        .ThenBy(note => note.CreatedAt).Select(Clone).ToArray();

    public void AddNote(string text)
    {
        var next = GetNotes().ToList();
        for (int index = 0; index < next.Count; index++) next[index].Order = index;
        next.Add(new Snippet { Text = ValidateText(text), Order = next.Count });
        Commit(_items, next);
    }

    public void EditNote(Guid id, string text)
    {
        var next = _notes.Select(Clone).ToList();
        var note = next.SingleOrDefault(note => note.Id == id) ?? throw new KeyNotFoundException("笔记不存在。");
        note.Text = ValidateText(text);
        note.UpdatedAt = DateTime.UtcNow;
        Commit(_items, next);
    }

    public void DeleteNote(Guid id)
    {
        var next = _notes.Select(Clone).ToList();
        if (next.RemoveAll(note => note.Id == id) == 0) throw new KeyNotFoundException("笔记不存在。");
        Commit(_items, next);
    }

    public void ConvertToNote(Guid id)
    {
        var item = _items.SingleOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("任务不存在。");
        var notes = GetNotes().ToList();
        for (int index = 0; index < notes.Count; index++) notes[index].Order = index;
        notes.Add(new Snippet { Id = item.Id, Text = item.Text, CreatedAt = item.CreatedAt, Order = notes.Count });
        Commit(_items.Where(item => item.Id != id).ToList(), notes);
    }

    public void ConvertToTodo(Guid id)
    {
        var note = _notes.SingleOrDefault(note => note.Id == id) ?? throw new KeyNotFoundException("笔记不存在。");
        var items = _items.OrderBy(item => item.Order).Select(Clone).ToList();
        for (int index = 0; index < items.Count; index++) items[index].Order = index;
        items.Add(new TodoItem { Id = note.Id, Text = note.Text, CreatedAt = note.CreatedAt, Order = items.Count });
        Commit(items, _notes.Where(note => note.Id != id).ToList());
    }

    private void Update(Guid id, Action<TodoItem> update)
    {
        var next = _items.Select(Clone).ToList();
        var item = next.SingleOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("任务不存在。");
        update(item);
        Commit(next);
    }

    private void Commit(List<TodoItem> next, List<Snippet>? notes = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Document { Todos = next, Notes = notes ?? _notes }, JsonOptions));
            File.Move(temporary, _path, overwrite: true);
            _items = next;
            if (notes is not null) _notes = notes;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string ValidateText(string text) => !string.IsNullOrWhiteSpace(text)
        ? text.Trim() : throw new ArgumentException("请输入任务内容。", nameof(text));

    private static TodoItem Clone(TodoItem item) => new()
    {
        Id = item.Id, Text = item.Text, IsCompleted = item.IsCompleted,
        Order = item.Order, CreatedAt = item.CreatedAt
    };

    private static Snippet Clone(Snippet note) => new()
    {
        Id = note.Id, Text = note.Text, Order = note.Order,
        CreatedAt = note.CreatedAt, UpdatedAt = note.UpdatedAt
    };
}
