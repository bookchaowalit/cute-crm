namespace AccountingETL.Core.Domain;

/// <summary>
/// A sync log entry recording what happened during a sync operation.
/// </summary>
public record SyncLog(
    DateTimeOffset SyncTime,
    EntityType Entity,
    int Inserted,
    int Updated,
    int Skipped,
    TimeSpan Duration,
    string Status,           // "success", "partial", "failed"
    string? ErrorMessage = null);
