using AccountingETL.Core.Domain;
using AccountingETL.Core.Pipeline;
using AccountingETL.Core.Ports;
using AccountingETL.Adapters.Express;
using AccountingETL.Adapters.PostgreSQL;
using AccountingETL.Adapters.ErpNext;

namespace AccountingETL.App;

public partial class MainForm : Form
{
    private AppConfig _config = null!;
    private IEtlPipeline _pipeline = null!;
    private ITargetAdapter? _secondaryTarget = null;
    private System.Windows.Forms.Timer _schedulerTimer = null!;
    private bool _isRunning = false;

    public MainForm()
    {
        InitializeComponent();
        LoadConfig();
        UpdateStatusDisplay();
        StartScheduler();
        InitTrayIcon();

        // Check if launched with /minimized flag
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => a.Equals("/minimized", StringComparison.OrdinalIgnoreCase)))
        {
            WindowState = FormWindowState.Minimized;
            Hide();
        }
    }

    private void InitTrayIcon()
    {
        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Express ETL — Running",
            Visible = false
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (s, e) => ShowFromTray());
        menu.Items.Add("Run Now", null, (s, e) => BtnRunNow_Click(null, EventArgs.Empty));
        menu.Items.Add("Settings", null, (s, e) => BtnSettings_Click(null, EventArgs.Empty));
        menu.Items.Add("-");
        menu.Items.Add("Exit", null, (s, e) => ExitApp());
        trayIcon.ContextMenuStrip = menu;

        trayIcon.DoubleClick += (s, e) => ShowFromTray();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        trayIcon.Visible = false;
    }

    private void MinimizeToTray()
    {
        Hide();
        trayIcon.Visible = true;
    }

    private void ExitApp()
    {
        trayIcon.Visible = false;
        _schedulerTimer?.Stop();
        Application.Exit();
    }

    // ===== Designer-generated controls (no .Designer.cs needed) =====
    private Label lblTitle = null!;
    private Label lblStatus = null!;
    private Label lblConfigSummary = null!;
    private TextBox txtLog = null!;
    private Button btnSettings = null!;
    private Button btnRunNow = null!;
    private Button btnViewLog = null!;
    private Button btnHistory = null!;
    private Button btnAbout = null!;
    private Button btnExit = null!;
    private ProgressBar progressBar = null!;
    private NotifyIcon trayIcon = null!;

    private void InitializeComponent()
    {
        this.Text = "Express → PostgreSQL ETL";
        this.Size = new Size(540, 540);
        this.MinimumSize = new Size(540, 540);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(245, 245, 245);

        int y = 15;
        int leftMargin = 20;
        int contentWidth = 460;

        // Title
        lblTitle = new Label
        {
            Text = "Express → PostgreSQL ETL",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Location = new Point(leftMargin, y),
            Size = new Size(contentWidth, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false
        };
        this.Controls.Add(lblTitle);
        y += 40;

        // Status panel
        var statusPanel = new Panel
        {
            Location = new Point(leftMargin, y),
            Size = new Size(contentWidth, 70),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };
        this.Controls.Add(statusPanel);

        lblStatus = new Label
        {
            Text = "พร้อมใช้งาน",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(10, 10),
            Size = new Size(contentWidth - 20, 22),
            AutoSize = false
        };
        statusPanel.Controls.Add(lblStatus);

        progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            Location = new Point(10, 36),
            Size = new Size(contentWidth - 20, 14),
            Visible = false
        };
        statusPanel.Controls.Add(progressBar);

        y += 80;

        // Config summary
        lblConfigSummary = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9),
            Location = new Point(leftMargin, y),
            Size = new Size(contentWidth, 60),
            AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Padding = new Padding(8)
        };
        this.Controls.Add(lblConfigSummary);
        y += 70;

        // Log box
        txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9),
            Location = new Point(leftMargin, y),
            Size = new Size(contentWidth, 180),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.Lime
        };
        this.Controls.Add(txtLog);
        y += 190;

        // Buttons (row 1)
        int btnWidth = 95;
        int btnY = y;
        int btnX = leftMargin;

        btnSettings = new Button
        {
            Text = "⚙ Settings",
            Location = new Point(btnX, btnY),
            Size = new Size(btnWidth, 34),
            Font = new Font("Segoe UI", 9)
        };
        btnSettings.Click += BtnSettings_Click;
        this.Controls.Add(btnSettings);
        btnX += btnWidth + 6;

        btnRunNow = new Button
        {
            Text = "▶ Run Now",
            Location = new Point(btnX, btnY),
            Size = new Size(btnWidth, 34),
            Font = new Font("Segoe UI", 9),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnRunNow.Click += BtnRunNow_Click;
        this.Controls.Add(btnRunNow);
        btnX += btnWidth + 6;

        btnViewLog = new Button
        {
            Text = "📄 Log",
            Location = new Point(btnX, btnY),
            Size = new Size(btnWidth, 34),
            Font = new Font("Segoe UI", 9)
        };
        btnViewLog.Click += BtnViewLog_Click;
        this.Controls.Add(btnViewLog);
        btnX += btnWidth + 6;

        btnHistory = new Button
        {
            Text = "📊 History",
            Location = new Point(btnX, btnY),
            Size = new Size(btnWidth, 34),
            Font = new Font("Segoe UI", 9)
        };
        btnHistory.Click += BtnHistory_Click;
        this.Controls.Add(btnHistory);
        btnX += btnWidth + 6;

        btnAbout = new Button
        {
            Text = "ℹ️ About",
            Location = new Point(btnX, btnY),
            Size = new Size(btnWidth, 34),
            Font = new Font("Segoe UI", 9)
        };
        btnAbout.Click += BtnAbout_Click;
        this.Controls.Add(btnAbout);
        btnX += btnWidth + 6;

        btnExit = new Button
        {
            Text = "✕ Exit",
            Location = new Point(btnX, btnY),
            Size = new Size(btnWidth, 34),
            Font = new Font("Segoe UI", 9)
        };
        btnExit.Click += BtnExit_Click;
        this.Controls.Add(btnExit);
    }

    // ===== Config =====
    private void LoadConfig()
    {
        _config = AppConfig.Load();

        // Build pipeline from config using adapters
        var source = new ExpressSourceAdapter(_config.DbfPath, _config.DbfEncoding);
        var target = new PostgreSqlTargetAdapter(_config.ConnectionString, "express_staging");

        // Build field mapper from config
        var mapper = new ExpressFieldMapper(_config.FieldMapping?.ToDictionary());

        // Optional secondary target (ERPNext)
        ITargetAdapter? secondaryTarget = null;
        if (_config.SyncToErpNext && !string.IsNullOrWhiteSpace(_config.ErpNextUrl))
        {
            secondaryTarget = new ErpNextTargetAdapter(_config.ErpNextUrl, _config.ErpNextApiKey, _config.ErpNextApiSecret);
        }

        var pipelineConfig = new EtlConfig
        {
            SourcePath = _config.DbfPath,
            SourceEncoding = _config.DbfEncoding,
            TargetHost = _config.PgHost,
            TargetPort = _config.PgPort,
            TargetDatabase = _config.PgDb,
            TargetUser = _config.PgUser,
            TargetPassword = _config.PgPass,
            TargetSchema = "express_staging",
            IntervalHours = _config.IntervalHours,
            LastSyncTime = _config.LastSyncTime,
            LineNotifyToken = _config.LineToken,
            NotifyOnSuccess = _config.NotifyOnSuccess,
            NotifyOnFailure = _config.NotifyOnFailure,
        };

        _pipeline = new EtlPipeline(source, target, pipelineConfig, mapper, secondaryTarget);
        _pipeline.Log += (msg) => AppendLog(msg);

        _secondaryTarget = secondaryTarget;
    }

    private void UpdateStatusDisplay()
    {
        if (_config.IsConfigured)
        {
            lblConfigSummary.Text =
                $"📁 DBF: {_config.DbfPath}\n" +
                $"🗄️  PostgreSQL: {_config.PgHost}:{_config.PgPort} / {_config.PgDb}\n" +
                $"️  รันทุก {_config.IntervalHours} ชั่วโมง";
            lblConfigSummary.ForeColor = Color.DarkGreen;
            lblStatus.Text = "✓ ตั้งค่าเรียบร้อย — ETL จะรันอัตโนมัติ";
            lblStatus.ForeColor = Color.DarkGreen;
        }
        else
        {
            lblConfigSummary.Text = "⚠️  ยังไม่ได้ตั้งค่า — กด Settings เพื่อเริ่มต้น";
            lblConfigSummary.ForeColor = Color.DarkOrange;
            lblStatus.Text = "ไม่พร้อมใช้งาน";
            lblStatus.ForeColor = Color.Red;
        }
    }

    // ===== Log =====
    private void AppendLog(string msg)
    {
        if (txtLog.InvokeRequired)
        {
            txtLog.Invoke(() => AppendLog(msg));
            return;
        }

        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        txtLog.AppendText($"[{timestamp}] {msg}\r\n");
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();

        // Also write to file
        try
        {
            var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etl_log.txt");
            File.AppendAllText(logFile, $"[{timestamp}] {msg}\n");
        }
        catch { }
    }

    // ===== ETL Run =====
    private async void BtnRunNow_Click(object? sender, EventArgs e)
    {
        if (_isRunning) return;
        if (!_config.IsConfigured)
        {
            MessageBox.Show("กรุณาตั้งค่า PostgreSQL connection ก่อน", "ยังไม่ได้ตั้งค่า",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isRunning = true;
        btnRunNow.Enabled = false;
        progressBar.Visible = true;
        lblStatus.Text = "กำลังรัน ETL...";
        lblStatus.ForeColor = Color.DarkBlue;

        var progress = new Progress<string>(msg =>
        {
            AppendLog(msg);
        });

        try
        {
            await _pipeline.InitializeAsync();
            var results = await _pipeline.RunAsync(progress: progress);

            // LINE Notify on success
            if (_config.NotifyOnSuccess && !string.IsNullOrWhiteSpace(_config.LineToken))
            {
                var summary = string.Join(" | ", results.Select(r => $"{r.Entity}: +{r.Counts.Inserted} ~{r.Counts.Updated}"));
                await LineNotify.SendSuccessAsync(_config.LineToken, summary, (long)results.Sum(r => r.Duration.TotalMilliseconds));
            }

            // Update config
            _config.LastSyncTime = DateTime.Now;
            _config.Save();

            lblStatus.Text = "✓ ETL เสร็จสิ้น";
            lblStatus.ForeColor = Color.DarkGreen;
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"✗ ผิดพลาด: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            AppendLog($"ERROR: {ex.Message}");

            // LINE Notify on failure
            if (_config.NotifyOnFailure && !string.IsNullOrWhiteSpace(_config.LineToken))
                await LineNotify.SendErrorAsync(_config.LineToken, ex.Message);
        }
        finally
        {
            _isRunning = false;
            btnRunNow.Enabled = true;
            progressBar.Visible = false;
        }
    }

    // ===== Settings =====
    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var dlg = new SettingsForm(_config);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            LoadConfig();
            UpdateStatusDisplay();
            StartScheduler();
        }
    }

    // ===== Scheduler =====
    private void StartScheduler()
    {
        _schedulerTimer?.Stop();
        _schedulerTimer?.Dispose();

        if (!_config.IsConfigured) return;

        int intervalMs = _config.IntervalHours * 60 * 60 * 1000;
        _schedulerTimer = new System.Windows.Forms.Timer { Interval = intervalMs };
        _schedulerTimer.Tick += async (s, e) =>
        {
            AppendLog("⏰ Scheduler trigger — เริ่ม ETL อัตโนมัติ");
            _isRunning = true;
            btnRunNow.Enabled = false;
            progressBar.Visible = true;

            try
            {
                await _pipeline.InitializeAsync();
                await _pipeline.RunAsync();

                _config.LastSyncTime = DateTime.Now;
                _config.Save();

                lblStatus.Text = "✓ Auto-sync เสร็จสิ้น";
                lblStatus.ForeColor = Color.DarkGreen;
            }
            catch (Exception)
            {
                lblStatus.Text = $"✗ Auto-sync ผิดพลาด";
                lblStatus.ForeColor = Color.Red;
            }
            finally
            {
                _isRunning = false;
                btnRunNow.Enabled = true;
                progressBar.Visible = false;
            }
        };
        _schedulerTimer.Start();
        AppendLog($"⏱️  Scheduler: รันทุก {_config.IntervalHours} ชั่วโมง");
    }

    // ===== View Log =====
    private void BtnViewLog_Click(object? sender, EventArgs e)
    {
        var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etl_log.txt");
        if (File.Exists(logFile))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{logFile}\"",
                UseShellExecute = true
            });
        }
        else
        {
            MessageBox.Show("ยังไม่มี log file", "View Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void BtnHistory_Click(object? sender, EventArgs e)
    {
        using var dlg = new SyncHistoryForm(_config);
        dlg.ShowDialog(this);
    }

    private async void BtnAbout_Click(object? sender, EventArgs e)
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "1.0.0";

        var msg = $"Express → PostgreSQL ETL\nVersion {version}\n\n";
        msg += "Checking for updates...";

        using var dlg = new Form
        {
            Text = "About",
            Size = new Size(380, 260),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(245, 245, 245)
        };

        var lbl = new Label
        {
            Text = msg,
            Font = new Font("Segoe UI", 10),
            Location = new Point(20, 20),
            Size = new Size(340, 100),
            AutoSize = false
        };
        dlg.Controls.Add(lbl);

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(150, 180),
            Size = new Size(80, 32)
        };
        dlg.Controls.Add(btnOk);
        dlg.AcceptButton = btnOk;

        dlg.Show(this);
        Application.DoEvents();

        // Check for updates
        var update = await UpdateChecker.CheckAsync();

        if (update != null)
        {
            lbl.Text = $"Express → PostgreSQL ETL\nVersion {version}\n\n" +
                $"🆕 Version {update.LatestVersion} available!\n\n" +
                $"Click below to open GitHub Releases page.";
            lbl.ForeColor = Color.DarkGreen;

            var btnDownload = new Button
            {
                Text = "📥 Open Releases",
                Location = new Point(130, 130),
                Size = new Size(130, 36),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnDownload.Click += (s, e2) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = update.ReleaseUrl,
                    UseShellExecute = true
                });
            };
            dlg.Controls.Add(btnDownload);
        }
        else
        {
            lbl.Text = $"Express → PostgreSQL ETL\nVersion {version}\n\n✅ You have the latest version!";
            lbl.ForeColor = Color.DarkGreen;
        }

        Application.DoEvents();
        dlg.ShowDialog(this);
    }

    private void BtnExit_Click(object? sender, EventArgs e)
    {
        ExitApp();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // Minimize to tray instead of taskbar
        if (_config.MinimizeToTray && WindowState == FormWindowState.Minimized)
        {
            MinimizeToTray();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // If minimize-to-tray is enabled and user clicks X, hide instead of close
        if (_config.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            MinimizeToTray();
            return;
        }

        trayIcon.Visible = false;
        _schedulerTimer?.Stop();
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        trayIcon?.Dispose();
        base.OnFormClosed(e);
    }
}
