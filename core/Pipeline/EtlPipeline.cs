using AccountingETL.Core.Domain;
using AccountingETL.Core.Ports;

namespace AccountingETL.Core.Pipeline;

/// <summary>
/// Default ETL pipeline implementation — coordinates source → transform → target.
/// This is the orchestrator that lives in Core and depends only on interfaces.
/// </summary>
public class EtlPipeline : IEtlPipeline
{
    private readonly ISourceAdapter _source;
    private readonly ITargetAdapter _target;
    private readonly IFieldMapper? _mapper;
    private readonly ITargetAdapter? _secondaryTarget; // e.g., ERPNext
    private readonly EtlConfig _config;

    public event Action<string>? Log;

    public IReadOnlyList<EntityType> SyncedEntities => _source.SupportedEntities
        .Intersect(_target.SupportedEntities)
        .ToList();

    public EtlPipeline(
        ISourceAdapter source,
        ITargetAdapter target,
        EtlConfig config,
        IFieldMapper? mapper = null,
        ITargetAdapter? secondaryTarget = null)
    {
        _source = source;
        _target = target;
        _config = config;
        _mapper = mapper;
        _secondaryTarget = secondaryTarget;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        LogMessage($"Initializing {_target.TargetName}...");
        await _target.EnsureSchemaAsync(ct);
        LogMessage($"{_target.TargetName} ready.");
    }

    public async Task<IReadOnlyList<SyncResult>> RunAsync(
        EntityType? entity = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var syncStartTime = DateTimeOffset.Now;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        LogMessage("====== ETL เริ่มทำงาน ======");

        var entitiesToSync = entity.HasValue
            ? new[] { entity.Value }
            : SyncedEntities;

        var results = new List<SyncResult>();

        try
        {
            foreach (var entityType in entitiesToSync)
            {
                ct.ThrowIfCancellationRequested();

                var result = await SyncEntityAsync(entityType, syncStartTime, progress, ct);
                results.Add(result);

                // Also sync to secondary target if configured (e.g., ERPNext for master data)
                if (_secondaryTarget != null && IsMasterData(entityType))
                {
                    await SyncToSecondaryTargetAsync(entityType, ct);
                }
            }

            // Update config
            _config.LastSyncTime = syncStartTime;

            sw.Stop();
            LogMessage("====== ETL เสร็จสิ้น ======");

            // Send LINE Notify on success
            await NotifySuccessAsync(results, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogMessage($"✗ ETL ผิดพลาด: {ex.Message}");

            await NotifyFailureAsync(ex.Message);

            // Add failed result
            if (entity.HasValue && !results.Any(r => r.Entity == entity.Value))
            {
                results.Add(new SyncResult(
                    entity.Value, SyncCounts.Zero, sw.Elapsed, "failed", ex.Message));
            }
        }

        return results;
    }

    private async Task<SyncResult> SyncEntityAsync(
        EntityType entity,
        DateTimeOffset syncStartTime,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var entityName = entity.ToString();

        try
        {
            // Read from source
            LogMessage($"Reading {entityName} from {_source.SourceName}...");
            var records = new List<Record>();
            await foreach (var record in _source.ReadAsync(entity, _config.LastSyncTime, ct))
            {
                var mapped = _mapper?.Map(entity, record) ?? record;
                records.Add(mapped);
            }

            if (records.Count == 0)
            {
                LogMessage($"  {entityName}: ไม่มีข้อมูล — ข้าม");
                sw.Stop();
                return new SyncResult(entity, SyncCounts.Zero, sw.Elapsed, "success");
            }

            LogMessage($"  {entityName}: อ่านได้ {records.Count} รายการ");

            // Write to target
            var keyField = EntitySchema.GetKeyField(entity);
            var counts = await _target.UpsertAsync(entity, records, keyField, ct);

            sw.Stop();

            // Log to target
            var status = counts.Inserted > 0 || counts.Updated > 0 ? "success" : "skipped";
            var syncLog = new SyncLog(
                SyncTime: syncStartTime,
                Entity: entity,
                Inserted: counts.Inserted,
                Updated: counts.Updated,
                Skipped: counts.Skipped,
                Duration: sw.Elapsed,
                Status: status);

            await _target.WriteSyncLogAsync(syncLog, ct);

            var msg = $"✓ {entityName}: +{counts.Inserted} ~{counts.Updated} ⏱{sw.ElapsedMilliseconds}ms";
            LogMessage(msg);
            progress?.Report(msg);

            return new SyncResult(entity, counts, sw.Elapsed, status);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var msg = $"✗ {entityName}: {ex.Message}";
            LogMessage(msg);

            return new SyncResult(entity, SyncCounts.Zero, sw.Elapsed, "failed", ex.Message);
        }
    }

    private async Task SyncToSecondaryTargetAsync(EntityType entity, CancellationToken ct)
    {
        if (_secondaryTarget == null) return;

        try
        {
            LogMessage($"Syncing {entity} to {_secondaryTarget.TargetName}...");
            var keyField = EntitySchema.GetKeyField(entity);

            // Read from primary target to get canonical records
            // For ERPNext, this is typically done by reading from the staging DB
            // Since we don't have a direct way to read from the target, we re-read from source
            var records = new List<Record>();
            await foreach (var record in _source.ReadAsync(entity, null, ct))
            {
                var mapped = _mapper?.Map(entity, record) ?? record;
                records.Add(mapped);
            }

            if (records.Count > 0)
            {
                var counts = await _secondaryTarget.UpsertAsync(entity, records, keyField, ct);
                LogMessage($"  {_secondaryTarget.TargetName}: {entity} → +{counts.Inserted} ~{counts.Updated} ⏭{counts.Skipped}");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"  ⚠ {_secondaryTarget.TargetName} sync failed for {entity}: {ex.Message}");
        }
    }

    private static bool IsMasterData(EntityType entity) => entity switch
    {
        EntityType.Customer or EntityType.Supplier or EntityType.Item => true,
        _ => false
    };

    private void LogMessage(string msg) => Log?.Invoke(msg);

    private async Task NotifySuccessAsync(IReadOnlyList<SyncResult> results, long elapsedMs)
    {
        // This is a notification concern — could be injected as a separate port
        // For now, we just log — the UI layer can handle LINE Notify
        var summary = string.Join(" | ", results.Select(r => $"{r.Entity}: +{r.Counts.Inserted} ~{r.Counts.Updated}"));
        LogMessage($"Summary: {summary} | Total: {elapsedMs}ms");
    }

    private Task NotifyFailureAsync(string errorMessage)
    {
        LogMessage($"Failure notification: {errorMessage}");
        return Task.CompletedTask;
    }
}
