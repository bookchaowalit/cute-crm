using System.Text.Json;
using System.Security.Cryptography;

namespace ExpressETL;

public class AppConfig
{
    public string DbfPath { get; set; } = @"C:\Program Files (x86)\ExpressD\dat";
    public string PgHost { get; set; } = "";
    public int PgPort { get; set; } = 5432;
    public string PgDb { get; set; } = "";
    public string PgUser { get; set; } = "";
    public string PgPass { get; set; } = "";
    public int IntervalHours { get; set; } = 1;
    public DateTime LastSyncTime { get; set; } = DateTime.MinValue;

    // LINE Notify
    public string LineToken { get; set; } = "";
    public bool NotifyOnSuccess { get; set; } = false;
    public bool NotifyOnFailure { get; set; } = true;

    // Background mode
    public bool MinimizeToTray { get; set; } = false;
    public bool AutoStart { get; set; } = false;

    private static string ConfigFilePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    public static AppConfig Load()
    {
        if (File.Exists(ConfigFilePath))
        {
            var content = File.ReadAllText(ConfigFilePath);

            // Try to decrypt first (if encrypted)
            if (ConfigEncryption.IsEncrypted(content))
            {
                var decrypted = ConfigEncryption.Decrypt(content);
                if (decrypted != null) return decrypted;
            }

            // Fall back to plain JSON (backwards compatibility)
            return JsonSerializer.Deserialize<AppConfig>(content) ?? new AppConfig();
        }
        return new AppConfig();
    }

    public void Save()
    {
        // Always save encrypted
        var encrypted = ConfigEncryption.Encrypt(this);
        File.WriteAllText(ConfigFilePath, encrypted);
    }

    /// <summary>
    /// Save as plain JSON (for debugging or manual edit)
    /// </summary>
    public void SavePlain()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFilePath, json);
    }

    public string ConnectionString =>
        $"Host={PgHost};Port={PgPort};Database={PgDb};Username={PgUser};Password={PgPass};CommandTimeout=120";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PgHost) && !string.IsNullOrWhiteSpace(PgDb);
}
