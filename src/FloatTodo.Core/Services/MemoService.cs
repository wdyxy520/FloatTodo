using System.Text.Json;
using FloatTodo.Core.Models;

namespace FloatTodo.Core.Services;

/// <summary>Unified notes; upgrades legacy records only after backing up the original file.</summary>
public sealed class MemoService
{
    private readonly string _path;
    private bool _legacy;
    private List<Memo> _memos = [];
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public sealed class Document
    {
        public int Version { get; set; } = 2;
        public List<Memo> Memos { get; set; } = [];
    }
    public MemoService(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloatTodo", "todos.json");
        if (!File.Exists(_path)) return;
        var json = File.ReadAllText(_path);
        using var parsed = JsonDocument.Parse(json);
        if (parsed.RootElement.ValueKind == JsonValueKind.Object && parsed.RootElement.EnumerateObject().Any(p => p.Name.Equals("Memos", StringComparison.OrdinalIgnoreCase)))
        {
            var doc = JsonSerializer.Deserialize<Document>(json, Options) ?? throw new InvalidDataException("记事文件无效。");
            if (doc.Version != 2) throw new InvalidDataException("不支持此记事文件版本。");
            _memos = doc.Memos;
            Validate(_memos);
        }
        else
        {
            var old = new TodoService(_path);
            _memos = old.GetItems().Select(i => new Memo {
                Id = i.Id, CreatedAt = i.CreatedAt, IsChecklist = true,
                Items = [new ChecklistItem { Text = i.Text, IsCompleted = i.IsCompleted }]
            }).Concat(old.GetNotes().Select(n => new Memo { Id = n.Id, CreatedAt = n.CreatedAt, Text = n.Text })).ToList();
            _legacy = true;
        }
        foreach (var memo in _memos) memo.SortItems();
    }
    public List<Memo> GetMemos() => JsonSerializer.Deserialize<List<Memo>>(JsonSerializer.Serialize(_memos, Options), Options)!;
    public void Save(IEnumerable<Memo> memos)
    {
        var next = memos.ToList();
        Validate(next);
        var json = JsonSerializer.Serialize(new Document { Memos = next }, Options);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + ".tmp";
        try
        {
            if (_legacy) File.Copy(_path, _path + ".pre-memos-" + Guid.NewGuid().ToString("N") + ".bak");
            File.WriteAllText(temporary, json);
            File.Move(temporary, _path, overwrite: true);
            _memos = JsonSerializer.Deserialize<Document>(json, Options)!.Memos;
            _legacy = false;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    private static void Validate(List<Memo>? memos)
    {
        if (memos is null || memos.Any(m => m is null || m.Id == Guid.Empty || m.Text is null || m.Title is null || m.Items is null) ||
            memos.Select(m => m.Id).Distinct().Count() != memos.Count)
            throw new InvalidDataException("记事包含无效或重复记录。");
        foreach (var memo in memos)
            if (memo.Items.Any(i => i is null || i.Id == Guid.Empty || i.Text is null) || memo.Items.Select(i => i.Id).Distinct().Count() != memo.Items.Count)
                throw new InvalidDataException("清单包含无效或重复项目。");
    }
}
