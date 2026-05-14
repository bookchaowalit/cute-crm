namespace ExpressETL;

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
        var etl = new EtlService(config);

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton(config);
                services.AddSingleton(etl);
                services.AddHostedService<EtlWindowsService>();
            })
            .UseWindowsService(options =>
            {
                options.ServiceName = "ExpressETL";
            })
            .Build();

        host.Run();
    }
}
