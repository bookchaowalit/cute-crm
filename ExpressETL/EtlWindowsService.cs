using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ExpressETL;

/// <summary>
/// Windows Service wrapper — รัน ETL เป็น Windows Service แท้จริง
/// ใช้: ExpressETL.exe /service install   → ติดตั้ง service
///      ExpressETL.exe /service uninstall → ลบ service
///      ExpressETL.exe /service           → รันเป็น service
/// </summary>
public class EtlWindowsService : BackgroundService
{
    private readonly EtlService _etl;
    private readonly AppConfig _config;
    private readonly System.Timers.Timer _timer;

    public EtlWindowsService(EtlService etl, AppConfig config)
    {
        _etl = etl;
        _config = config;
        _timer = new System.Timers.Timer(config.IntervalHours * 60 * 60 * 1000);
        _timer.Elapsed += async (s, e) => await RunEtlAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once on startup
        await RunEtlAsync();

        // Then run on schedule
        _timer.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task RunEtlAsync()
    {
        try
        {
            await _etl.RunAsync();
        }
        catch (Exception ex)
        {
            // Log to Windows Event Log
            System.Diagnostics.EventLog.WriteEntry("ExpressETL",
                $"ETL Error: {ex.Message}\n{ex.StackTrace}",
                System.Diagnostics.EventLogEntryType.Error);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Stop();
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Service installer / uninstaller utility
/// </summary>
public static class ServiceManager
{
    public const string ServiceName = "ExpressETL";

    public static bool IsInstalled()
    {
        return System.ServiceProcess.ServiceController.GetServices()
            .Any(s => s.ServiceName == ServiceName);
    }

    public static void Install(string exePath)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"create \"{ServiceName}\" binPath= \"{exePath} /service\" start= auto",
            Verb = "runas",
            UseShellExecute = true
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        proc?.WaitForExit();

        psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"description \"{ServiceName}\" \"Express Accounting to PostgreSQL ETL Service\"",
            Verb = "runas",
            UseShellExecute = true
        };
        using var proc2 = System.Diagnostics.Process.Start(psi);
        proc2?.WaitForExit();
    }

    public static void Uninstall()
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"stop \"{ServiceName}\"",
            Verb = "runas",
            UseShellExecute = true
        };
        using var proc1 = System.Diagnostics.Process.Start(psi);
        proc1?.WaitForExit();

        psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"delete \"{ServiceName}\"",
            Verb = "runas",
            UseShellExecute = true
        };
        using var proc2 = System.Diagnostics.Process.Start(psi);
        proc2?.WaitForExit();
    }

    public static void Start()
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"start \"{ServiceName}\"",
            Verb = "runas",
            UseShellExecute = true
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        proc?.WaitForExit();
    }
}
