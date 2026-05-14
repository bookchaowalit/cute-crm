namespace AccountingETL.Core.Domain;

/// <summary>
/// Configuration for the ETL pipeline — source, target, scheduling, and notifications.
/// This is the canonical config shape; each project may have its own persistence layer.
/// </summary>
public class EtlConfig
{
    // Source settings
    public string SourcePath { get; set; } = string.Empty;
    public string SourceEncoding { get; set; } = "tis-620";

    // Target settings (PostgreSQL)
    public string TargetHost { get; set; } = "localhost";
    public int TargetPort { get; set; } = 5432;
    public string TargetDatabase { get; set; } = "accounting_etl";
    public string TargetUser { get; set; } = "postgres";
    public string TargetPassword { get; set; } = string.Empty;
    public string TargetSchema { get; set; } = "etl_staging";

    // Scheduling
    public int IntervalHours { get; set; } = 24;
    public DateTimeOffset? LastSyncTime { get; set; }

    // Notifications (LINE Notify)
    public string LineNotifyToken { get; set; } = string.Empty;
    public bool NotifyOnSuccess { get; set; } = true;
    public bool NotifyOnFailure { get; set; } = true;

    // UI preferences
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoStart { get; set; } = false;

    // ERPNext direct sync (optional secondary target)
    public string ErpNextUrl { get; set; } = string.Empty;
    public string ErpNextApiKey { get; set; } = string.Empty;
    public string ErpNextApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// Build a connection string for the target database.
    /// </summary>
    public string BuildConnectionString()
    {
        return $"Host={TargetHost};Port={TargetPort};Database={TargetDatabase};" +
               $"Username={TargetUser};Password={TargetPassword};";
    }
}
