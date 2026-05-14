using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using AccountingETL.Core.Domain;
using AccountingETL.Core.Pipeline;
using AccountingETL.Core.Ports;
using AccountingETL.Adapters.Express;
using AccountingETL.Adapters.PostgreSQL;
using AccountingETL.Adapters.ErpNext;

namespace AccountingETL.App;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Handle service mode arguments
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

        if (args.Length > 0)
        {
            switch (args[0].ToLower())
            {
                case "/service":
                    RunAsService();
                    return;
                case "/serviceinstall":
                case "/install":
                    ServiceManager.Install(exePath);
                    ServiceManager.Start();
                    MessageBox.Show($"Service '{ServiceManager.ServiceName}' installed and started.",
                        "Service Installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                case "/serviceuninstall":
                case "/uninstall":
                    ServiceManager.Uninstall();
                    MessageBox.Show($"Service '{ServiceManager.ServiceName}' removed.",
                        "Service Removed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
            }
        }

        // Default: run GUI application
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    static void RunAsService()
    {
        var config = AppConfig.Load();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton(config);

                // Register adapters
                services.AddSingleton<ISourceAdapter>(sp =>
                    new ExpressSourceAdapter(config.DbfPath, config.DbfEncoding));

                services.AddSingleton<ITargetAdapter>(sp =>
                    new PostgreSqlTargetAdapter(config.ConnectionString, "express_staging"));

                services.AddSingleton<IFieldMapper>(sp =>
                    new ExpressFieldMapper(config.FieldMapping?.ToDictionary()));

                // Register pipeline
                services.AddSingleton<IEtlPipeline>(sp =>
                {
                    var source = sp.GetRequiredService<ISourceAdapter>();
                    var target = sp.GetRequiredService<ITargetAdapter>();
                    var mapper = sp.GetService<IFieldMapper>();

                    // Get secondary target if registered
                    ITargetAdapter? secondary = null;
                    if (config.SyncToErpNext && !string.IsNullOrWhiteSpace(config.ErpNextUrl))
                    {
                        secondary = new ErpNextTargetAdapter(config.ErpNextUrl, config.ErpNextApiKey, config.ErpNextApiSecret);
                    }

                    var pipelineConfig = new EtlConfig
                    {
                        SourcePath = config.DbfPath,
                        SourceEncoding = config.DbfEncoding,
                        TargetHost = config.PgHost,
                        TargetPort = config.PgPort,
                        TargetDatabase = config.PgDb,
                        TargetUser = config.PgUser,
                        TargetPassword = config.PgPass,
                        TargetSchema = "express_staging",
                        IntervalHours = config.IntervalHours,
                        LastSyncTime = config.LastSyncTime,
                    };

                    return new EtlPipeline(source, target, pipelineConfig, mapper, secondary);
                });

                services.AddHostedService<EtlWindowsService>();
            })
            .UseWindowsService(options =>
            {
                options.ServiceName = "AccountingETL";
            })
            .Build();

        host.Run();
    }
}
