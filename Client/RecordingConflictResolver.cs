using CommunityToolkit.Datasync.Client.Offline;

namespace DatasyncConflictRepro.Client;

/// <summary>Records conflict callbacks so tests can assert a 409 path was hit.</summary>
public class RecordingConflictResolver : IConflictResolver
{
    public List<object?> Conflicts { get; } = [];

    public Task<ConflictResolution> ResolveConflictAsync(
        object? clientObject,
        object? serverObject,
        CancellationToken cancellationToken = default)
    {
        Conflicts.Add(clientObject);
        return Task.FromResult(new ConflictResolution
        {
            Result = ConflictResolutionResult.Client,
            Entity = clientObject
        });
    }
}
