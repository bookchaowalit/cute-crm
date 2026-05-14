using AccountingETL.Core.Domain;

namespace AccountingETL.Core.Ports;

/// <summary>
/// Port for writing data to a target system (database, ERP, API, etc.)
/// </summary>
public interface ITargetAdapter
{
    /// <summary>
    /// Human-readable name of this target (e.g., "PostgreSQL", "ERPNext")
    /// </summary>
    string TargetName { get; }

    /// <summary>
    /// Returns all supported entity types for this target.
    /// </summary>
    IReadOnlyList<EntityType> SupportedEntities { get; }

    /// <summary>
    /// Ensure the target schema/tables exist. Called once at startup.
    /// </summary>
    Task EnsureSchemaAsync(CancellationToken ct = default);

    /// <summary>
    /// Batch upsert records into the target system.
    /// Returns (inserted, updated, skipped) counts.
    /// </summary>
    Task<SyncCounts> UpsertAsync(EntityType entity, IEnumerable<Record> records, string keyField, CancellationToken ct = default);

    /// <summary>
    /// Get distinct values of a key field from the target (for determining new vs existing).
    /// </summary>
    Task<ISet<string>> GetExistingKeysAsync(EntityType entity, string keyField, CancellationToken ct = default);

    /// <summary>
    /// Write a sync log entry.
    /// </summary>
    Task WriteSyncLogAsync(SyncLog log, CancellationToken ct = default);

    /// <summary>
    /// Read sync history for display in UI.
    /// </summary>
    Task<IReadOnlyList<SyncLog>> ReadSyncHistoryAsync(int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Validate that the target connection is accessible.
    /// </summary>
    Task<bool> ValidateConnectionAsync(CancellationToken ct = default);
}
