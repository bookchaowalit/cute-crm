using System.Text.Json;

namespace ExpressETL;

/// <summary>
/// ส่งข้อความแจ้งเตือนผ่าน LINE Notify API
/// </summary>
public static class LineNotify
{
    private static readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>
    /// ส่งข้อความไปยัง LINE Notify (ไม่ throw exception)
    /// </summary>
    public static async Task<bool> SendAsync(string token, string message)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://notify-api.line.me/api/notify")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["message"] = message
                })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            // LINE Notify returns {"status":200,"message":"ok"} on success
            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetInt32() : -1;
            return status == 200;
        }
        catch
        {
            // Fail silently — notification is non-critical
            return false;
        }
    }

    /// <summary>
    /// ส่งข้อความสำเร็จ
    /// </summary>
    public static Task<bool> SendSuccessAsync(string token, string summary, long durationMs)
    {
        var msg = $"✅ ETL สำเร็จ\n{summary}\n️ ใช้เวลา {durationMs / 1000.0:F1} วินาที";
        return SendAsync(token, msg);
    }

    /// <summary>
    /// ส่งข้อความผิดพลาด
    /// </summary>
    public static Task<bool> SendErrorAsync(string token, string errorMessage)
    {
        var msg = $"❌ ETL ล้มเหลว!\n\n{errorMessage}";
        return SendAsync(token, msg);
    }
}
