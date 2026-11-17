using CommunityToolkit.Datasync.Client.Offline;
using DatasyncConflictRepro.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatasyncConflictRepro;

/// <summary>
/// Repro + fix: after sync advances Version, saving a stale UI-tracked entity
/// queues the old Version and the next push conflicts.
/// </summary>
public class EditWhileSyncingTests(ReproFixture fixture) : IClassFixture<ReproFixture>
{
    [Fact]
    public async Task StaleUiSave_afterSync_queuesOldVersion_andFollowUpPushConflicts()
    {
        var todoId = Guid.NewGuid().ToString("N");
        var (syncCtx, uiCtx, resolver, versionAfterPull, uiItem) = await ArrangeStaleUiAsync(todoId);
        await using (syncCtx)
        await using (uiCtx)
        {
            uiItem.Title = "second-edit-stale";
            uiItem.UpdatedAt = DateTimeOffset.UtcNow;
            await uiCtx.SaveChangesAsync();

            var pending = await uiCtx.DatasyncOperationsQueue.SingleAsync(o => o.ItemId == todoId);
            Assert.Equal(versionAfterPull, pending.EntityVersion);

            await syncCtx.PushAsync();
            Assert.NotEmpty(resolver.Conflicts);
        }
    }

    [Fact]
    public async Task RebaseUiSave_afterSync_queuesFreshVersion_andFollowUpPushSucceeds()
    {
        var todoId = Guid.NewGuid().ToString("N");
        var (syncCtx, uiCtx, resolver, versionAfterPull, _) = await ArrangeStaleUiAsync(todoId);
        await using (syncCtx)
        await using (uiCtx)
        {
            const string secondTitle = "second-edit-rebased";
            await RebaseTodoSave.SaveTitleAsync(uiCtx, todoId, secondTitle);

            var pending = await uiCtx.DatasyncOperationsQueue.SingleAsync(o => o.ItemId == todoId);
            Assert.NotEqual(versionAfterPull, pending.EntityVersion);

            var push = await syncCtx.PushAsync();
            Assert.True(push.IsSuccessful);
            Assert.Empty(resolver.Conflicts);

            await using var serverDb = fixture.CreateServerDbContext();
            Assert.Equal(secondTitle, (await serverDb.TodoItems.SingleAsync(t => t.Id == todoId)).Title);
        }
    }

    /// <summary>
    /// Pull → first edit → load UI entity @ V1 → push+pull (server/SQLite → V2).
    /// UI change tracker still holds V1.
    /// </summary>
    private async Task<(
        ClientDbContext SyncCtx,
        ClientDbContext UiCtx,
        RecordingConflictResolver Resolver,
        string? VersionAfterPull,
        ClientTodoItem UiItem)> ArrangeStaleUiAsync(string todoId)
    {
        await fixture.SeedServerTodoAsync(todoId, "seed");

        var clientDb = fixture.NewClientDbPath();
        var (syncCtx, resolver) = fixture.CreateClient(clientDb);
        Assert.True((await syncCtx.Todos.PullAsync()).IsSuccessful);

        var first = await syncCtx.Todos.SingleAsync(t => t.Id == todoId);
        var versionAfterPull = first.Version;
        first.Title = "first-edit";
        first.UpdatedAt = DateTimeOffset.UtcNow;
        await syncCtx.SaveChangesAsync();

        var (uiCtx, _) = fixture.CreateClient(clientDb, sharedResolver: resolver, ensureCreated: false);
        var uiItem = await uiCtx.Todos.SingleAsync(t => t.Id == todoId);

        Assert.True((await syncCtx.PushAsync()).IsSuccessful);
        Assert.True((await syncCtx.PullAsync()).IsSuccessful);
        Assert.Empty(resolver.Conflicts);

        return (syncCtx, uiCtx, resolver, versionAfterPull, uiItem);
    }
}
