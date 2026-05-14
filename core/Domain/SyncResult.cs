namespace AccountingETL.Core.Domain;

/// <summary>
/// Result counts from a single sync operation.
/// </summary>
public record SyncCounts(int Inserted, int Updated, int Skipped)
{
    public int Total => Inserted + Updated + Skipped;

    public static SyncCounts Zero => new(0, 0, 0);

    public SyncCounts Add(SyncCounts other) =>
        new(Inserted + other.Inserted, Updated + other.Updated, Skipped + other.Skipped);
}

/// <summary>
/// Result of syncing a single entity type.
/// </summary>
public record SyncResult(
    EntityType Entity,
    SyncCounts Counts,
    TimeSpan Duration,
    string Status,       // "success", "partial", "failed"
    string? ErrorMessage = null);
