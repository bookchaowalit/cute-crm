namespace AccountingETL.App;

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

    // LINE Notify fields
    private TextBox txtLineToken = null!;
    private CheckBox chkNotifySuccess = null!;
    private CheckBox chkNotifyFailure = null!;

    // Background mode fields
    private CheckBox chkMinimizeToTray = null!;
    private CheckBox chkAutoStart = null!;

    // Encoding
    private ComboBox cmbEncoding = null!;

    // ERPNext fields
    private CheckBox chkSyncToErpNext = null!;
    private TextBox txtErpUrl = null!;
    private TextBox txtErpApiKey = null!;
    private TextBox txtErpApiSecret = null!;

    // Field Mapping textboxes (8 fields, 4 rows of 2)
    private TextBox[] txtFieldMappings = Array.Empty<TextBox>();

    public SettingsForm(AppConfig config)
    {
        _config = config;
        InitializeComponent();
        LoadConfig();
    }

    private void InitializeComponent()
    {
        this.Text = "ตั้งค่า ETL";
        this.Size = new Size(560, 620);
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

        // DBF Encoding dropdown
        cmbEncoding = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(fieldX, y),
            Size = new Size(200, 28)
        };
        var encodings = new[]
        {
            ("tis-620", "Thai (tis-620)"),
            ("cp874", "Thai (cp874)"),
            ("windows-874", "Thai (windows-874)"),
            ("utf-8", "UTF-8"),
            ("iso-8859-1", "Western (ISO-8859-1)"),
            ("windows-1252", "Western (windows-1252)"),
            ("shift_jis", "Japanese (Shift-JIS)"),
            ("gb2312", "Chinese (GB2312)"),
        };
        foreach (var (code, name) in encodings)
        {
            cmbEncoding.Items.Add(new { Code = code, Name = name });
        }
        cmbEncoding.DisplayMember = "Name";
        cmbEncoding.ValueMember = "Code";
        cmbEncoding.SelectedValue = _config.DbfEncoding;
        AddField("DBF Encoding:", cmbEncoding);
        y += 0; // AddField already increments y

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

        // LINE Notify section
        y += 8;
        var lineLabel = new Label
        {
            Text = " LINE Notify",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(leftMargin, y),
            Size = new Size(420, 24),
            AutoSize = false
        };
        this.Controls.Add(lineLabel);
        y += 26;

        var lblLineInfo = new Label
        {
            Text = "รับ LINE Token: https://notify-bot.line.me/my/",
            Font = new Font("Segoe UI", 8),
            ForeColor = Color.Gray,
            Location = new Point(fieldX, y),
            Size = new Size(fieldWidth + 40, 16),
            AutoSize = false
        };
        this.Controls.Add(lblLineInfo);
        y += 18;

        txtLineToken = new TextBox { Text = _config.LineToken, UseSystemPasswordChar = true };
        AddField("LINE Token:", txtLineToken);

        chkNotifySuccess = new CheckBox
        {
            Text = "แจ้งเมื่อสำเร็จ (ทุก sync)",
            Location = new Point(fieldX, y),
            Size = new Size(fieldWidth, 22),
            Checked = _config.NotifyOnSuccess,
            AutoSize = false
        };
        this.Controls.Add(chkNotifySuccess);
        y += 24;

        chkNotifyFailure = new CheckBox
        {
            Text = "แจ้งเมื่อผิดพลาด (แนะนำ)",
            Location = new Point(fieldX, y),
            Size = new Size(fieldWidth, 22),
            Checked = _config.NotifyOnFailure,
            AutoSize = false
        };
        this.Controls.Add(chkNotifyFailure);
        y += 24;

        // Background mode section
        var bgLabel = new Label
        {
            Text = " Background Mode",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(leftMargin, y),
            Size = new Size(420, 24),
            AutoSize = false
        };
        this.Controls.Add(bgLabel);
        y += 26;

        chkMinimizeToTray = new CheckBox
        {
            Text = "Minimize to tray (ซ่อนใน system tray)",
            Location = new Point(fieldX, y),
            Size = new Size(fieldWidth, 22),
            Checked = _config.MinimizeToTray,
            AutoSize = false
        };
        this.Controls.Add(chkMinimizeToTray);
        y += 24;

        chkAutoStart = new CheckBox
        {
            Text = "Auto-start เมื่อเปิด Windows (Startup)",
            Location = new Point(fieldX, y),
            Size = new Size(fieldWidth, 22),
            Checked = _config.AutoStart,
            AutoSize = false
        };
        this.Controls.Add(chkAutoStart);
        y += 24;

        var btnService = new Button
        {
            Text = ServiceManager.IsInstalled() ? "❌ Uninstall Service" : "⚙ Install as Windows Service",
            Location = new Point(fieldX, y),
            Size = new Size(250, 30),
            Font = new Font("Segoe UI", 9)
        };
        btnService.Click += (s, e) =>
        {
            if (ServiceManager.IsInstalled())
            {
                if (MessageBox.Show("Uninstall Windows Service?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ServiceManager.Uninstall();
                    btnService.Text = "⚙ Install as Windows Service";
                    MessageBox.Show("Service removed.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                if (MessageBox.Show("Install ExpressETL as a Windows Service?\n\n" +
                    "This will run ETL automatically even without login.", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ServiceManager.Install(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
                    ServiceManager.Start();
                    btnService.Text = "❌ Uninstall Service";
                    MessageBox.Show("Service installed and started!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        };
        this.Controls.Add(btnService);
        y += 36;

        // ERPNext Direct Sync section
        var erpLabel = new Label
        {
            Text = " ERPNext Direct Sync",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(leftMargin, y),
            Size = new Size(420, 24),
            AutoSize = false
        };
        this.Controls.Add(erpLabel);
        y += 26;

        chkSyncToErpNext = new CheckBox
        {
            Text = "Sync เข้า ERPNext โดยตรง (REST API)",
            Location = new Point(fieldX, y),
            Size = new Size(fieldWidth, 22),
            Checked = _config.SyncToErpNext,
            AutoSize = false
        };
        chkSyncToErpNext.CheckedChanged += (s, e) =>
        {
            txtErpUrl.Enabled = chkSyncToErpNext.Checked;
            txtErpApiKey.Enabled = chkSyncToErpNext.Checked;
            txtErpApiSecret.Enabled = chkSyncToErpNext.Checked;
        };
        this.Controls.Add(chkSyncToErpNext);
        y += 24;

        txtErpUrl = new TextBox { Text = _config.ErpNextUrl, Enabled = _config.SyncToErpNext };
        AddField("ERPNext URL:", txtErpUrl);

        txtErpApiKey = new TextBox { Text = _config.ErpNextApiKey, Enabled = _config.SyncToErpNext };
        AddField("API Key:", txtErpApiKey);

        txtErpApiSecret = new TextBox { Text = _config.ErpNextApiSecret, UseSystemPasswordChar = true, Enabled = _config.SyncToErpNext };
        AddField("API Secret:", txtErpApiSecret);

        // Field Mapping section
        var fmLabel = new Label
        {
            Text = " Field Mapping",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(leftMargin, y),
            Size = new Size(420, 24),
            AutoSize = false
        };
        this.Controls.Add(fmLabel);
        y += 26;

        var fmInfo = new Label
        {
            Text = "เปลี่ยนชื่อ field ถ้า DBF ของคุณใช้ชื่อต่างจาก default",
            Font = new Font("Segoe UI", 8),
            ForeColor = Color.Gray,
            Location = new Point(fieldX, y),
            Size = new Size(fieldWidth + 40, 16),
            AutoSize = false
        };
        this.Controls.Add(fmInfo);
        y += 18;

        var fm = _config.FieldMapping;
        var fmFields = new[]
        {
            ("Customer Code", fm.CustomerCodeField),
            ("Customer Name", fm.CustomerNameField),
            ("Supplier Code", fm.SupplierCodeField),
            ("Supplier Name", fm.SupplierNameField),
            ("Item Code", fm.ItemCodeField),
            ("Item Name", fm.ItemNameField),
            ("Sale Price", fm.ItemSalePriceField),
            ("Cost Price", fm.ItemCostPriceField),
        };

        txtFieldMappings = new TextBox[fmFields.Length];
        for (int i = 0; i < fmFields.Length; i++)
        {
            var (label, value) = fmFields[i];
            var lbl = new Label
            {
                Text = label + ":",
                Location = new Point(leftMargin, y + 8),
                Size = new Size(100, 20),
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Font = new Font("Segoe UI", 8)
            };
            this.Controls.Add(lbl);

            var txt = new TextBox { Text = value, Font = new Font("Consolas", 9) };
            txt.Location = new Point(130, y);
            txt.Size = new Size(120, 24);
            this.Controls.Add(txt);
            txtFieldMappings[i] = txt;

            if (i % 2 == 0 && i + 1 < fmFields.Length)
            {
                var (label2, value2) = fmFields[i + 1];
                var lbl2 = new Label
                {
                    Text = label2 + ":",
                    Location = new Point(270, y + 8),
                    Size = new Size(100, 20),
                    TextAlign = ContentAlignment.MiddleRight,
                    AutoSize = false,
                    Font = new Font("Segoe UI", 8)
                };
                this.Controls.Add(lbl2);

                var txt2 = new TextBox { Text = value2, Font = new Font("Consolas", 9) };
                txt2.Location = new Point(380, y);
                txt2.Size = new Size(120, 24);
                this.Controls.Add(txt2);
                txtFieldMappings[i + 1] = txt2;

                i++; // Skip next iteration
            }

            y += 28;
        }

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
        var target = new AccountingETL.Adapters.PostgreSQL.PostgreSqlTargetAdapter(
            tempConfig.ConnectionString, "express_staging");

        bool ok = await target.ValidateConnectionAsync();

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
        _config.LineToken = txtLineToken.Text.Trim();
        _config.NotifyOnSuccess = chkNotifySuccess.Checked;
        _config.NotifyOnFailure = chkNotifyFailure.Checked;
        _config.MinimizeToTray = chkMinimizeToTray.Checked;
        _config.AutoStart = chkAutoStart.Checked;
        _config.DbfEncoding = (cmbEncoding.SelectedValue as dynamic)?.Code?.ToString() ?? "tis-620";
        _config.SyncToErpNext = chkSyncToErpNext.Checked;
        _config.ErpNextUrl = txtErpUrl.Text.Trim();
        _config.ErpNextApiKey = txtErpApiKey.Text.Trim();
        _config.ErpNextApiSecret = txtErpApiSecret.Text;

        // Field Mapping
        if (txtFieldMappings.Length >= 8)
        {
            var fm = new FieldMappingConfig
            {
                CustomerCodeField = txtFieldMappings[0].Text.Trim(),
                CustomerNameField = txtFieldMappings[1].Text.Trim(),
                SupplierCodeField = txtFieldMappings[2].Text.Trim(),
                SupplierNameField = txtFieldMappings[3].Text.Trim(),
                ItemCodeField = txtFieldMappings[4].Text.Trim(),
                ItemNameField = txtFieldMappings[5].Text.Trim(),
                ItemSalePriceField = txtFieldMappings[6].Text.Trim(),
                ItemCostPriceField = txtFieldMappings[7].Text.Trim()
            };
            _config.FieldMapping = fm;
        }

        // Apply auto-start immediately
        if (_config.AutoStart) AutoStartManager.Enable();
        else AutoStartManager.Disable();
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
            IntervalHours = (int)numInterval.Value,
            LineToken = txtLineToken.Text.Trim(),
            NotifyOnSuccess = chkNotifySuccess.Checked,
            NotifyOnFailure = chkNotifyFailure.Checked,
            MinimizeToTray = chkMinimizeToTray.Checked,
            AutoStart = chkAutoStart.Checked,
            DbfEncoding = (cmbEncoding.SelectedValue as dynamic)?.Code?.ToString() ?? "tis-620",
            SyncToErpNext = chkSyncToErpNext.Checked,
            ErpNextUrl = txtErpUrl.Text.Trim(),
            ErpNextApiKey = txtErpApiKey.Text.Trim(),
            ErpNextApiSecret = txtErpApiSecret.Text,
            FieldMapping = txtFieldMappings.Length >= 8 ? new FieldMappingConfig
            {
                CustomerCodeField = txtFieldMappings[0].Text.Trim(),
                CustomerNameField = txtFieldMappings[1].Text.Trim(),
                SupplierCodeField = txtFieldMappings[2].Text.Trim(),
                SupplierNameField = txtFieldMappings[3].Text.Trim(),
                ItemCodeField = txtFieldMappings[4].Text.Trim(),
                ItemNameField = txtFieldMappings[5].Text.Trim(),
                ItemSalePriceField = txtFieldMappings[6].Text.Trim(),
                ItemCostPriceField = txtFieldMappings[7].Text.Trim()
            } : new FieldMappingConfig()
        };
    }
}
