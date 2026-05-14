using System.Text.Json;

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

    private static string ConfigFilePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    public static AppConfig Load()
    {
        if (File.Exists(ConfigFilePath))
        {
            var json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        return new AppConfig();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFilePath, json);
    }

    public string ConnectionString =>
        $"Host={PgHost};Port={PgPort};Database={PgDb};Username={PgUser};Password={PgPass};CommandTimeout=120";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PgHost) && !string.IsNullOrWhiteSpace(PgDb);
}
