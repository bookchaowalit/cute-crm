namespace ExpressETL;

public partial class MainForm : Form
{
    private AppConfig _config = null!;
    private EtlService _etl = null!;
    private System.Windows.Forms.Timer _schedulerTimer = null!;
    private bool _isRunning = false;

    public MainForm()
    {
        InitializeComponent();
        LoadConfig();
        UpdateStatusDisplay();
        StartScheduler();
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
    private Button btnExit = null!;
    private ProgressBar progressBar = null!;
    private System.Windows.Forms.Timer schedulerTimer = null!;

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
        _etl = new EtlService(_config);
        _etl.OnLog += (s, msg) => AppendLog(msg);
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
            await _etl.RunAsync(progress);
            lblStatus.Text = "✓ ETL เสร็จสิ้น";
            lblStatus.ForeColor = Color.DarkGreen;
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"✗ ผิดพลาด: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            AppendLog($"ERROR: {ex.Message}");
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
                await _etl.RunAsync();
                lblStatus.Text = "✓ Auto-sync เสร็จสิ้น";
                lblStatus.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex)
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

    private void BtnExit_Click(object? sender, EventArgs e)
    {
        Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _schedulerTimer?.Stop();
        base.OnFormClosing(e);
    }
}
