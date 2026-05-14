using Npgsql;

namespace ExpressETL;

public partial class SyncHistoryForm : Form
{
    private readonly AppConfig _config;

    public SyncHistoryForm(AppConfig config)
    {
        _config = config;
        InitializeComponent();
        LoadHistory();
    }

    private DataGridView dgv = null!;
    private Label lblSummary = null!;
    private Button btnRefresh = null!;
    private Button btnClose = null!;

    private void InitializeComponent()
    {
        this.Text = "📊 Sync History";
        this.Size = new Size(600, 420);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = Color.FromArgb(245, 245, 245);

        int y = 12;
        int leftMargin = 12;
        int contentWidth = 560;

        // Summary label
        lblSummary = new Label
        {
            Text = "Loading...",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(leftMargin, y),
            Size = new Size(contentWidth, 24),
            AutoSize = false
        };
        this.Controls.Add(lblSummary);
        y += 30;

        // Data grid
        dgv = new DataGridView
        {
            Location = new Point(leftMargin, y),
            Size = new Size(contentWidth, 280),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9)
        };
        this.Controls.Add(dgv);
        y += 290;

        // Buttons
        btnRefresh = new Button
        {
            Text = "🔄 Refresh",
            Location = new Point(leftMargin, y),
            Size = new Size(100, 32),
            Font = new Font("Segoe UI", 9)
        };
        btnRefresh.Click += (s, e) => LoadHistory();
        this.Controls.Add(btnRefresh);

        btnClose = new Button
        {
            Text = "Close",
            Location = new Point(leftMargin + 110, y),
            Size = new Size(80, 32),
            DialogResult = DialogResult.OK,
            Font = new Font("Segoe UI", 9)
        };
        this.Controls.Add(btnClose);
    }

    private async void LoadHistory()
    {
        lblSummary.Text = "Loading...";
        dgv.Rows.Clear();
        dgv.Columns.Clear();

        dgv.Columns.Add("sync_time", "เวลา");
        dgv.Columns.Add("table_name", "ตาราง");
        dgv.Columns.Add("inserted", "+เพิ่ม");
        dgv.Columns.Add("updated", "~อัปเดต");
        dgv.Columns.Add("skipped", "⊘ข้าม");
        dgv.Columns.Add("duration_ms", "เวลา (ms)");
        dgv.Columns.Add("status", "สถานะ");

        dgv.Columns["sync_time"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss";
        dgv.Columns["duration_ms"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        dgv.Columns["inserted"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        dgv.Columns["updated"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        dgv.Columns["skipped"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        try
        {
            await using var conn = new NpgsqlConnection(_config.ConnectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand("""
                SELECT sync_time, table_name, inserted, updated, skipped, duration_ms, status
                FROM express_staging.sync_log
                ORDER BY sync_time DESC, id DESC
                LIMIT 100
                """, conn);

            await using var reader = await cmd.ExecuteReaderAsync();
            int totalInserted = 0, totalUpdated = 0, totalSkipped = 0, syncCount = 0;

            while (await reader.ReadAsync())
            {
                var row = new DataGridViewRow();
                row.CreateCells(dgv);
                row.Cells[0].Value = reader.GetDateTime(0);
                row.Cells[1].Value = reader.GetString(1);
                row.Cells[2].Value = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                row.Cells[3].Value = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                row.Cells[4].Value = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                row.Cells[5].Value = reader.IsDBNull(5) ? 0 : reader.GetInt64(5);
                row.Cells[6].Value = reader.GetString(6);

                // Color coding
                var status = reader.GetString(6);
                if (status == "success") row.Cells[6].Style.ForeColor = Color.DarkGreen;
                else if (status == "running") row.Cells[6].Style.ForeColor = Color.DarkBlue;
                else row.Cells[6].Style.ForeColor = Color.Red;

                totalInserted += row.Cells[2].Value is int ins ? ins : 0;
                totalUpdated += row.Cells[3].Value is int upd ? upd : 0;
                totalSkipped += row.Cells[4].Value is int skp ? skp : 0;
                syncCount++;

                dgv.Rows.Add(row);
            }

            lblSummary.Text = $"ทั้งหมด {syncCount} sync | +{totalInserted} เพิ่ม | ~{totalUpdated} อัปเดต | {totalSkipped} ข้าม";
            lblSummary.ForeColor = Color.DarkGreen;
        }
        catch (Exception ex)
        {
            lblSummary.Text = $"✗ Error: {ex.Message}";
            lblSummary.ForeColor = Color.Red;
        }
    }
}
