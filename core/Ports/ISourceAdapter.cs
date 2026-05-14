using AccountingETL.Core.Domain;

namespace AccountingETL.Core.Ports;

/// <summary>
/// Port for reading data from an accounting program's source format.
/// Each accounting system implements its own adapter (DBF, ODBC, API, CSV, etc.)
/// </summary>
public interface ISourceAdapter
{
    /// <summary>
    /// Human-readable name of this source (e.g., "Express Accounting", "SAP B1")
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Returns all supported entity types for this source.
    /// </summary>
    IReadOnlyList<EntityType> SupportedEntities { get; }

    /// <summary>
    /// Read all records for a given entity since the specified cutoff time.
    /// If cutoff is null, reads all records.
    /// </summary>
    IAsyncEnumerable<Record> ReadAsync(EntityType entity, DateTimeOffset? cutoff, CancellationToken ct = default);

    /// <summary>
    /// Validate that the source path/connection is accessible.
    /// </summary>
    Task<bool> ValidateConnectionAsync(CancellationToken ct = default);
}
