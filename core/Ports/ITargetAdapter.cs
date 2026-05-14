using AccountingETL.Core.Domain;

namespace AccountingETL.Core.Ports;

/// <summary>
/// Port for writing data to a target system (database, ERP, API, etc.)
///
/// Supports two modes:
/// 1. **Canonical mode** — predefined schema with upsert by key field
/// 2. **Auto-discovery mode** — dynamic table creation + bulk insert
/// </summary>
public interface ITargetAdapter
{
    /// <summary>
    /// Human-readable name of this target (e.g., "PostgreSQL", "ERPNext")
    /// </summary>
    string TargetName { get; }

    /// <summary>
    /// Fired by the adapter to emit warnings or diagnostic messages to the pipeline log.
    /// </summary>
    event Action<string>? Log;

    /// <summary>
    /// Returns all supported entity types for this target.
    /// Empty list means this adapter does not support canonical mode.
    /// </summary>
    IReadOnlyList<EntityType> SupportedEntities { get; }

    // ===== Mode 1: Canonical =====

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

    // ===== Mode 2: Auto-discovery =====

    /// <summary>
    /// Returns true if this adapter supports auto-discovery mode.
    /// </summary>
    bool SupportsAutoDiscovery => false;

    /// <summary>
    /// Create a table dynamically based on discovered table info.
    /// </summary>
    Task EnsureTableAsync(SourceTableInfo tableInfo, CancellationToken ct = default)
        => throw new NotSupportedException($"Auto-discovery not supported by {TargetName}");

    /// <summary>
    /// Insert all records into a dynamically-created table.
    /// Returns the number of inserted records.
    /// </summary>
    Task<int> InsertAllAsync(string tableName, IEnumerable<Record> records, CancellationToken ct = default)
        => throw new NotSupportedException($"Table-level insert not supported by {TargetName}");

    /// <summary>
    /// Get distinct table names from the target.
    /// </summary>
    Task<ISet<string>> GetTableNamesAsync(CancellationToken ct = default)
        => throw new NotSupportedException($"Table listing not supported by {TargetName}");

    // ===== Common =====

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
