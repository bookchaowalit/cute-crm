namespace ExpressETL;

public partial class SettingsForm : Form
{
    private readonly AppConfig _config;

    private TextBox txtDbfPath = null!;
    private TextBox txtPgHost = null!;
    private TextBox txtPgPort = null!;
    private TextBox txtPgDb = null!;
    private TextBox txtPgUser = null!;
    private TextBox txtPgPass = null!;
    private NumericUpDown numInterval = null!;
    private Label lblTestStatus = null!;
    private Button btnTest = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;

    public SettingsForm(AppConfig config)
    {
        _config = config;
        InitializeComponent();
        LoadConfig();
    }

    private void InitializeComponent()
    {
        this.Text = "ตั้งค่า ETL";
        this.Size = new Size(460, 420);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = Color.FromArgb(245, 245, 245);

        int y = 15;
        int labelWidth = 100;
        int fieldX = 120;
        int fieldWidth = 280;
        int rowHeight = 36;
        int leftMargin = 20;

        void AddField(string label, Control control)
        {
            var lbl = new Label
            {
                Text = label,
                Location = new Point(leftMargin, y + 8),
                Size = new Size(labelWidth, 20),
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false
            };
            this.Controls.Add(lbl);
            control.Location = new Point(fieldX, y);
            control.Size = new Size(fieldWidth, 28);
            this.Controls.Add(control);
            y += rowHeight;
        }

        // Title
        var title = new Label
        {
            Text = "ตั้งค่าการเชื่อมต่อ",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(leftMargin, y),
            Size = new Size(420, 28),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false
        };
        this.Controls.Add(title);
        y += 35;

        // Fields
        txtDbfPath = new TextBox { Text = _config.DbfPath };
        // Wrap in panel with browse button
        var dbfPanel = new Panel { Location = new Point(fieldX, y), Size = new Size(fieldWidth + 40, 28) };
        txtDbfPath.Location = new Point(0, 0);
        txtDbfPath.Size = new Size(fieldWidth - 35, 28);
        txtDbfPath.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        var btnBrowse = new Button { Text = "...", Location = new Point(fieldWidth - 32, 0), Size = new Size(30, 28) };
        btnBrowse.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "เลือกโฟลเดอร์ที่เก็บไฟล์ .DBF",
                SelectedPath = txtDbfPath.Text,
                UseDescriptionForTitle = true
            };
            if (fbd.ShowDialog(this) == DialogResult.OK)
                txtDbfPath.Text = fbd.SelectedPath;
        };
        dbfPanel.Controls.Add(txtDbfPath);
        dbfPanel.Controls.Add(btnBrowse);

        var lblDbf = new Label
        {
            Text = "DBF Path:",
            Location = new Point(leftMargin, y + 8),
            Size = new Size(labelWidth, 20),
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false
        };
        this.Controls.Add(lblDbf);
        this.Controls.Add(dbfPanel);
        y += rowHeight;

        txtPgHost = new TextBox { Text = _config.PgHost };
        AddField("PG Host:", txtPgHost);

        txtPgPort = new TextBox { Text = _config.PgPort.ToString() };
        AddField("PG Port:", txtPgPort);

        txtPgDb = new TextBox { Text = _config.PgDb };
        AddField("PG Database:", txtPgDb);

        txtPgUser = new TextBox { Text = _config.PgUser };
        AddField("PG User:", txtPgUser);

        txtPgPass = new TextBox { Text = _config.PgPass, UseSystemPasswordChar = true };
        AddField("PG Password:", txtPgPass);

        numInterval = new NumericUpDown
        {
            Value = _config.IntervalHours,
            Minimum = 1,
            Maximum = 24
        };
        AddField("Interval (ชม.):", numInterval);

        y += 10;

        // Test connection
        btnTest = new Button
        {
            Text = " Test Connection",
            Location = new Point(leftMargin, y),
            Size = new Size(140, 32),
            Font = new Font("Segoe UI", 9)
        };
        btnTest.Click += BtnTest_Click;
        this.Controls.Add(btnTest);

        lblTestStatus = new Label
        {
            Text = "",
            Location = new Point(fieldX, y + 6),
            Size = new Size(fieldWidth, 22),
            Font = new Font("Segoe UI", 9),
            AutoSize = false
        };
        this.Controls.Add(lblTestStatus);
        y += 42;

        // Buttons
        btnSave = new Button
        {
            Text = "💾 Save & Close",
            Location = new Point(leftMargin, y),
            Size = new Size(130, 36),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            DialogResult = DialogResult.OK
        };
        btnSave.Click += BtnSave_Click;
        this.Controls.Add(btnSave);

        btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(leftMargin + 140, y),
            Size = new Size(100, 36),
            DialogResult = DialogResult.Cancel
        };
        this.Controls.Add(btnCancel);
    }

    private void LoadConfig()
    {
        // Already loaded in constructor
    }

    private async void BtnTest_Click(object? sender, EventArgs e)
    {
        btnTest.Enabled = false;
        btnTest.Text = "กำลังทดสอบ...";
        lblTestStatus.Text = "";

        var tempConfig = GetConfigFromFields();
        var etl = new EtlService(tempConfig);

        bool ok = await etl.TestConnectionAsync();

        if (ok)
        {
            lblTestStatus.Text = "✓ เชื่อมต่อสำเร็จ!";
            lblTestStatus.ForeColor = Color.DarkGreen;
        }
        else
        {
            lblTestStatus.Text = "✗ เชื่อมต่อไม่ได้ — ตรวจสอบ host/user/pass";
            lblTestStatus.ForeColor = Color.Red;
        }

        btnTest.Enabled = true;
        btnTest.Text = "🔌 Test Connection";
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        ApplyConfig();
        _config.Save();
    }

    private void ApplyConfig()
    {
        _config.DbfPath = txtDbfPath.Text.Trim();
        _config.PgHost = txtPgHost.Text.Trim();
        _config.PgPort = int.TryParse(txtPgPort.Text.Trim(), out var p) ? p : 5432;
        _config.PgDb = txtPgDb.Text.Trim();
        _config.PgUser = txtPgUser.Text.Trim();
        _config.PgPass = txtPgPass.Text;
        _config.IntervalHours = (int)numInterval.Value;
    }

    private AppConfig GetConfigFromFields()
    {
        return new AppConfig
        {
            DbfPath = txtDbfPath.Text.Trim(),
            PgHost = txtPgHost.Text.Trim(),
            PgPort = int.TryParse(txtPgPort.Text.Trim(), out var p) ? p : 5432,
            PgDb = txtPgDb.Text.Trim(),
            PgUser = txtPgUser.Text.Trim(),
            PgPass = txtPgPass.Text,
            IntervalHours = (int)numInterval.Value
        };
    }
}
