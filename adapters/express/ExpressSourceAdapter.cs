using System.Text;
using AccountingETL.Core.Domain;
using AccountingETL.Core.Exceptions;
using AccountingETL.Core.Ports;

namespace AccountingETL.Adapters.Express;

/// <summary>
/// Source adapter for Express Accounting — reads DBF files.
/// 
/// Supports two modes:
/// 1. **Canonical mode** — maps known DBF files to EntityType (Customer, Supplier, etc.)
/// 2. **Auto-discovery mode** — scans the folder for all .DBF files and discovers tables dynamically
/// </summary>
public class ExpressSourceAdapter : ISourceAdapter
{
    private readonly string _dbfPath;
    private readonly string _encodingName;

    public string SourceName => "Express Accounting";

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

    // Express DBF file name patterns (canonical mode)
    private static readonly Dictionary<EntityType, (string primary, string[] alternatives)> _filePatterns = new()
    {
        [EntityType.Customer] = ("ARMAS.DBF", new[] { "AR_MAS.DBF", "CUSTMAS.DBF", "CUSTOMER.DBF" }),
        [EntityType.Supplier] = ("APMAS.DBF", new[] { "AP_MAS.DBF", "VENDMAS.DBF", "SUPPLIER.DBF" }),
        [EntityType.Item] = ("ICMAS.DBF", new[] { "IC_MAS.DBF", "ITEMMAS.DBF", "ITEMS.DBF" }),
        [EntityType.ArInvoice] = ("ARTRN.DBF", new[] { "AR_TRN.DBF", "ARINVH.DBF", "AR_INVH.DBF" }),
        [EntityType.ApInvoice] = ("APTRN.DBF", new[] { "AP_TRN.DBF", "APINVH.DBF", "AP_INVH.DBF" }),
        [EntityType.GlTransaction] = ("GLTRANS.DBF", new[] { "GL_TRAN.DBF", "GL_TRN.DBF", "GLTRAN.DBF", "GL.DBF" }),
    };

    private static readonly Dictionary<EntityType, string> _detailFilePatterns = new()
    {
        [EntityType.ArInvoice] = "ARTRND.DBF",
        [EntityType.ApInvoice] = "APTRND.DBF",
    };

    private static readonly Dictionary<EntityType, string[]> _detailFileAlternatives = new()
    {
        [EntityType.ArInvoice] = new[] { "AR_TRND.DBF", "ARINVD.DBF", "AR_INVD.DBF" },
        [EntityType.ApInvoice] = new[] { "AP_TRND.DBF", "APINVD.DBF", "AP_INVD.DBF" },
    };

    // Known Express table descriptions for auto-discovery display names
    private static readonly Dictionary<string, string> _knownTableDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ARMAS"] = "AR Master (Customers)",
        ["APMAS"] = "AP Master (Suppliers)",
        ["ICMAS"] = "Item Master (Products)",
        ["ARTRN"] = "AR Transactions (Sales Invoices)",
        ["ARTRND"] = "AR Transaction Details",
        ["APTRN"] = "AP Transactions (Purchase Invoices)",
        ["APTRND"] = "AP Transaction Details",
        ["GLTRANS"] = "GL Transactions",
        ["BKMAs"] = "Bank Master",
        ["BKTRN"] = "Bank Transactions",
        ["ISTAB"] = "Item Stock Table",
        ["ISTAX"] = "Item Tax Table",
        ["ISVAT"] = "VAT Table",
        ["OESO"] = "Opening Stock",
    };

    public ExpressSourceAdapter(string dbfPath, string encodingName = "tis-620")
    {
        _dbfPath = dbfPath;
        _encodingName = encodingName;
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Directory.Exists(_dbfPath));
    }

    // ===== Canonical Mode =====

    public async IAsyncEnumerable<Record> ReadAsync(EntityType entity, DateTimeOffset? cutoff, CancellationToken ct = default)
    {
        var dbfReader = new DbfReader(_encodingName);

        if (entity == EntityType.ArInvoiceLine)
        {
            var detailPath = FindDbfFile(EntityType.ArInvoice, isDetail: true);
            if (detailPath == null) yield break;
            foreach (var r in dbfReader.Read(detailPath))
            {
                ct.ThrowIfCancellationRequested();
                yield return r;
            }
            yield break;
        }

        if (entity == EntityType.ApInvoiceLine)
        {
            var detailPath = FindDbfFile(EntityType.ApInvoice, isDetail: true);
            if (detailPath == null) yield break;
            foreach (var r in dbfReader.Read(detailPath))
            {
                ct.ThrowIfCancellationRequested();
                yield return r;
            }
            yield break;
        }

        var filePath = FindDbfFile(entity);
        if (filePath == null) yield break;

        foreach (var r in dbfReader.Read(filePath))
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
        }
    }

    // ===== Auto-Discovery Mode =====

    public async Task<IReadOnlyList<SourceTableInfo>> DiscoverTablesAsync(CancellationToken ct = default)
    {
        var tables = new List<SourceTableInfo>();
        var dbfReader = new DbfReader(_encodingName);

        if (!Directory.Exists(_dbfPath))
            return tables;

        var dbfFiles = Directory.GetFiles(_dbfPath, "*.DBF", SearchOption.TopDirectoryOnly);

        foreach (var filePath in dbfFiles.OrderBy(f => f))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var info = dbfReader.GetTableInfo(filePath);
                if (info == null) continue;

                var tableName = Path.GetFileNameWithoutExtension(filePath).ToUpperInvariant();
                var displayName = _knownTableDescriptions.TryGetValue(tableName, out var desc)
                    ? $"{tableName} — {desc}"
                    : tableName;

                tables.Add(new SourceTableInfo(
                    TableName: tableName,
                    DisplayName: displayName,
                    Fields: info.Fields,
                    RecordCount: info.RecordCount,
                    SourcePath: filePath));
            }
            catch
            {
                // Skip files that can't be parsed
            }
        }

        return tables;
    }

    public async IAsyncEnumerable<Record> ReadTableAsync(string tableName, CancellationToken ct = default)
    {
        var dbfReader = new DbfReader(_encodingName);

        // Try exact match first, then case-insensitive
        var filePath = Path.Combine(_dbfPath, $"{tableName}.DBF");
        if (!File.Exists(filePath))
        {
            var files = Directory.GetFiles(_dbfPath, "*.DBF");
            filePath = files.FirstOrDefault(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f), tableName, StringComparison.OrdinalIgnoreCase));
            if (filePath == null) yield break;
        }

        var records = dbfReader.Read(filePath);
        foreach (var r in records)
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
        }
    }

    // ===== Helpers =====

    private string? FindDbfFile(EntityType entity, bool isDetail = false)
    {
        if (isDetail)
        {
            if (!_detailFilePatterns.TryGetValue(entity, out var primary)) return null;
            var alternatives = _detailFileAlternatives.TryGetValue(entity, out var alts) ? alts : Array.Empty<string>();
            return FindFile(primary, alternatives);
        }

        if (!_filePatterns.TryGetValue(entity, out var pattern)) return null;
        return FindFile(pattern.primary, pattern.alternatives);
    }

    private string? FindFile(string primary, string[] alternatives)
    {
        var p = Path.Combine(_dbfPath, primary);
        if (File.Exists(p)) return p;

        foreach (var a in alternatives)
        {
            var ap = Path.Combine(_dbfPath, a);
            if (File.Exists(ap)) return ap;
        }
        return null;
    }
}
