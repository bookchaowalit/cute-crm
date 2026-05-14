using AccountingETL.Core.Domain;

namespace AccountingETL.Core.Ports;

/// <summary>
/// The main ETL orchestration port. Coordinates source → transform → target.
/// </summary>
public interface IEtlPipeline
{
    event Action<string>? Log;

    IReadOnlyList<EntityType> SyncedEntities { get; }

    /// <summary>
    /// Run a full sync for all entities (or a specific one).
    /// </summary>
    Task<IReadOnlyList<SyncResult>> RunAsync(
        EntityType? entity = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Ensure target schema is ready.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);
}
