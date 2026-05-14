using System.Collections.Concurrent;
using System.Linq;
using AccountingETL.Core.Domain;
using AccountingETL.Core.Exceptions;
using AccountingETL.Core.Ports;
using Npgsql;
using NpgsqlTypes;

namespace AccountingETL.Adapters.PostgreSQL;

/// <summary>
/// PostgreSQL target adapter using high-performance COPY protocol.
/// </summary>
public class PostgreSqlTargetAdapter : ITargetAdapter
{
    private readonly string _connectionString;
    private readonly string _schema;

    public string TargetName => "PostgreSQL";

    public IReadOnlyList<EntityType> SupportedEntities { get; } = new[]
    {
        EntityType.Customer,
        EntityType.Supplier,
        EntityType.Item,
        EntityType.ArInvoice,
        EntityType.ArInvoiceLine,
        EntityType.ApInvoice,
        EntityType.ApInvoiceLine,
        EntityType.GlTransaction,
    };

    public bool SupportsAutoDiscovery => true;

    public PostgreSqlTargetAdapter(string connectionString, string schema = "etl_staging")
    {
        _connectionString = connectionString;
        _schema = schema;
    }

    public async Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ===== Auto-Discovery Mode =====

    public async Task EnsureTableAsync(SourceTableInfo tableInfo, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var columns = string.Join(",\n    ", tableInfo.Fields.Select(f =>
            $"\"{f.Name}\" {f.PgType}"));

        var sql = $"""
            CREATE TABLE IF NOT EXISTS "{_schema}"."{tableInfo.TableName}" (
                {columns}
            );
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> InsertAllAsync(string tableName, IEnumerable<Record> records, CancellationToken ct = default)
    {
        var recordsList = records.ToList();
        if (recordsList.Count == 0) return 0;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Get column names AND types from table schema
        var schemaSql = $"""
            SELECT column_name, udt_name
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table
            ORDER BY ordinal_position
            """;
        var columns = new List<(string Name, string PgType)>();
        await using (var colCmd = new NpgsqlCommand(schemaSql, conn))
        {
            colCmd.Parameters.AddWithValue("schema", _schema);
            colCmd.Parameters.AddWithValue("table", tableName);
            await using var colReader = await colCmd.ExecuteReaderAsync(ct);
            while (await colReader.ReadAsync(ct))
                columns.Add((colReader.GetString(0), colReader.GetString(1)));
        }

        if (columns.Count == 0) return 0;

        // Build INSERT statement - all values sent as text, PostgreSQL handles type conversion
        var colNames = string.Join(", ", columns.Select(c => $"\"{c.Name}\""));
        var paramNames = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
        var insertSql = $"INSERT INTO \"{_schema}\".\"{tableName}\" ({colNames}) VALUES ({paramNames})";

        int inserted = 0;
        await using var cmd = new NpgsqlCommand(insertSql, conn);

        foreach (var record in recordsList)
        {
            cmd.Parameters.Clear();
            for (int i = 0; i < columns.Count; i++)
            {
                var (colName, pgType) = columns[i];
                object? val = DBNull.Value;

                if (record.TryGetValue(colName, out var rawVal) && rawVal != null)
                {
                    // Convert to string - PostgreSQL will parse it based on column type
                    val = rawVal.ToString()?.Replace("\0", "") ?? "";
                }

                // Use AddWithValue then override NpgsqlDbType - this forces Npgsql to use Text type
                // even when the underlying .NET value is a DateTime/DateTimeOffset
                cmd.Parameters.AddWithValue($"@p{i}", val ?? DBNull.Value);
                cmd.Parameters[i].NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text;
            }

            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
                inserted++;
            }
            catch
            {
                // Per-row insert failed — skip this row but continue with others
            }
        }

        return inserted;
    }

    /// <summary>
    /// Convert a raw DBF value to the correct .NET type for the PostgreSQL column.
    /// For timestamp/date columns, returns ISO 8601 string to avoid Npgsql DateTimeOffset wrapping.
    /// </summary>
    private static object? ConvertValue(object? value, string pgType)
    {
        if (value == null) return DBNull.Value;

        var str = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(str)) return DBNull.Value;

        try
        {
            // For timestamp/date columns, return ISO 8601 string
            // This avoids Npgsql's DateTimeOffset wrapping issue entirely
            if (pgType == "date" || pgType == "timestamp" || pgType == "timestamptz")
            {
                DateTime parsedDate;
                if (DateTime.TryParseExact(str, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out parsedDate))
                {
                    // Return as ISO 8601 date string (PostgreSQL parses this natively)
                    return pgType == "date"
                        ? parsedDate.ToString("yyyy-MM-dd")
                        : parsedDate.ToString("yyyy-MM-ddTHH:mm:ss.000000Z");
                }
                if (DateTime.TryParse(str, out parsedDate))
                {
                    return pgType == "date"
                        ? parsedDate.ToString("yyyy-MM-dd")
                        : parsedDate.ToString("yyyy-MM-ddTHH:mm:ss.000000Z");
                }
                return DBNull.Value;
            }

            switch (pgType)
            {
                case "int2":
                case "int4":
                case "int8":
                case "bigint":
                    if (int.TryParse(str, out int i)) return i;
                    if (long.TryParse(str, out long l)) return l;
                    return 0;

                case "numeric":
                case "decimal":
                case "float4":
                case "float8":
                    if (decimal.TryParse(str, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal d))
                        return d;
                    return 0m;

                case "bool":
                case "boolean":
                    return str.Equals("T", StringComparison.OrdinalIgnoreCase) ||
                           str.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                           str.Equals("1", StringComparison.OrdinalIgnoreCase);

                default:
                    // text, varchar, etc. — strip null bytes
                    return str.Replace("\0", "");
            }
        }
        catch
        {
            // Fallback: return as text with null bytes stripped
            return str.Replace("\0", "");
        }
    }

    public async Task<ISet<string>> GetTableNamesAsync(CancellationToken ct = default)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = $"SELECT table_name FROM information_schema.tables WHERE table_schema = @schema";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", _schema);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    // ===== Canonical Mode =====

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = $"""
            CREATE SCHEMA IF NOT EXISTS "{_schema}";

            CREATE TABLE IF NOT EXISTS "{_schema}".customers (
                code TEXT PRIMARY KEY,
                name TEXT,
                address TEXT,
                tax_id TEXT,
                phone TEXT,
                email TEXT,
                credit_limit DECIMAL(18,2),
                discount DECIMAL(5,2),
                is_active BOOLEAN
            );

            CREATE TABLE IF NOT EXISTS "{_schema}".suppliers (
                code TEXT PRIMARY KEY,
                name TEXT,
                address TEXT,
                tax_id TEXT,
                phone TEXT,
                email TEXT,
                payment_terms TEXT,
                is_active BOOLEAN
            );

            CREATE TABLE IF NOT EXISTS "{_schema}".items (
                code TEXT PRIMARY KEY,
                name TEXT,
                unit TEXT,
                cost_price DECIMAL(18,2),
                sale_price DECIMAL(18,2),
                stock_qty DECIMAL(18,4),
                is_active BOOLEAN
            );

            CREATE TABLE IF NOT EXISTS "{_schema}".ar_invoices (
                invoice_no TEXT PRIMARY KEY,
                customer_code TEXT,
                invoice_date TIMESTAMPTZ,
                due_date TIMESTAMPTZ,
                total_amount DECIMAL(18,2),
                tax_amount DECIMAL(18,2),
                discount_amount DECIMAL(18,2),
                net_amount DECIMAL(18,2),
                is_void BOOLEAN
            );

            CREATE TABLE IF NOT EXISTS "{_schema}".ar_invoice_lines (
                invoice_no TEXT,
                line_no INTEGER,
                item_code TEXT,
                quantity DECIMAL(18,4),
                unit_price DECIMAL(18,2),
                amount DECIMAL(18,2),
                PRIMARY KEY (invoice_no, line_no)
            );

            CREATE TABLE IF NOT EXISTS "{_schema}".ap_invoices (
                invoice_no TEXT PRIMARY KEY,
                supplier_code TEXT,
                invoice_date TIMESTAMPTZ,
                due_date TIMESTAMPTZ,
                total_amount DECIMAL(18,2),
                tax_amount DECIMAL(18,2),
                discount_amount DECIMAL(18,2),
                net_amount DECIMAL(18,2),
                is_void BOOLEAN
            );

            CREATE TABLE IF NOT EXISTS "{_schema}".ap_invoice_lines (
                invoice_no TEXT,
                line_no INTEGER,
                item_code TEXT,
                quantity DECIMAL(18,4),
                unit_price DECIMAL(18,2),
                amount DECIMAL(18,2),
                PRIMARY KEY (invoice_no, line_no)
            );

            CREATE TABLE IF NOT EXISTS "{_schema}".gl_transactions (
                doc_no TEXT,
                doc_date TIMESTAMPTZ,
                account_code TEXT,
                description TEXT,
                debit_amount DECIMAL(18,2),
                credit_amount DECIMAL(18,2),
                PRIMARY KEY (doc_no, account_code)
            );

            CREATE TABLE IF NOT EXISTS "{_schema}".sync_log (
                id SERIAL PRIMARY KEY,
                sync_time TIMESTAMPTZ NOT NULL,
                entity TEXT NOT NULL,
                inserted INTEGER NOT NULL,
                updated INTEGER NOT NULL,
                skipped INTEGER NOT NULL,
                duration_ms BIGINT NOT NULL,
                status TEXT NOT NULL,
                error_message TEXT
            );
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ISet<string>> GetExistingKeysAsync(EntityType entity, string keyField, CancellationToken ct = default)
    {
        var tableName = GetTableName(entity);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = $"SELECT \"{keyField}\" FROM \"{_schema}\".\"{tableName}\"";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var val = reader.GetValue(0);
            if (val != DBNull.Value)
                keys.Add(val.ToString()!);
        }

        return keys;
    }

    public async Task<SyncCounts> UpsertAsync(EntityType entity, IEnumerable<Record> records, string keyField, CancellationToken ct = default)
    {
        var existingKeys = await GetExistingKeysAsync(entity, keyField, ct);
        var newRecords = new List<Record>();
        var existingRecords = new List<Record>();

        foreach (var r in records)
        {
            var key = r.Get<string>(keyField);
            if (string.IsNullOrEmpty(key) || existingKeys.Contains(key))
                existingRecords.Add(r);
            else
                newRecords.Add(r);
        }

        int inserted = 0;
        int updated = 0;

        if (newRecords.Count > 0)
            inserted = await CopyToTableAsync(entity, newRecords, ct);

        if (existingRecords.Count > 0)
            updated = await CopyToTempAndUpdateAsync(entity, existingRecords, keyField, ct);

        return new SyncCounts(inserted, updated, 0);
    }

    private string GetTableName(EntityType entity) => entity switch
    {
        EntityType.Customer => "customers",
        EntityType.Supplier => "suppliers",
        EntityType.Item => "items",
        EntityType.ArInvoice => "ar_invoices",
        EntityType.ArInvoiceLine => "ar_invoice_lines",
        EntityType.ApInvoice => "ap_invoices",
        EntityType.ApInvoiceLine => "ap_invoice_lines",
        EntityType.GlTransaction => "gl_transactions",
        _ => throw new ArgumentException($"Unknown entity: {entity}", nameof(entity))
    };

    private async Task<int> CopyToTableAsync(EntityType entity, List<Record> records, CancellationToken ct)
    {
        var tableName = GetTableName(entity);
        var fields = EntitySchema.GetFields(entity).Keys.ToArray();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var writer = conn.BeginBinaryImport(
            $"COPY \"{_schema}\".\"{tableName}\" ({string.Join(",", fields.Select(f => $"\"{f}\""))}) FROM STDIN (FORMAT BINARY)");

        foreach (var record in records)
        {
            await writer.StartRowAsync(ct);
            foreach (var field in fields)
            {
                var val = record.TryGetValue(field, out var v) ? v : null;
                await writer.WriteAsync(val, ct);
            }
        }

        await writer.CompleteAsync(ct);
        return records.Count;
    }

    private async Task<int> CopyToTempAndUpdateAsync(EntityType entity, List<Record> records, string keyField, CancellationToken ct)
    {
        var tableName = GetTableName(entity);
        var fields = EntitySchema.GetFields(entity).Keys.ToArray();
        var tempTable = $"{tableName}_temp";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Create temp table
        var fieldDefs = string.Join(", ", fields.Select(f => $"\"{f}\" TEXT"));
        await using var createCmd = new NpgsqlCommand(
            $"CREATE TEMP TABLE \"{tempTable}\" ({fieldDefs}) ON COMMIT DROP;", conn);
        await createCmd.ExecuteNonQueryAsync(ct);

        // COPY to temp table
        await using var writer = conn.BeginBinaryImport(
            $"COPY \"{tempTable}\" ({string.Join(",", fields.Select(f => $"\"{f}\""))}) FROM STDIN (FORMAT BINARY)");

        foreach (var record in records)
        {
            await writer.StartRowAsync(ct);
            foreach (var field in fields)
            {
                var val = record.TryGetValue(field, out var v) ? v : null;
                await writer.WriteAsync(val, ct);
            }
        }
        await writer.CompleteAsync(ct);

        // UPDATE from temp
        var setClause = string.Join(", ", fields
            .Where(f => !string.Equals(f, keyField, StringComparison.OrdinalIgnoreCase))
            .Select(f => $"\"{f}\" = t.\"{f}\""));

        var updateSql = $"""
            UPDATE "{_schema}"."{tableName}" AS tgt
            SET {setClause}
            FROM "{tempTable}" AS t
            WHERE tgt."{keyField}" = t."{keyField}"
            """;

        await using var updateCmd = new NpgsqlCommand(updateSql, conn);
        return await updateCmd.ExecuteNonQueryAsync(ct);
    }

    public async Task WriteSyncLogAsync(SyncLog log, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = $"""
            INSERT INTO "{_schema}".sync_log (sync_time, entity, inserted, updated, skipped, duration_ms, status, error_message)
            VALUES (@syncTime, @entity, @inserted, @updated, @skipped, @durationMs, @status, @errorMessage)
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("syncTime", log.SyncTime);
        cmd.Parameters.AddWithValue("entity", log.Entity.ToString());
        cmd.Parameters.AddWithValue("inserted", log.Inserted);
        cmd.Parameters.AddWithValue("updated", log.Updated);
        cmd.Parameters.AddWithValue("skipped", log.Skipped);
        cmd.Parameters.AddWithValue("durationMs", (long)log.Duration.TotalMilliseconds);
        cmd.Parameters.AddWithValue("status", log.Status);
        cmd.Parameters.AddWithValue("errorMessage", (object?)log.ErrorMessage ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<SyncLog>> ReadSyncHistoryAsync(int limit = 100, CancellationToken ct = default)
    {
        var logs = new List<SyncLog>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = $"""
            SELECT sync_time, entity, inserted, updated, skipped, duration_ms, status, error_message
            FROM "{_schema}".sync_log
            ORDER BY sync_time DESC
            LIMIT @limit
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var entityStr = reader.GetString(1);
            Enum.TryParse<EntityType>(entityStr, out var entity);

            logs.Add(new SyncLog(
                SyncTime: reader.GetDateTime(0),
                Entity: entity,
                Inserted: reader.GetInt32(2),
                Updated: reader.GetInt32(3),
                Skipped: reader.GetInt32(4),
                Duration: TimeSpan.FromMilliseconds(reader.GetInt64(5)),
                Status: reader.GetString(6),
                ErrorMessage: reader.IsDBNull(7) ? null : reader.GetString(7)
            ));
        }

        return logs;
    }
}
