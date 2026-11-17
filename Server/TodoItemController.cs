using CommunityToolkit.Datasync.Server;
using CommunityToolkit.Datasync.Server.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace DatasyncConflictRepro.Server;

[Route("tables/todoitem")]
public class TodoItemController(IRepository<ServerTodoItem> repository)
    : TableController<ServerTodoItem>(repository);
