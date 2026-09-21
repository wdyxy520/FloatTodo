using FloatTodo.Core.Models;
using FloatTodo.ViewModels;
using Xunit;

namespace FloatTodo.Core.Tests;

public class MemoViewModelTests
{
    [Fact]
    public void CompletionSinksAndUndoRestoresOriginalOrderAcrossReload()
    {
        var today = new TodayViewModel([new Memo { IsChecklist = true, Items = [
            new() { Text = "a", Order = 0 }, new() { Text = "b", Order = 1 }, new() { Text = "c", Order = 2 }
        ] }]);
        var memo = today.Memos[0];
        memo.Items[0].IsCompleted = true;
        Assert.Equal(["b", "c", "a"], memo.Items.Select(i => i.Text));
        var reloaded = new TodayViewModel(today.Snapshot()).Memos[0];
        reloaded.Items.Single(i => i.Text == "a").IsCompleted = false;
        Assert.Equal(["a", "b", "c"], reloaded.Items.Select(i => i.Text));
    }
    [Fact]
    public void EnterReplacesSelectionAndCreatesFollowingItemWithoutChangingIds()
    {
        var today = new TodayViewModel([new Memo { IsChecklist = true, Items = [new() { Text = "abcXYZdef", Order = 0 }, new() { Text = "last", Order = 1 }] }]);
        var memo = today.Memos[0];
        var id = memo.Items[0].Id;
        memo.SplitItem(memo.Items[0], 3, 3);
        Assert.Equal(["abc", "def", "last"], memo.Items.Select(i => i.Text));
        Assert.Equal(id, memo.Items[0].Id);
        Assert.Equal(3, memo.Snapshot().Items.Select(i => i.Order).Distinct().Count());
    }
    [Fact]
    public void AddItemInsertsNewBlankAndSplitItemCreatesNextRow()
    {
        var today = new TodayViewModel([]);
        today.NewChecklistCommand.Execute(null);
        var memo = today.Memos[0];
        // Initially NewChecklist added 1 item
        Assert.Single(memo.Items);
        memo.AddItem();
        // Now there are 2 items
        Assert.Equal(2, memo.Items.Count);
        memo.SplitItem(memo.Items[0], 0, 0);
        // Split on first item created another item below it
        Assert.Equal(3, memo.Items.Count);
    }
    [Fact]
    public void TopDividerAppearsOnlyOnFirstCompletedItemWhenIncompleteExist()
    {
        var today = new TodayViewModel([new Memo { IsChecklist = true, Items = [
            new() { Text = "todo1", Order = 0 },
            new() { Text = "todo2", Order = 1 },
            new() { Text = "done1", Order = 2, IsCompleted = true },
            new() { Text = "done2", Order = 3, IsCompleted = true }
        ] }]);
        var memo = today.Memos[0];
        Assert.False(memo.Items[0].ShowsTopDivider);
        Assert.False(memo.Items[1].ShowsTopDivider);
        Assert.True(memo.Items[2].ShowsTopDivider);
        Assert.False(memo.Items[3].ShowsTopDivider);

        // Mark all as completed -> divider disappears
        while (memo.Items.Any(i => !i.IsCompleted))
        {
            memo.Items.First(i => !i.IsCompleted).IsCompleted = true;
        }
        Assert.All(memo.Items, i => Assert.False(i.ShowsTopDivider));
    }
    [Fact]
    public void MoveItemReordersItemsCorrectly()
    {
        var today = new TodayViewModel([new Memo { IsChecklist = true, Items = [
            new() { Text = "a", Order = 0 }, new() { Text = "b", Order = 1 }, new() { Text = "c", Order = 2 }
        ] }]);
        var memo = today.Memos[0];
        memo.MoveItem(memo.Items[0], 1); // Move "a" down by 1
        Assert.Equal(["b", "a", "c"], memo.Items.Select(i => i.Text));
        memo.MoveItem(memo.Items[2], -1); // Move "c" up by 1
        Assert.Equal(["b", "c", "a"], memo.Items.Select(i => i.Text));
    }
    [Fact]
    public void EditingOneCardClosesThePreviousCardAndSnapshotIsDetached()
    {
        var today = new TodayViewModel([new Memo { Text = "original" }, new Memo()]);
        today.Edit(today.Memos[0]);
        var snapshot = today.Snapshot();
        today.Memos[0].Text = "changed";
        today.Edit(today.Memos[1]);
        Assert.False(today.Memos[0].IsEditing);
        Assert.True(today.Memos[1].IsEditing);
        Assert.Equal("original", snapshot[0].Text);
    }
    [Fact]
    public void ConversionPreservesMemoIdentityTitleAndEmptyLines()
    {
        var today = new TodayViewModel([new Memo { Title = "title", Text = "a\n\nb" }]);
        var memo = today.Memos[0]; var id = memo.Id;
        memo.ConvertCommand.Execute(null);
        Assert.Equal(["a", "", "b"], memo.Items.Select(i => i.Text));
        memo.ConvertCommand.Execute(null);
        Assert.Equal("a\n\nb", memo.Text);
        Assert.Equal(id, memo.Id); Assert.Equal("title", memo.Title);
    }
    [Fact]
    public void MoveMemoReordersSavedMemosAndSyncs()
    {
        var m1 = new Memo { Text = "1" };
        var m2 = new Memo { Text = "2" };
        var m3 = new Memo { Text = "3" };
        var today = new TodayViewModel([m1, m2, m3]);
        var savedM1 = today.SavedMemos[0];
        var savedM3 = today.SavedMemos[2];

        today.MoveMemo(savedM1, 2); // Move first memo to index 2
        Assert.Equal(["2", "3", "1"], today.SavedMemos.Select(m => m.Text));
    }

    [Fact]
    public void CompletingItemFiresAtMostSingleMoveEvent()
    {
        var memo = new Memo
        {
            IsChecklist = true,
            Items =
            [
                new() { Text = "a", Order = 0 },
                new() { Text = "b", Order = 1 },
                new() { Text = "c", Order = 2 },
                new() { Text = "d", Order = 3 }
            ]
        };
        var today = new TodayViewModel([memo]);
        var vm = today.Memos[0];

        int moveCount = 0;
        vm.Items.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
                moveCount++;
        };

        // 勾选首项，在以前的实现中会触发多次连续 Move
        vm.Items[0].IsCompleted = true;

        Assert.Equal(1, moveCount);
        Assert.Equal(["b", "c", "d", "a"], vm.Items.Select(i => i.Text));
    }

    [Fact]
    public void ConvertHintAndTitleToggleLabel_AreLocalized()
    {
        var prevCulture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("zh-CN");
            var memoZh = new MemoViewModel(new Memo { IsChecklist = true }, () => { }, _ => { }, _ => { });
            Assert.Equal("转为文本", memoZh.ConvertHint);
            Assert.Equal("显示标题", memoZh.TitleToggleLabel);
            memoZh.ShowTitle = true;
            Assert.Equal("隐藏标题", memoZh.TitleToggleLabel);

            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            var memoEn = new MemoViewModel(new Memo { IsChecklist = true }, () => { }, _ => { }, _ => { });
            Assert.Equal("Convert to Text", memoEn.ConvertHint);
            Assert.Equal("Show Title", memoEn.TitleToggleLabel);
            memoEn.ShowTitle = true;
            Assert.Equal("Hide Title", memoEn.TitleToggleLabel);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = prevCulture;
        }
    }

    [Fact]
    public void ReorderModeTogglesAndResetsOnFinishEditOrConvert()
    {
        var memo = new MemoViewModel(new Memo
        {
            IsChecklist = true,
            Items = [new() { Text = "item 1", Order = 0 }, new() { Text = "item 2", Order = 1 }]
        }, () => { }, _ => { }, _ => { });

        Assert.False(memo.IsReordering);
        Assert.All(memo.Items, item => Assert.False(item.IsReordering));

        memo.ToggleReorderCommand.Execute(null);
        Assert.True(memo.IsReordering);
        Assert.All(memo.Items, item => Assert.True(item.IsReordering));

        memo.FinishEditCommand.Execute(null);
        Assert.False(memo.IsReordering);
        Assert.All(memo.Items, item => Assert.False(item.IsReordering));

        memo.BeginEditCommand.Execute(null);
        memo.ToggleReorderCommand.Execute(null);
        Assert.True(memo.IsReordering);

        memo.ConvertCommand.Execute(null);
        Assert.False(memo.IsReordering);
    }

    [Fact]
    public void NewMemoIsAtomicallyCreatedInEditingState()
    {
        var today = new TodayViewModel([]);
        today.NewTextCommand.Execute(null);
        Assert.Single(today.Memos);
        var memo = today.Memos[0];
        Assert.True(memo.IsEditing);
        Assert.True(memo.IsEmptyNew);
    }

    [Fact]
    public void ConsecutiveAddReusesEmptyNewMemoAndTriggersFeedback()
    {
        var today = new TodayViewModel([]);
        today.NewTextCommand.Execute(null);
        var memo = today.Memos[0];

        bool feedbackTriggered = false;
        memo.ReuseFeedbackRequested += () => feedbackTriggered = true;

        today.NewTextCommand.Execute(null);

        Assert.Single(today.Memos);
        Assert.Same(memo, today.Memos[0]);
        Assert.True(feedbackTriggered);
    }
}
