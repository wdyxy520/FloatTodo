using System.Text.Json;
using FloatTodo.Core.Models;
using FloatTodo.Core.Services;
using Xunit;

namespace FloatTodo.Core.Tests;

public sealed class MemoServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FloatTodoTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "todos.json");
    [Fact]
    public void LegacyMigration_PreservesIdsTextCompletion_AndBacksUpBeforeWrite()
    {
        var legacy = new TodoService(FilePath);
        legacy.Add("带电脑");
        var id = legacy.GetItems()[0].Id;
        legacy.SetCompleted(id, true);
        legacy.AddNote("集合地点\n公司门口");
        var original = File.ReadAllText(FilePath);
        var service = new MemoService(FilePath);
        var memos = service.GetMemos();
        Assert.Equal(2, memos.Count);
        Assert.Equal(id, memos[0].Id);
        Assert.True(memos[0].Items[0].IsCompleted);
        Assert.Equal("集合地点\n公司门口", memos[1].Text);
        Assert.Equal(original, File.ReadAllText(FilePath));
        service.Save(memos);
        Assert.Equal(original, File.ReadAllText(Assert.Single(Directory.GetFiles(_dir, "*.bak"))));
        Assert.Equal(id, new MemoService(FilePath).GetMemos()[0].Id);
    }
    [Fact]
    public void LegacyArray_IsMigrated()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new[] { new TodoItem { Text = "旧版", IsCompleted = true } }));
        Assert.True(new MemoService(FilePath).GetMemos()[0].Items[0].IsCompleted);
    }
    [Fact]
    public void CompletedItemsSink_AndUncheckingRestoresOrder_AcrossReload()
    {
        var memo = new Memo { Text = "A\nB\nC" };
        memo.Convert();
        var first = memo.Items[0];
        first.IsCompleted = true;
        memo.SortItems();
        Assert.Equal(new[] { "B", "C", "A" }, memo.Items.Select(i => i.Text));
        var service = new MemoService(FilePath);
        service.Save([memo]);
        memo = new MemoService(FilePath).GetMemos()[0];
        Assert.Equal(new[] { "B", "C", "A" }, memo.Items.Select(i => i.Text));
        memo.Items.Single(i => i.Id == first.Id).IsCompleted = false;
        memo.SortItems();
        Assert.Equal(new[] { "A", "B", "C" }, memo.Items.Select(i => i.Text));
    }
    [Fact]
    public void Conversion_PreservesTitleBlankLinesAndIdentity()
    {
        var memo = new Memo { Title = "出行", Text = "电脑\n\n充电器" };
        var id = memo.Id;
        memo.Convert();
        Assert.Equal(3, memo.Items.Count);
        memo.Convert();
        Assert.Equal("电脑\n\n充电器", memo.Text);
        Assert.Equal("出行", memo.Title);
        Assert.Equal(id, memo.Id);
    }
    [Fact]
    public void FailedSave_LeavesDiskAndSnapshotUntouched()
    {
        var service = new MemoService(FilePath);
        service.Save([new Memo { Text = "Saved" }]);
        var draft = service.GetMemos();
        draft[0].Text = "Unsaved";
        Directory.CreateDirectory(FilePath + ".tmp");
        var error = Record.Exception(() => service.Save(draft));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal("Saved", service.GetMemos()[0].Text);
        Assert.Equal("Saved", new MemoService(FilePath).GetMemos()[0].Text);
    }
    [Fact]
    public void InvalidDocument_IsRejectedWithoutOverwriting()
    {
        Directory.CreateDirectory(_dir);
        const string invalid = "{\"Version\":2,\"Memos\":null}";
        File.WriteAllText(FilePath, invalid);
        Assert.Throws<InvalidDataException>(() => new MemoService(FilePath));
        Assert.Equal(invalid, File.ReadAllText(FilePath));
    }
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}
