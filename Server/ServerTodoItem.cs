using CommunityToolkit.Datasync.Server.EntityFrameworkCore;

namespace DatasyncConflictRepro.Server;

public class ServerTodoItem : RepositoryControlledEntityTableData
{
    public string Title { get; set; } = string.Empty;
}
