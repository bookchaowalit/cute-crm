namespace AccountingETL.Core.Domain;

/// <summary>
/// Describes a field in a discovered source table.
/// </summary>
public record SourceFieldInfo(
    string Name,
    string DbType,       // "C", "N", "D", "L", "M" (DBF types) or generic "string", "number", "datetime"
    int Length,
    int Decimals = 0)
{
    /// <summary>
    /// Maps the DBF type to a PostgreSQL-compatible type name.
    /// </summary>
    public string PgType => DbType switch
    {
        "N" => Decimals > 0 ? $"numeric(18,{Decimals})" : "bigint",
        "D" => "date",
        "L" => "boolean",
        "M" => "text",
        _ => "text"
    };
}

/// <summary>
/// Describes a discovered source table from auto-discovery.
/// </summary>
public record SourceTableInfo(
    string TableName,        // e.g., "ARMAS" (from ARMAS.DBF)
    string DisplayName,      // e.g., "AR Master (Customers)"
    IReadOnlyList<SourceFieldInfo> Fields,
    int RecordCount,
    string? SourcePath = null);
