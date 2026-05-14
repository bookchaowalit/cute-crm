using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using AccountingETL.Core.Domain;
using AccountingETL.Core.Pipeline;
using AccountingETL.Core.Ports;
using AccountingETL.Adapters.Express;
using AccountingETL.Adapters.PostgreSQL;
using AccountingETL.Adapters.ErpNext;

namespace AccountingETL.App;

/// <summary>
/// Windows Service wrapper — runs ETL as a proper Windows Service.
/// Uses the hexagonal architecture pipeline with pluggable adapters.
/// </summary>
public class EtlWindowsService : BackgroundService
{
    private readonly IEtlPipeline _pipeline;
    private readonly AppConfig _config;
    private readonly System.Timers.Timer _timer;

    public EtlWindowsService(IEtlPipeline pipeline, AppConfig config)
    {
        _pipeline = pipeline;
        _config = config;
        _timer = new System.Timers.Timer(config.IntervalHours * 60 * 60 * 1000);
        _timer.Elapsed += async (s, e) => await RunEtlAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize schema
        await _pipeline.InitializeAsync(stoppingToken);

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
            await _pipeline.RunAsync(ct: CancellationToken.None);

            // Update config
            _config.LastSyncTime = DateTime.Now;
            _config.Save();
        }
        catch (Exception ex)
        {
            // Log to Windows Event Log
            System.Diagnostics.EventLog.WriteEntry("AccountingETL",
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
    public const string ServiceName = "AccountingETL";

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
            Arguments = $"description \"{ServiceName}\" \"Accounting ETL Service Service\"",
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
