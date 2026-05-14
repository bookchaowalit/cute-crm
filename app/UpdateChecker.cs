using System.Text.Json;

namespace AccountingETL.App;

/// <summary>
/// ตรวจสอบ version ใหม่จาก GitHub Releases
/// </summary>
public static class UpdateChecker
{
    private static readonly string _currentVersion =
        System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "1.0.0";

    private static readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var response = await _client.GetStringAsync(
                "https://api.github.com/repos/bookchaowalit/cute-crm/releases/latest");

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            string tagName = root.GetProperty("tag_name").GetString() ?? "";
            string releaseUrl = root.GetProperty("html_url").GetString() ?? "";
            string body = root.GetProperty("body").GetString() ?? "";

            // Parse version number from tag (e.g., "v1.5.0" → "1.5.0")
            string latestVersion = tagName.TrimStart('v');

            if (IsNewerVersion(latestVersion, _currentVersion))
            {
                return new UpdateInfo
                {
                    CurrentVersion = _currentVersion,
                    LatestVersion = latestVersion,
                    ReleaseUrl = releaseUrl,
                    ReleaseNotes = body
                };
            }

            return null; // Up to date
        }
        catch
        {
            // Network error or API issue — fail silently
            return null;
        }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        if (Version.TryParse(latest, out var v1) && Version.TryParse(current, out var v2))
            return v1 > v2;
        return latest != current;
    }

    public class UpdateInfo
    {
        public string CurrentVersion { get; init; } = "";
        public string LatestVersion { get; init; } = "";
        public string ReleaseUrl { get; init; } = "";
        public string ReleaseNotes { get; init; } = "";
    }
}
