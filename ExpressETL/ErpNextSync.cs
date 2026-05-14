using System.Text;
using System.Text.Json;

namespace ExpressETL;

/// <summary>
/// Sync ข้อมูลเข้า ERPNext โดยตรงผ่าน REST API
/// </summary>
public class ErpNextSync
{
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly HttpClient _client;

    public ErpNextSync(string baseUrl, string apiKey, string apiSecret)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("token", $"{_apiKey}:{_apiSecret}");
    }

    /// <summary>
    /// สร้างหรืออัปเดต Customer ใน ERPNext
    /// </summary>
    public async Task<bool> UpsertCustomerAsync(string expressCode, string name, string? taxId, string? tel)
    {
        // Check if exists
        var exists = await ResourceExistsAsync("Customer",
            $"[\"custom_express_code\", \"=\", \"{expressCode}\"]");

        var payload = new
        {
            doctype = "Customer",
            customer_name = name,
            customer_group = "All Customer Groups",
            territory = "All Territories",
            tax_id = taxId ?? "",
            mobile_no = tel ?? "",
            custom_express_code = expressCode
        };

        if (exists)
        {
            // Update
            var nameField = await GetCustomerNameAsync(expressCode);
            if (string.IsNullOrEmpty(nameField)) return false;

            var response = await _client.PutAsync(
                $"{_baseUrl}/api/resource/Customer/{Uri.EscapeDataString(nameField)}",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        else
        {
            // Create
            var response = await _client.PostAsync(
                $"{_baseUrl}/api/resource/Customer",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
    }

    /// <summary>
    /// สร้างหรืออัปเดต Supplier ใน ERPNext
    /// </summary>
    public async Task<bool> UpsertSupplierAsync(string expressCode, string name, string? taxId)
    {
        var exists = await ResourceExistsAsync("Supplier",
            $"[\"custom_express_code\", \"=\", \"{expressCode}\"]");

        var payload = new
        {
            doctype = "Supplier",
            supplier_name = name,
            supplier_group = "All Supplier Groups",
            tax_id = taxId ?? "",
            custom_express_code = expressCode
        };

        if (exists)
        {
            var nameField = await GetSupplierNameAsync(expressCode);
            if (string.IsNullOrEmpty(nameField)) return false;

            var response = await _client.PutAsync(
                $"{_baseUrl}/api/resource/Supplier/{Uri.EscapeDataString(nameField)}",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        else
        {
            var response = await _client.PostAsync(
                $"{_baseUrl}/api/resource/Supplier",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
    }

    /// <summary>
    /// สร้างหรืออัปเดต Item ใน ERPNext
    /// </summary>
    public async Task<bool> UpsertItemAsync(string itemCode, string name, decimal? salePrice, string? unit)
    {
        var exists = await ResourceExistsAsync("Item",
            $"[\"item_code\", \"=\", \"{itemCode}\"]");

        var payload = new
        {
            doctype = "Item",
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
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        else
        {
            var response = await _client.PostAsync(
                $"{_baseUrl}/api/resource/Item",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
    }

    private async Task<bool> ResourceExistsAsync(string doctype, string filters)
    {
        var response = await _client.GetAsync(
            $"{_baseUrl}/api/resource/{doctype}?filters={Uri.EscapeDataString(filters)}&limit=1");
        if (!response.IsSuccessStatusCode) return false;

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        return data.GetArrayLength() > 0;
    }

    private async Task<string?> GetCustomerNameAsync(string expressCode)
    {
        var response = await _client.GetAsync(
            $"{_baseUrl}/api/resource/Customer?filters={Uri.EscapeDataString($"[\"custom_express_code\",\"=\",\"{expressCode}\"]")}&limit=1");
        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;
        return data[0].GetProperty("name").GetString();
    }

    private async Task<string?> GetSupplierNameAsync(string expressCode)
    {
        var response = await _client.GetAsync(
            $"{_baseUrl}/api/resource/Supplier?filters={Uri.EscapeDataString($"[\"custom_express_code\",\"=\",\"{expressCode}\"]")}&limit=1");
        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;
        return data[0].GetProperty("name").GetString();
    }
}
