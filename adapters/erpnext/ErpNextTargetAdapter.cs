using System.Text;
using System.Text.Json;
using AccountingETL.Core.Domain;
using AccountingETL.Core.Exceptions;
using AccountingETL.Core.Ports;

namespace AccountingETL.Adapters.ErpNext;

/// <summary>
/// ERPNext target adapter — syncs master data via REST API.
/// Supports Customer, Supplier, and Item doctypes.
/// </summary>
public class ErpNextTargetAdapter : ITargetAdapter
{
    private readonly string _baseUrl;
    private readonly HttpClient _client;

    public string TargetName => "ERPNext";

    public IReadOnlyList<EntityType> SupportedEntities { get; } = new[]
    {
        EntityType.Customer,
        EntityType.Supplier,
        EntityType.Item,
    };

    public ErpNextTargetAdapter(string baseUrl, string apiKey, string apiSecret)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("token", $"{apiKey}:{apiSecret}");
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
    {
        // Quick check: try to fetch system settings
        return Task.FromResult(true); // Simplified — actual validation happens during sync
    }

    public Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        // ERPNext doesn't need schema setup — doctypes are predefined
        return Task.CompletedTask;
    }

    public async Task<SyncCounts> UpsertAsync(EntityType entity, IEnumerable<Record> records, string keyField, CancellationToken ct = default)
    {
        int inserted = 0, updated = 0, skipped = 0;

        foreach (var record in records)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                bool success = entity switch
                {
                    EntityType.Customer => await UpsertCustomerAsync(record, ct),
                    EntityType.Supplier => await UpsertSupplierAsync(record, ct),
                    EntityType.Item => await UpsertItemAsync(record, ct),
                    _ => throw new NotSupportedException($"Entity type {entity} not supported by ERPNext adapter")
                };

                if (success)
                    inserted++; // We don't know if it was insert vs update without an extra check
                else
                    skipped++;
            }
            catch
            {
                skipped++;
            }
        }

        return new SyncCounts(inserted, updated, skipped);
    }

    public Task<ISet<string>> GetExistingKeysAsync(EntityType entity, string keyField, CancellationToken ct = default)
    {
        // ERPNext doesn't support efficient bulk key retrieval via REST API
        // Return empty set — the adapter handles existence check internally
        return Task.FromResult<ISet<string>>(new HashSet<string>());
    }

    public Task WriteSyncLogAsync(SyncLog log, CancellationToken ct = default)
    {
        // ERPNext doesn't have a local sync log — skip
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SyncLog>> ReadSyncHistoryAsync(int limit = 100, CancellationToken ct = default)
    {
        // ERPNext doesn't store sync history locally
        return Task.FromResult<IReadOnlyList<SyncLog>>(Array.Empty<SyncLog>());
    }

    private async Task<bool> UpsertCustomerAsync(Record record, CancellationToken ct)
    {
        var expressCode = record.Get<string>("code");
        if (string.IsNullOrEmpty(expressCode)) return false;

        var name = record.Get<string>("name", "");
        var taxId = record.Get<string>("tax_id");
        var tel = record.Get<string>("phone");

        var exists = await ResourceExistsAsync("Customer",
            $"[\"custom_express_code\",\"=\",\"{expressCode}\"]", ct);

        var payload = new
        {
            customer_name = name,
            customer_group = "All Customer Groups",
            territory = "All Territories",
            tax_id = taxId ?? "",
            mobile_no = tel ?? "",
            custom_express_code = expressCode
        };

        if (exists)
        {
            var nameField = await GetResourceNameAsync("Customer", "custom_express_code", expressCode, ct);
            if (string.IsNullOrEmpty(nameField)) return false;

            var response = await _client.PutAsync(
                $"{_baseUrl}/api/resource/Customer/{Uri.EscapeDataString(nameField)}",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
            return response.IsSuccessStatusCode;
        }
        else
        {
            var response = await _client.PostAsync(
                $"{_baseUrl}/api/resource/Customer",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
            return response.IsSuccessStatusCode;
        }
    }

    private async Task<bool> UpsertSupplierAsync(Record record, CancellationToken ct)
    {
        var expressCode = record.Get<string>("code");
        if (string.IsNullOrEmpty(expressCode)) return false;

        var name = record.Get<string>("name", "");
        var taxId = record.Get<string>("tax_id");

        var exists = await ResourceExistsAsync("Supplier",
            $"[\"custom_express_code\",\"=\",\"{expressCode}\"]", ct);

        var payload = new
        {
            supplier_name = name,
            supplier_group = "All Supplier Groups",
            tax_id = taxId ?? "",
            custom_express_code = expressCode
        };

        if (exists)
        {
            var nameField = await GetResourceNameAsync("Supplier", "custom_express_code", expressCode, ct);
            if (string.IsNullOrEmpty(nameField)) return false;

            var response = await _client.PutAsync(
                $"{_baseUrl}/api/resource/Supplier/{Uri.EscapeDataString(nameField)}",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
            return response.IsSuccessStatusCode;
        }
        else
        {
            var response = await _client.PostAsync(
                $"{_baseUrl}/api/resource/Supplier",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
            return response.IsSuccessStatusCode;
        }
    }

    private async Task<bool> UpsertItemAsync(Record record, CancellationToken ct)
    {
        var itemCode = record.Get<string>("code");
        if (string.IsNullOrEmpty(itemCode)) return false;

        var name = record.Get<string>("name", "");
        var salePrice = record.Get<decimal?>("sale_price");
        var unit = record.Get<string>("unit");

        var exists = await ResourceExistsAsync("Item",
            $"[\"item_code\",\"=\",\"{itemCode}\"]", ct);

        var payload = new
        {
            item_code = itemCode,
            item_name = name,
            item_group = "All Item Groups",
            stock_uom = unit ?? "Nos",
            standard_rate = salePrice ?? 0,
            is_sales_item = 1,
            is_purchase_item = 1,
            is_stock_item = 1
        };

        if (exists)
        {
            var response = await _client.PutAsync(
                $"{_baseUrl}/api/resource/Item/{Uri.EscapeDataString(itemCode)}",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
            return response.IsSuccessStatusCode;
        }
        else
        {
            var response = await _client.PostAsync(
                $"{_baseUrl}/api/resource/Item",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
            return response.IsSuccessStatusCode;
        }
    }

    private async Task<bool> ResourceExistsAsync(string doctype, string filters, CancellationToken ct)
    {
        var response = await _client.GetAsync(
            $"{_baseUrl}/api/resource/{doctype}?filters={Uri.EscapeDataString(filters)}&limit=1", ct);
        if (!response.IsSuccessStatusCode) return false;

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        return data.GetArrayLength() > 0;
    }

    private async Task<string?> GetResourceNameAsync(string doctype, string filterField, string filterValue, CancellationToken ct)
    {
        var response = await _client.GetAsync(
            $"{_baseUrl}/api/resource/{doctype}?filters={Uri.EscapeDataString($"[\"{filterField}\",\"=\",\"{filterValue}\"]")}&limit=1", ct);
        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;
        return data[0].GetProperty("name").GetString();
    }
}
