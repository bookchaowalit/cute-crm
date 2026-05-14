using AccountingETL.Core.Domain;

namespace AccountingETL.Core.Ports;

/// <summary>
/// Port for reading data from an accounting program's source format.
/// Each accounting system implements its own adapter (DBF, ODBC, API, CSV, etc.)
/// 
/// Supports two modes:
/// 1. **Canonical mode** — known schema with field mapping (SupportedEntities + ReadAsync)
/// 2. **Auto-discovery mode** — scan and discover all tables dynamically (DiscoverTables + ReadTableAsync)
/// </summary>
public interface ISourceAdapter
{
    /// <summary>
    /// Human-readable name of this source (e.g., "Express Accounting", "SAP B1")
    /// </summary>
    string SourceName { get; }

    // ===== Mode 1: Canonical (known schema) =====

    /// <summary>
    /// Returns all supported entity types for this source.
    /// Empty list means this adapter does not support canonical mode.
    /// </summary>
    IReadOnlyList<EntityType> SupportedEntities { get; }

    /// <summary>
    /// Read all records for a given entity since the specified cutoff time.
    /// If cutoff is null, reads all records.
    /// </summary>
    IAsyncEnumerable<Record> ReadAsync(EntityType entity, DateTimeOffset? cutoff, CancellationToken ct = default);

    // ===== Mode 2: Auto-discovery (dynamic schema) =====

    /// <summary>
    /// Returns true if this adapter supports auto-discovery mode.
    /// </summary>
    bool SupportsAutoDiscovery => false;

    /// <summary>
    /// Scan the source and discover all available tables.
    /// Called once at startup to build the table list.
    /// </summary>
    Task<IReadOnlyList<SourceTableInfo>> DiscoverTablesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SourceTableInfo>>(Array.Empty<SourceTableInfo>());

    /// <summary>
    /// Read all records from a discovered table by name.
    /// </summary>
    IAsyncEnumerable<Record> ReadTableAsync(string tableName, CancellationToken ct = default)
        => throw new NotSupportedException($"Table-level reading not supported by {SourceName}");

    // ===== Common =====

    /// <summary>
    /// Validate that the source path/connection is accessible.
    /// </summary>
    Task<bool> ValidateConnectionAsync(CancellationToken ct = default);
}
