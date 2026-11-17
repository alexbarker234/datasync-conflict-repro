using Microsoft.EntityFrameworkCore;

namespace DatasyncConflictRepro.Client;

/// <summary>
/// After sync may have advanced Version: detach stale tracker state,
/// reload from SQLite, apply field changes, then save.
/// </summary>
public static class RebaseTodoSave
{
    public static async Task SaveTitleAsync(ClientDbContext db, string todoId, string newTitle)
    {
        foreach (var entry in db.ChangeTracker.Entries<ClientTodoItem>()
                     .Where(e => e.Entity.Id == todoId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        var fresh = await db.Todos.SingleAsync(t => t.Id == todoId);
        fresh.Title = newTitle;
        fresh.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }
}
