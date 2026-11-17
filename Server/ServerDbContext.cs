using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DatasyncConflictRepro.Server;

public class ServerDbContext(DbContextOptions<ServerDbContext> options) : DbContext(options)
{
    public DbSet<ServerTodoItem> TodoItems => Set<ServerTodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite cannot OrderBy/Filter DateTimeOffset in SQL without a conversion.
        modelBuilder.Entity<ServerTodoItem>()
            .Property(e => e.UpdatedAt)
            .HasConversion(new SqliteDateTimeOffsetConverter());

        base.OnModelCreating(modelBuilder);
    }

    private sealed class SqliteDateTimeOffsetConverter()
        : ValueConverter<DateTimeOffset, long>(
            v => v.ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v));
}
