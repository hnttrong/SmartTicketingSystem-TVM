using TvmDemo.Controllers;
using TvmDemo.Models;

namespace TvmDemo.Views;

/// <summary>
/// Output Report window displaying a <see cref="DataGridView"/> of recent TVM transactions
/// joined across <c>dbo.[Transaction]</c>, <c>dbo.Destination</c>, <c>dbo.Payment</c>, and <c>dbo.Ticket</c>.
/// </summary>
public class TransactionReportForm : Form
{
    private readonly TicketController _controller;
    private readonly bool _isVietnamese;
    private readonly ComboBox _cboStatus = new();
    private readonly BufferedDataGridView _grid = new();
    private readonly Label _lblSummary = new();
    private readonly Label _lblError = new();

    public TransactionReportForm(TicketController controller, bool isVietnamese)
    {
        _controller = controller;
        _isVietnamese = isVietnamese;

        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;

        Rectangle workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        int targetW = Math.Min(1220, Math.Max(900, workArea.Width - 48));
        int targetH = Math.Min(680, Math.Max(520, workArea.Height - 64));

        Text = isVietnamese
            ? "Báo cáo Giao dịch & Vé đã phát hành (MSSQL Output Report)"
            : "TVM Transaction & Issued Ticket Report (MSSQL Output Report)";
        ClientSize = new Size(targetW, targetH);
        MinimumSize = new Size(880, 480);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(241, 245, 249);
        Font = KioskTheme.PxFont(14f);

        BuildLayout();
        Shown += async (_, _) => await RefreshDataAsync();
    }

    private void BuildLayout()
    {
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = KioskTheme.DarkNavyText,
            Padding = new Padding(18, 12, 18, 12)
        };

        var lblTitle = new Label
        {
            Text = _isVietnamese
                ? "▤ LỊCH SỬ GIAO DỊCH TVM (JOIN: Transaction + Destination + Payment + Ticket)"
                : "▤ RECENT TVM TRANSACTIONS (JOIN: Transaction + Destination + Payment + Ticket)",
            UseMnemonic = false,
            ForeColor = Color.White,
            Font = KioskTheme.PxFont(17f, FontStyle.Bold),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var rightToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = KioskTheme.DarkNavyText,
            Padding = new Padding(0, 4, 0, 0)
        };

        var lblFilter = new Label
        {
            Text = _isVietnamese ? "Trạng thái:" : "Status:",
            UseMnemonic = false,
            ForeColor = Color.White,
            Font = KioskTheme.PxFont(14f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 7, 8, 0)
        };

        _cboStatus.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboStatus.Items.AddRange(new object[] { "ALL", "Completed", "Failed", "Cancelled", "Pending" });
        _cboStatus.SelectedIndex = 0;
        _cboStatus.Font = KioskTheme.PxFont(14f);
        _cboStatus.Width = 135;
        _cboStatus.Margin = new Padding(0, 3, 12, 0);
        _cboStatus.SelectedIndexChanged += async (_, _) => await RefreshDataAsync();

        var btnRefresh = new Button
        {
            Text = _isVietnamese ? "↻ Làm mới" : "↻ Refresh",
            UseMnemonic = false,
            Size = new Size(140, 34),
            BackColor = KioskTheme.Teal,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = KioskTheme.PxFont(14f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 0)
        };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += async (_, _) => await RefreshDataAsync();

        rightToolbar.Controls.AddRange(new Control[] { lblFilter, _cboStatus, btnRefresh });
        topPanel.Controls.Add(lblTitle);
        topPanel.Controls.Add(rightToolbar);

        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = Color.White,
            Padding = new Padding(18, 10, 18, 10)
        };

        _lblSummary.Dock = DockStyle.Left;
        _lblSummary.UseMnemonic = false;
        _lblSummary.AutoSize = true;
        _lblSummary.Font = KioskTheme.PxFont(14.5f, FontStyle.Bold);
        _lblSummary.ForeColor = KioskTheme.DarkNavyText;

        _lblError.Dock = DockStyle.Right;
        _lblError.UseMnemonic = false;
        _lblError.AutoSize = true;
        _lblError.ForeColor = KioskTheme.ErrorRed;
        _lblError.Font = KioskTheme.PxFont(13.5f, FontStyle.Bold);

        bottomPanel.Controls.Add(_lblSummary);
        bottomPanel.Controls.Add(_lblError);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeight = 40;
        _grid.RowTemplate.Height = 34;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = KioskTheme.DeepBlue;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = KioskTheme.PxFont(13.5f, FontStyle.Bold);
        _grid.Font = KioskTheme.PxFont(13.5f);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Txn Code", DataPropertyName = "TransactionCode", Width = 155 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", DataPropertyName = "CreatedAt", Width = 140, DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm:ss" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Station", DataPropertyName = _isVietnamese ? "StationNameVi" : "StationNameEn", Width = 145 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qty", DataPropertyName = "Quantity", Width = 48 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total (VND)", DataPropertyName = "TotalAmountVnd", Width = 100, DefaultCellStyle = { Format = "N0" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mode", DataPropertyName = "PaymentMode", Width = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Provider", DataPropertyName = "ProviderName", Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Pay Status", DataPropertyName = "PaymentStatus", Width = 92 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Txn Status", DataPropertyName = "TransactionStatus", Width = 92 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ticket Number(s)", DataPropertyName = "TicketNumbers", Width = 185 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Barcode", DataPropertyName = "PrimaryBarcode", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 160 });

        _grid.RowPrePaint += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            if (_grid.Rows[e.RowIndex].DataBoundItem is not TransactionReportRow row) return;

            _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = row.TransactionStatus switch
            {
                "Completed" => Color.FromArgb(236, 253, 245),
                "Failed" => Color.FromArgb(254, 242, 242),
                "Cancelled" => Color.FromArgb(255, 251, 235),
                _ => Color.White
            };
        };

        Controls.Add(_grid);
        Controls.Add(bottomPanel);
        Controls.Add(topPanel);
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            _lblError.Text = string.Empty;
            string? filter = _cboStatus.SelectedItem?.ToString();
            List<TransactionReportRow> rows = await _controller.LoadReportRowsAsync(50, filter);
            _grid.DataSource = rows;

            decimal revenue = rows.Where(r => r.TransactionStatus == "Completed").Sum(r => r.TotalAmountVnd);
            int tickets = rows.Sum(r => r.TicketsIssuedCount);
            _lblSummary.Text = _isVietnamese
                ? $"Tổng số dòng: {rows.Count}  |  Vé đã in: {tickets}  |  Doanh thu (Completed): {revenue:N0} VND"
                : $"Rows loaded: {rows.Count}  |  Printed Tickets: {tickets}  |  Collected Revenue (Completed): {revenue:N0} VND";
        }
        catch (Exception ex)
        {
            _grid.DataSource = null;
            _lblSummary.Text = _isVietnamese ? "Không thể tải dữ liệu báo cáo." : "Unable to load report data.";
            _lblError.Text = $"DB Error: {ex.Message}";
        }
    }
}
