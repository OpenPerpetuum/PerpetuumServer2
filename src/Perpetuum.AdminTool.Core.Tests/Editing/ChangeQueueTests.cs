using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.Core.Tests.Editing;

public sealed class ChangeQueueTests
{
    [Fact]
    public void Clear_RemovesChangesAndAssociatedEntityNames()
    {
        var queue = new ChangeQueue();
        queue.Add(new RawSqlChange("test", "SELECT 1;"));
        queue.AddNewEntityName("definition_test");

        queue.Clear();

        Assert.False(queue.HasPending);
        Assert.Empty(queue.Items);
        Assert.Empty(queue.PendingNewEntityNames);
    }

    [Fact]
    public void Remove_LeavesChangesQueuedAfterTheOperationSnapshot()
    {
        var queue = new ChangeQueue();
        var exported = new RawSqlChange("exported", "SELECT 1;");
        var addedLater = new RawSqlChange("added later", "SELECT 2;");
        queue.Add(exported);
        queue.Add(addedLater);
        queue.AddNewEntityName("definition_added_later");

        queue.Remove([exported]);

        Assert.Equal([addedLater], queue.Items);
        Assert.Equal(["definition_added_later"], queue.PendingNewEntityNames);
    }
}
