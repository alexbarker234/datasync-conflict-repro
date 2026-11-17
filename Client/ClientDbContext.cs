using CommunityToolkit.Datasync.Client.Offline;
using Microsoft.EntityFrameworkCore;

namespace DatasyncConflictRepro.Client;

public class ClientDbContext(
    DbContextOptions<ClientDbContext> options,
    HttpClient httpClient,
    IConflictResolver conflictResolver) : OfflineDbContext(options)
{
    public DbSet<ClientTodoItem> Todos => Set<ClientTodoItem>();

    protected override void OnDatasyncInitialization(DatasyncOfflineOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseHttpClient(httpClient)
            .UseDefaultConflictResolver(conflictResolver)
            .Entity<ClientTodoItem>(cfg => cfg.Endpoint = new Uri("/tables/todoitem", UriKind.Relative));
    }
}
