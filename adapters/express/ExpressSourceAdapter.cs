using System.Text;
using AccountingETL.Core.Domain;
using AccountingETL.Core.Exceptions;
using AccountingETL.Core.Ports;

namespace AccountingETL.Adapters.Express;

/// <summary>
/// Source adapter for Express Accounting — reads DBF files and maps them to canonical records.
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

    // Express DBF file name patterns
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

    public ExpressSourceAdapter(string dbfPath, string encodingName = "tis-620")
    {
        _dbfPath = dbfPath;
        _encodingName = encodingName;
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Directory.Exists(_dbfPath));
    }

    public async IAsyncEnumerable<Record> ReadAsync(EntityType entity, DateTimeOffset? cutoff, CancellationToken ct = default)
    {
        // Express doesn't support incremental reads at DBF level — reads all
        var dbfReader = new DbfReader(_encodingName);

        if (entity == EntityType.ArInvoiceLine)
        {
            // Read detail lines for AR invoices
            var detailPath = FindDbfFile(EntityType.ArInvoice, isDetail: true);
            if (detailPath == null) yield break;

            var rawRecords = dbfReader.Read(detailPath);
            foreach (var r in rawRecords)
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

            var rawRecords = dbfReader.Read(detailPath);
            foreach (var r in rawRecords)
            {
                ct.ThrowIfCancellationRequested();
                yield return r;
            }
            yield break;
        }

        var filePath = FindDbfFile(entity);
        if (filePath == null) yield break;

        var records = dbfReader.Read(filePath);
        foreach (var r in records)
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
        }
    }

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
