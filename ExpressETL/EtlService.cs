using Npgsql;
using System.Text;

namespace ExpressETL;

/// <summary>
/// ETL: อ่าน DBF → Transform → UPSERT เข้า PostgreSQL
/// </summary>
public class EtlService
{
    private readonly AppConfig _config;
    private readonly DbfReader _dbfReader;
    public event EventHandler<string>? OnLog;

    public EtlService(AppConfig config)
    {
        _config = config;
        _dbfReader = new DbfReader("tis-620");
    }

    private void Log(string msg)
    {
        OnLog?.Invoke(this, msg);
    }

    public async Task RunAsync(IProgress<string>? progress = null)
    {
        Log("====== ETL เริ่มทำงาน ======");

        try
        {
            await using var conn = new NpgsqlConnection(_config.ConnectionString);
            await conn.OpenAsync();

            await SyncCustomersAsync(conn, progress);
            await SyncSuppliersAsync(conn, progress);
            await SyncItemsAsync(conn, progress);

            Log("====== ETL เสร็จสิ้น ======");
        }
        catch (Exception ex)
        {
            Log($"✗ ETL ผิดพลาด: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var conn = new NpgsqlConnection(_config.ConnectionString);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ===== Customers (ARMAS.DBF) =====
    private async Task SyncCustomersAsync(NpgsqlConnection conn, IProgress<string>? progress)
    {
        var filePath = Path.Combine(_config.DbfPath, "ARMAS.DBF");
        if (!File.Exists(filePath))
        {
            var alts = new[] { "AR_MAS.DBF", "CUSTMAS.DBF", "CUSTOMER.DBF" };
            foreach (var a in alts)
            {
                var p = Path.Combine(_config.DbfPath, a);
                if (File.Exists(p)) { Log($"⚠ ARMAS.DBF ไม่พบ แต่พบ {a}"); filePath = p; break; }
            }
        }
        if (!File.Exists(filePath))
        {
            Log($"⚠ ไม่พบ Customer file — ข้าม");
            LogDbfFiles(_config.DbfPath);
            return;
        }

        Log($"อ่าน {Path.GetFileName(filePath)} ...");
        LogDbfStructure(filePath);
        var records = _dbfReader.Read(filePath);
        Log($"พบ Customer {records.Count} รายการ");
        if (records.Count > 0)
            Log($"Fields: {string.Join(", ", records[0].Keys.Take(5))}");

        int inserted = 0, updated = 0;

        foreach (var r in records)
        {
            string code = GetString(r, "CUSTCODE");
            if (string.IsNullOrWhiteSpace(code)) continue;

            string name = GetString(r, "CUSTNAME");
            string taxId = GetString(r, "TAXID");
            string address = GetString(r, "ADDRESS");
            string tel = GetString(r, "TEL");
            int creditDay = GetInt(r, "CREDITDAY");

            bool exists = await RecordExistsAsync(conn, "express_staging.customers", "express_code", code);
            var sql = exists
                ? """
                  UPDATE express_staging.customers SET
                    name = @name, tax_id = @tax_id, address = @address,
                    tel = @tel, credit_day = @credit_day, updated_at = NOW()
                  WHERE express_code = @code
                  """
                : """
                  INSERT INTO express_staging.customers
                    (express_code, name, tax_id, address, tel, credit_day, updated_at)
                  VALUES (@code, @name, @tax_id, @address, @tel, @credit_day, NOW())
                  ON CONFLICT (express_code) DO UPDATE SET
                    name = EXCLUDED.name, tax_id = EXCLUDED.tax_id,
                    address = EXCLUDED.address, tel = EXCLUDED.tel,
                    credit_day = EXCLUDED.credit_day, updated_at = NOW()
                  """;

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("code", code);
            cmd.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tax_id", (object?)taxId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("address", (object?)address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tel", (object?)tel ?? DBNull.Value);
            cmd.Parameters.AddWithValue("credit_day", creditDay);
            await cmd.ExecuteNonQueryAsync();

            if (exists) updated++; else inserted++;
        }

        Log($"✓ Customer: เพิ่ม {inserted}, อัปเดต {updated}");
        progress?.Report($"Customer: +{inserted} ~{updated}");
    }

    // ===== Suppliers (APMAS.DBF) =====
    private async Task SyncSuppliersAsync(NpgsqlConnection conn, IProgress<string>? progress)
    {
        var filePath = Path.Combine(_config.DbfPath, "APMAS.DBF");
        if (!File.Exists(filePath))
        {
            var alts = new[] { "AP_MAS.DBF", "VENDMAS.DBF", "SUPPLIER.DBF" };
            foreach (var a in alts)
            {
                var p = Path.Combine(_config.DbfPath, a);
                if (File.Exists(p)) { Log($" APMAS.DBF ไม่พบ แต่พบ {a}"); filePath = p; break; }
            }
        }
        if (!File.Exists(filePath))
        {
            Log($"⚠ ไม่พบ Supplier file — ข้าม");
            return;
        }

        Log($"อ่าน {Path.GetFileName(filePath)} ...");
        LogDbfStructure(filePath);
        var records = _dbfReader.Read(filePath);
        Log($"พบ Supplier {records.Count} รายการ");
        if (records.Count > 0)
            Log($"Fields: {string.Join(", ", records[0].Keys.Take(5))}");

        int inserted = 0, updated = 0;

        foreach (var r in records)
        {
            string code = GetString(r, "VENDCODE");
            if (string.IsNullOrWhiteSpace(code)) continue;

            string name = GetString(r, "VENDNAME");
            string taxId = GetString(r, "TAXID");
            string address = GetString(r, "ADDRESS");
            string tel = GetString(r, "TEL");
            int creditDay = GetInt(r, "CREDITDAY");

            bool exists = await RecordExistsAsync(conn, "express_staging.suppliers", "express_code", code);
            var sql = exists
                ? """
                  UPDATE express_staging.suppliers SET
                    name = @name, tax_id = @tax_id, address = @address,
                    tel = @tel, credit_day = @credit_day, updated_at = NOW()
                  WHERE express_code = @code
                  """
                : """
                  INSERT INTO express_staging.suppliers
                    (express_code, name, tax_id, address, tel, credit_day, updated_at)
                  VALUES (@code, @name, @tax_id, @address, @tel, @credit_day, NOW())
                  ON CONFLICT (express_code) DO UPDATE SET
                    name = EXCLUDED.name, tax_id = EXCLUDED.tax_id,
                    address = EXCLUDED.address, tel = EXCLUDED.tel,
                    credit_day = EXCLUDED.credit_day, updated_at = NOW()
                  """;

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("code", code);
            cmd.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tax_id", (object?)taxId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("address", (object?)address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tel", (object?)tel ?? DBNull.Value);
            cmd.Parameters.AddWithValue("credit_day", creditDay);
            await cmd.ExecuteNonQueryAsync();

            if (exists) updated++; else inserted++;
        }

        Log($"✓ Supplier: เพิ่ม {inserted}, อัปเดต {updated}");
        progress?.Report($"Supplier: +{inserted} ~{updated}");
    }

    // ===== Items (ICMAS.DBF) =====
    private async Task SyncItemsAsync(NpgsqlConnection conn, IProgress<string>? progress)
    {
        var filePath = Path.Combine(_config.DbfPath, "ICMAS.DBF");
        if (!File.Exists(filePath))
        {
            var alts = new[] { "IC_MAS.DBF", "ITEMMAS.DBF", "ITEMS.DBF" };
            foreach (var a in alts)
            {
                var p = Path.Combine(_config.DbfPath, a);
                if (File.Exists(p)) { Log($" ICMAS.DBF ไม่พบ แต่พบ {a}"); filePath = p; break; }
            }
        }
        if (!File.Exists(filePath))
        {
            Log($"⚠ ไม่พบ Item file — ข้าม");
            return;
        }

        Log($"อ่าน {Path.GetFileName(filePath)} ...");
        LogDbfStructure(filePath);
        var records = _dbfReader.Read(filePath);
        Log($"พบ Item {records.Count} รายการ");
        if (records.Count > 0)
            Log($"Fields: {string.Join(", ", records[0].Keys.Take(5))}");

        int inserted = 0, updated = 0;

        foreach (var r in records)
        {
            string code = GetString(r, "ITEMCODE");
            if (string.IsNullOrWhiteSpace(code)) continue;

            string name = GetString(r, "ITEMNAME");
            decimal salePrice = GetDecimal(r, "SALEPRICE");
            decimal costPrice = GetDecimal(r, "COSTPRICE");
            string unit = GetString(r, "UNIT");

            bool exists = await RecordExistsAsync(conn, "express_staging.items", "item_code", code);
            var sql = exists
                ? """
                  UPDATE express_staging.items SET
                    item_name = @name, sale_price = @sale_price,
                    cost_price = @cost_price, unit = @unit, updated_at = NOW()
                  WHERE item_code = @code
                  """
                : """
                  INSERT INTO express_staging.items
                    (item_code, item_name, sale_price, cost_price, unit, updated_at)
                  VALUES (@code, @name, @sale_price, @cost_price, @unit, NOW())
                  ON CONFLICT (item_code) DO UPDATE SET
                    item_name = EXCLUDED.item_name, sale_price = EXCLUDED.sale_price,
                    cost_price = EXCLUDED.cost_price, unit = EXCLUDED.unit,
                    updated_at = NOW()
                  """;

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("code", code);
            cmd.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sale_price", salePrice);
            cmd.Parameters.AddWithValue("cost_price", costPrice);
            cmd.Parameters.AddWithValue("unit", (object?)unit ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();

            if (exists) updated++; else inserted++;
        }

        Log($"✓ Item: เพิ่ม {inserted}, อัปเดต {updated}");
        progress?.Report($"Item: +{inserted} ~{updated}");
    }

    // ===== Helpers =====
    private async Task<bool> RecordExistsAsync(NpgsqlConnection conn, string table, string col, string val)
    {
        using var cmd = new NpgsqlCommand(
            $"SELECT 1 FROM {table} WHERE {col} = @val LIMIT 1", conn);
        cmd.Parameters.AddWithValue("val", val);
        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    private static string GetString(Dictionary<string, object> record, string key)
    {
        if (record.TryGetValue(key, out var val))
            return val?.ToString()?.Trim() ?? "";
        return "";
    }

    private static int GetInt(Dictionary<string, object> record, string key)
    {
        if (record.TryGetValue(key, out var val) && val is int i) return i;
        if (record.TryGetValue(key, out var dec) && dec is decimal d) return (int)d;
        return 0;
    }

    private static decimal GetDecimal(Dictionary<string, object> record, string key)
    {
        if (record.TryGetValue(key, out var val) && val is decimal d) return d;
        if (record.TryGetValue(key, out var i) && i is int n) return n;
        return 0;
    }

    // ===== Diagnostic helpers =====
    private void LogDbfFiles(string dirPath)
    {
        if (!Directory.Exists(dirPath))
        {
            Log($"⚠ DBF path ไม่พบ: {dirPath}");
            return;
        }
        var dbfFiles = Directory.GetFiles(dirPath, "*.DBF");
        Log($"พบ .DBF files {dbfFiles.Length} ไฟล์:");
        foreach (var f in dbfFiles.OrderBy(x => x))
        {
            Log($"  - {Path.GetFileName(f)} ({new FileInfo(f).Length / 1024} KB)");
        }
    }

    private void LogDbfStructure(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var br = new BinaryReader(fs);

            fs.Seek(0, SeekOrigin.Begin);
            br.ReadByte(); // version
            int recordCount = br.ReadInt32();
            short headerSize = br.ReadInt16();
            short recordSize = br.ReadInt16();

            Log($"  DBF info: records={recordCount}, header={headerSize}, record_size={recordSize}");
        }
        catch (Exception ex)
        {
            Log($"  ⚠ Cannot read DBF header: {ex.Message}");
        }
    }
}
