using CommunityToolkit.Datasync.Server;
using CommunityToolkit.Datasync.Server.EntityFrameworkCore;
using DatasyncConflictRepro.Client;
using DatasyncConflictRepro.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.ModelBuilder;
using Xunit;

namespace DatasyncConflictRepro;

/// <summary>In-process Datasync server (SQLite) + offline client helpers.</summary>
public sealed class ReproFixture : IAsyncLifetime, IDisposable
{
    private IHost? _host;
    private string? _workDir;

    public TestServer Server { get; private set; } = null!;
    private string ServerDbPath => Path.Combine(_workDir!, "server.db");

    public async Task InitializeAsync()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"datasync-conflict-repro-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);

        var modelBuilder = new ODataConventionModelBuilder();
        modelBuilder.EnableLowerCamelCase();
        modelBuilder.AddEntityType(typeof(ServerTodoItem));
        var edm = modelBuilder.GetEdmModel();

        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer()
                    .UseEnvironment("Development")
                    .ConfigureServices(services =>
                    {
                        services.AddDbContext<ServerDbContext>(options =>
                            options.UseSqlite($"Data Source={ServerDbPath}"));
                        services.AddScoped<IRepository<ServerTodoItem>>(sp =>
                            new EntityTableRepository<ServerTodoItem>(sp.GetRequiredService<ServerDbContext>()));
                        services.AddDatasyncServices(edm);
                        services.AddControllers()
                            .AddApplicationPart(typeof(TodoItemController).Assembly);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
            })
            .StartAsync();

        Server = _host.GetTestServer();

        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task SeedServerTodoAsync(string id, string title)
    {
        await using var db = CreateServerDbContext();
        db.TodoItems.Add(new ServerTodoItem
        {
            Id = id,
            Title = title,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = Guid.NewGuid().ToByteArray(),
            Deleted = false
        });
        await db.SaveChangesAsync();
    }

    public ServerDbContext CreateServerDbContext()
        => new(new DbContextOptionsBuilder<ServerDbContext>()
            .UseSqlite($"Data Source={ServerDbPath}")
            .Options);

    public string NewClientDbPath() => Path.Combine(_workDir!, $"client-{Guid.NewGuid():N}.db");

    public (ClientDbContext Context, RecordingConflictResolver Resolver) CreateClient(
        string clientDbPath,
        RecordingConflictResolver? sharedResolver = null,
        bool ensureCreated = true)
    {
        var http = new HttpClient(Server.CreateHandler()) { BaseAddress = Server.BaseAddress };
        var resolver = sharedResolver ?? new RecordingConflictResolver();
        var context = new ClientDbContext(
            new DbContextOptionsBuilder<ClientDbContext>().UseSqlite($"Data Source={clientDbPath}").Options,
            http,
            resolver);
        if (ensureCreated)
            context.Database.EnsureCreated();
        return (context, resolver);
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
            await _host.StopAsync();
        Dispose();
    }

    public void Dispose()
    {
        _host?.Dispose();
        if (_workDir is not null)
        {
            try { Directory.Delete(_workDir, recursive: true); } catch { /* ignore */ }
        }
    }
}
