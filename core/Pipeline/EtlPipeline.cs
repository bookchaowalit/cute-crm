using AccountingETL.Core.Domain;
using AccountingETL.Core.Ports;

namespace AccountingETL.Core.Pipeline;

/// <summary>
/// Default ETL pipeline implementation — coordinates source → transform → target.
/// 
/// Supports two modes:
/// 1. **Canonical mode** — uses SupportedEntities + ReadAsync + field mapping (best when schema is known)
/// 2. **Auto-discovery mode** — uses DiscoverTables + ReadTableAsync (best for unknown/legacy data)
/// 
/// The pipeline automatically picks the best mode based on what the adapters support.
/// </summary>
public class EtlPipeline : IEtlPipeline
{
    private readonly ISourceAdapter _source;
    private readonly ITargetAdapter _target;
    private readonly IFieldMapper? _mapper;
    private readonly ITargetAdapter? _secondaryTarget; // e.g., ERPNext
    private readonly EtlConfig _config;

    public event Action<string>? Log;

    /// <summary>
    /// Entities synced in canonical mode (empty if running in auto-discovery mode).
    /// </summary>
    public IReadOnlyList<EntityType> SyncedEntities => _source.SupportedEntities
        .Intersect(_target.SupportedEntities)
        .ToList();

    /// <summary>
    /// Tables synced in auto-discovery mode (empty if running in canonical mode).
    /// </summary>
    public IReadOnlyList<SourceTableInfo> DiscoveredTables { get; private set; }
        = Array.Empty<SourceTableInfo>();

    /// <summary>
    /// True if the pipeline is running in auto-discovery mode.
    /// </summary>
    public bool IsAutoDiscoveryMode => !SyncedEntities.Any()
        && _source.SupportsAutoDiscovery
        && _target.SupportsAutoDiscovery;

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

        if (IsAutoDiscoveryMode)
        {
            // Auto-discovery mode: discover tables first
            LogMessage($"Discovering tables from {_source.SourceName}...");
            DiscoveredTables = await _source.DiscoverTablesAsync(ct);
            LogMessage($"Found {DiscoveredTables.Count} tables: {string.Join(", ", DiscoveredTables.Select(t => t.TableName))}");

            // Create tables in target
            foreach (var table in DiscoveredTables)
            {
                await _target.EnsureTableAsync(table, ct);
                LogMessage($"  Created table: {table.TableName} ({table.Fields.Count} fields, {table.RecordCount} records)");
            }
        }
        else
        {
            // Canonical mode: use predefined schema
            await _target.EnsureSchemaAsync(ct);
        }

        LogMessage($"{_target.TargetName} ready. Mode: {(IsAutoDiscoveryMode ? "Auto-Discovery" : "Canonical")}");
    }

    public async Task<IReadOnlyList<SyncResult>> RunAsync(
        EntityType? entity = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var syncStartTime = DateTimeOffset.Now;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        LogMessage("====== ETL เริ่มทำงาน ======");

        var results = new List<SyncResult>();

        try
        {
            // Mode 1: Try canonical first
            if (!IsAutoDiscoveryMode && !entity.HasValue)
            {
                var canonicalResults = await RunCanonicalAsync(null, syncStartTime, progress, ct);

                // Check if canonical found any data at all
                var totalSynced = canonicalResults.Sum(r => r.Counts.Total);
                if (totalSynced == 0 && _source.SupportsAutoDiscovery && _target.SupportsAutoDiscovery)
                {
                    LogMessage("Canonical mode found 0 records — falling back to Auto-Discovery mode...");

                    // Discover tables and create them
                    try
                    {
                        LogMessage("Step 1/3: Discovering tables...");
                        DiscoveredTables = await _source.DiscoverTablesAsync(ct);
                        LogMessage($"Found {DiscoveredTables.Count} tables via auto-discovery.");

                        LogMessage("Step 2/3: Creating tables in PostgreSQL...");
                        int tablesCreated = 0;
                        foreach (var table in DiscoveredTables)
                        {
                            try
                            {
                                await _target.EnsureTableAsync(table, ct);
                                tablesCreated++;
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"  ⚠ Failed to create table {table.TableName}: {ex.Message}");
                            }
                        }
                        LogMessage($"Created {tablesCreated}/{DiscoveredTables.Count} tables.");

                        LogMessage("Step 3/3: Syncing data...");
                        // Now sync via auto-discovery
                        var autoResults = await RunAutoDiscoveryAsync(syncStartTime, progress, ct);
                        results.AddRange(autoResults);
                    }
                    catch (Exception fallbackEx)
                    {
                        LogMessage($"Auto-discovery failed: {fallbackEx.Message}");
                        LogMessage($"Stack: {fallbackEx.StackTrace?.Split('\n').FirstOrDefault()}");
                        throw;
                    }
                }
                else
                {
                    results.AddRange(canonicalResults);
                }
            }
            else if (IsAutoDiscoveryMode)
            {
                // Already in auto-discovery mode from Init
                var autoResults = await RunAutoDiscoveryAsync(syncStartTime, progress, ct);
                results.AddRange(autoResults);
            }
            else
            {
                // User requested a specific entity → use canonical
                var canonicalResults = await RunCanonicalAsync(entity, syncStartTime, progress, ct);
                results.AddRange(canonicalResults);
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

            if (!results.Any())
            {
                results.Add(new SyncResult(
                    entity ?? EntityType.Customer, SyncCounts.Zero, sw.Elapsed, "failed", ex.Message));
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<SyncResult>> RunCanonicalAsync(
        EntityType? entity,
        DateTimeOffset syncStartTime,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var entitiesToSync = entity.HasValue
            ? new[] { entity.Value }
            : SyncedEntities;

        var results = new List<SyncResult>();

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

        return results;
    }

    private async Task<IReadOnlyList<SyncResult>> RunAutoDiscoveryAsync(
        DateTimeOffset syncStartTime,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var results = new List<SyncResult>();

        foreach (var table in DiscoveredTables)
        {
            ct.ThrowIfCancellationRequested();

            var result = await SyncTableAsync(table, syncStartTime, progress, ct);
            results.Add(result);
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

    private async Task<SyncResult> SyncTableAsync(
        SourceTableInfo table,
        DateTimeOffset syncStartTime,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            LogMessage($"Reading {table.TableName} from {_source.SourceName}...");
            var records = new List<Record>();
            await foreach (var record in _source.ReadTableAsync(table.TableName, ct))
            {
                records.Add(record);
            }

            if (records.Count == 0)
            {
                LogMessage($"  {table.TableName}: ไม่มีข้อมูล — ข้าม");
                sw.Stop();
                return new SyncResult(EntityType.Customer, SyncCounts.Zero, sw.Elapsed, "success");
            }

            LogMessage($"  {table.TableName}: อ่านได้ {records.Count} รายการ");

            var inserted = await _target.InsertAllAsync(table.TableName, records, ct);

            sw.Stop();

            var status = inserted > 0 ? "success" : "skipped";
            var counts = new SyncCounts(inserted, 0, 0);
            var syncLog = new SyncLog(
                SyncTime: syncStartTime,
                Entity: EntityType.Customer,
                Inserted: inserted,
                Updated: 0,
                Skipped: 0,
                Duration: sw.Elapsed,
                Status: status);

            await _target.WriteSyncLogAsync(syncLog, ct);

            var msg = $"✓ {table.TableName}: +{inserted} ⏱{sw.ElapsedMilliseconds}ms";
            LogMessage(msg);
            progress?.Report(msg);

            return new SyncResult(EntityType.Customer, counts, sw.Elapsed, status);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var msg = $"✗ {table.TableName}: {ex.Message}";
            LogMessage(msg);

            return new SyncResult(EntityType.Customer, SyncCounts.Zero, sw.Elapsed, "failed", ex.Message);
        }
    }

    private async Task SyncToSecondaryTargetAsync(EntityType entity, CancellationToken ct)
    {
        if (_secondaryTarget == null) return;

        try
        {
            LogMessage($"Syncing {entity} to {_secondaryTarget.TargetName}...");
            var keyField = EntitySchema.GetKeyField(entity);

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
        string summary;
        if (IsAutoDiscoveryMode || DiscoveredTables.Any())
        {
            var totalInserted = results.Sum(r => r.Counts.Inserted);
            var totalTables = DiscoveredTables.Any() ? DiscoveredTables.Count : results.Count;
            summary = $"{totalTables} tables synced, {totalInserted} total records";
        }
        else
        {
            summary = string.Join(" | ", results.Select(r => $"{r.Entity}: +{r.Counts.Inserted} ~{r.Counts.Updated}"));
        }
        LogMessage($"Summary: {summary} | Total: {elapsedMs}ms");
    }

    private Task NotifyFailureAsync(string errorMessage)
    {
        LogMessage($"Failure notification: {errorMessage}");
        return Task.CompletedTask;
    }
}
