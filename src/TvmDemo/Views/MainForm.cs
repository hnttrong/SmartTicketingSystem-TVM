using TvmDemo.Controllers;
using TvmDemo.Data;
using TvmDemo.Models;
using TvmDemo.Services;

namespace TvmDemo.Views;

/// <summary>
/// Main 1280x800 touchscreen kiosk View (MVC View layer) hosting screens S01 through S08
/// and a docked Instructor Simulation Strip for switching <see cref="MockPaymentGateway"/> modes
/// and SQL Server connection profiles during live grading demonstrations.
/// Automatically scales proportionally to fit smaller or High-DPI laptop displays without clipping.
/// </summary>
public class MainForm : Form
{
    private readonly TicketController _controller;
    private readonly AppSettingsConfig _appConfig;
    private readonly float _uiScale;

    private bool _isVietnamese;
    private bool _isAccessibilityMode;
    private string _activeScreenId = "S01";
    private string _returnScreenBeforeCancel = "S02";

    // Async payment & countdown timer state (prevents UI freeze)
    private CancellationTokenSource? _paymentCts;
    private readonly System.Windows.Forms.Timer _countdownTimer = new() { Interval = 1000 };
    private int _remainingSeconds;
    private bool _cancelRequestedDuringPayment;

    // Persistent Kiosk Chrome
    private readonly Panel _bezelHost = new();
    private readonly GradientBackdropPanel _kioskViewport = new();
    private readonly HeaderBarPanel _headerBar = new();
    private readonly KioskLabel _lblHeaderTitle = new();
    private readonly KioskLabel _lblHeaderSub = new();
    private readonly KioskButton _btnLang = new();
    private readonly KioskButton _btnA11y = new();
    private readonly StepProgressControl _progressBar = new();
    private readonly ScreenPanel _screenHost = new();

    // Screen Panels (S01 - S08)
    private readonly Dictionary<string, ScreenPanel> _screens = new();

    // S01 controls
    private readonly KioskLabel _lblS01Badge = new();
    private readonly KioskLabel _lblS01Title = new();
    private readonly KioskLabel _lblS01Desc = new();
    private readonly KioskButton _btnS01Start = new();
    private readonly KioskLabel _lblS01FareInfo = new();
    private readonly KioskButton _btnS01Cancel = new();

    // S02 controls
    private readonly KioskLabel _lblS02Title = new();
    private readonly KioskLabel _lblS02QtyCaption = new();
    private readonly KioskLabel _lblS02QtyVal = new();
    private readonly GlassGridPanel _stationGrid = new();
    private readonly List<KioskButton> _stationButtons = new();
    private readonly KioskLabel _lblS02Summary = new();
    private readonly KioskButton _btnS02Cancel = new();
    private readonly KioskButton _btnS02Next = new();
    private Destination? _selectedDestination;
    private int _selectedQuantity = 1;

    // S03 controls
    private readonly KioskLabel _lblS03Title = new();
    private readonly KioskLabel _lblS03RouteInfo = new();
    private readonly KioskLabel _lblS03Total = new();
    private readonly KioskButton _btnModeCard = new();
    private readonly KioskButton _btnModeBankQr = new();
    private readonly KioskButton _btnModeWallet = new();
    private readonly KioskLabel _lblWalletPick = new();
    private readonly ComboBox _cboWalletBrand = new();
    private readonly KioskButton _btnS03Cancel = new();
    private readonly KioskButton _btnS03Back = new();
    private readonly KioskButton _btnS03Pay = new();

    // S04a Card Payment controls
    private readonly KioskLabel _lblS04aTitle = new();
    private readonly KioskLabel _lblS04aTimer = new();
    private readonly KioskLabel _lblS04aPrompt = new();
    private readonly KioskLabel _lblS04aSummary = new();
    private readonly KioskLabel _lblS04aReaderStatus = new();
    private readonly KioskButton _btnS04aCancel = new();

    // S04b QR/Wallet Payment controls
    private readonly KioskLabel _lblS04bTitle = new();
    private readonly KioskLabel _lblS04bTimer = new();
    private readonly KioskLabel _lblS04bPrompt = new();
    private readonly KioskLabel _lblS04bSummary = new();
    private readonly PictureBox _picQrCode = new();
    private readonly KioskLabel _lblS04bProvider = new();
    private readonly KioskButton _btnS04bCancel = new();

    // S05a Ticket Issued & Mini Report controls
    private readonly KioskLabel _lblS05aBadge = new();
    private readonly KioskLabel _lblS05aTitle = new();
    private readonly KioskLabel _lblS05aTicketHeader = new();
    private readonly KioskLabel _lblS05aTicketDetails = new();
    private readonly PictureBox _picBarcode = new();
    private readonly BufferedDataGridView _miniReportGrid = new();
    private readonly KioskButton _btnS05aOpenFullReport = new();
    private readonly KioskButton _btnS05aCancel = new();
    private readonly KioskButton _btnS05aDone = new();

    // S05b Declined controls
    private readonly KioskLabel _lblS05bTitle = new();
    private readonly KioskLabel _lblS05bDetail = new();
    private readonly KioskLabel _lblS05bChargeBanner = new();
    private readonly KioskButton _btnS05bCancel = new();
    private readonly KioskButton _btnS05bRetry = new();

    // S06 Cancel Confirmation controls
    private readonly KioskLabel _lblS06Title = new();
    private readonly KioskLabel _lblS06Detail = new();
    private readonly KioskLabel _lblS06ChargeBanner = new();
    private readonly KioskButton _btnS06ConfirmCancel = new();
    private readonly KioskButton _btnS06Continue = new();

    // S07 Timeout controls
    private readonly KioskLabel _lblS07Title = new();
    private readonly KioskLabel _lblS07Detail = new();
    private readonly KioskLabel _lblS07ChargeBanner = new();
    private readonly KioskButton _btnS07Cancel = new();
    private readonly KioskButton _btnS07Retry = new();

    // S08 Out of Service controls
    private readonly KioskLabel _lblS08Title = new();
    private readonly KioskLabel _lblS08Reason = new();
    private readonly KioskLabel _lblS08ChargeBanner = new();
    private readonly KioskLabel _lblS08Support = new();
    private readonly KioskButton _btnS08Cancel = new();
    private readonly KioskButton _btnS08DisabledInfo = new();
    private bool _lastS08WasCharged;

    // Instructor Demo Simulation Strip (docked at bottom, always visible)
    private readonly ComboBox _cboGatewaySim = new();
    private readonly CheckBox _chkPrinterFault = new();
    private readonly ComboBox _cboDbProfile = new();
    private readonly Label _lblDbStatus = new();

    public MainForm(TicketController controller, AppSettingsConfig appConfig)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));

        // Disable automatic WinForms Font-based rescaling so pixel geometry & pixel fonts match 1:1
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;

        Rectangle workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1032);
        }

        float scaleX = (workArea.Width - 28f) / 1280f;
        float scaleY = (workArea.Height - 56f) / 852f;
        _uiScale = Math.Clamp(Math.Min(1.0f, Math.Min(scaleX, scaleY)), 0.68f, 1.0f);

        Text = "HCMC Smart Ticketing System – Ticket Vendor Machine (TVM Demo - UC01)";
        BackColor = Color.FromArgb(9, 22, 44);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        Font = FFixed(16f);

        BuildDemoSimulationBar();
        BuildKioskShell();
        BuildAllScreens();
        ApplyModeTypography();

        // Set exact ClientSize AFTER FormBorderStyle & Font so WinForms preserves exact pixel dimensions
        ClientSize = new Size(S(1280), S(852));
        MinimumSize = Size;

        _countdownTimer.Tick += OnCountdownTickAsync;
        Shown += async (_, _) => await InitializeSystemAsync();
    }

    private int S(int px) => (int)Math.Round(px * _uiScale);

    private Rectangle R(int x, int y, int w, int h) => new(S(x), S(y), S(w), S(h));

    private Font FFixed(float px, FontStyle style = FontStyle.Regular)
        => KioskTheme.PxFont(px * _uiScale, style);

    private Font FScale(float px, FontStyle style = FontStyle.Regular)
        => KioskTheme.PxFont(px * _uiScale * (_isAccessibilityMode ? 1.12f : 1.0f), style);

    private void BuildKioskShell()
    {
        _bezelHost.Dock = DockStyle.Fill;
        _bezelHost.BackColor = Color.FromArgb(9, 22, 44);
        _bezelHost.Resize += (_, _) => CenterKioskViewport();

        _kioskViewport.Size = new Size(S(1280), S(800));
        _kioskViewport.Location = new Point(0, 0);

        // Top Persistent Utility Bar (84 px)
        _headerBar.Bounds = R(0, 0, 1280, 84);

        var badge = new KioskLabel
        {
            Text = "M1",
            Bounds = R(28, 14, 56, 56),
            FillColor = KioskTheme.Teal,
            ForeColor = Color.White,
            CornerRadius = S(12),
            TextAlign = ContentAlignment.MiddleCenter,
            WordWrap = false,
            Font = FFixed(22f, FontStyle.Bold)
        };

        _lblHeaderTitle.Bounds = R(98, 12, 680, 34);
        _lblHeaderTitle.ForeColor = Color.White;
        _lblHeaderTitle.TextAlign = ContentAlignment.MiddleLeft;
        _lblHeaderTitle.WordWrap = false;
        _lblHeaderTitle.Font = FFixed(23f, FontStyle.Bold);

        _lblHeaderSub.Bounds = R(98, 46, 680, 26);
        _lblHeaderSub.ForeColor = Color.FromArgb(205, 232, 255);
        _lblHeaderSub.TextAlign = ContentAlignment.MiddleLeft;
        _lblHeaderSub.WordWrap = false;
        _lblHeaderSub.Font = FFixed(16f, FontStyle.Regular);

        _btnLang.Bounds = R(824, 10, 170, 64);
        _btnLang.CornerRadius = S(14);
        _btnLang.Variant = KioskButtonVariant.HeaderUtility;
        _btnLang.Font = FFixed(17f, FontStyle.Bold);
        _btnLang.Text = "🌐 VI / EN";
        _btnLang.Click += (_, _) =>
        {
            _isVietnamese = !_isVietnamese;
            ApplyLocalization();
        };

        _btnA11y.Bounds = R(1006, 10, 246, 64);
        _btnA11y.CornerRadius = S(14);
        _btnA11y.Variant = KioskButtonVariant.HeaderUtility;
        _btnA11y.Font = FFixed(16.5f, FontStyle.Bold);
        _btnA11y.Click += (_, _) =>
        {
            _isAccessibilityMode = !_isAccessibilityMode;
            ApplyAccessibilityMode();
        };

        _headerBar.Controls.AddRange(new Control[] { badge, _lblHeaderTitle, _lblHeaderSub, _btnLang, _btnA11y });

        // 4-Step Progress Bar (58 px)
        _progressBar.Bounds = R(28, 94, 1224, 58);
        _progressBar.UiScale = _uiScale;

        // Screen Host Container (624 px high)
        _screenHost.Bounds = R(28, 162, 1224, 624);

        _kioskViewport.Controls.Add(_screenHost);
        _kioskViewport.Controls.Add(_progressBar);
        _kioskViewport.Controls.Add(_headerBar);

        _bezelHost.Controls.Add(_kioskViewport);
        Controls.Add(_bezelHost);
        _bezelHost.BringToFront();
    }

    private void CenterKioskViewport()
    {
        int x = Math.Max(0, (_bezelHost.ClientSize.Width - _kioskViewport.Width) / 2);
        int y = Math.Max(0, (_bezelHost.ClientSize.Height - _kioskViewport.Height) / 2);
        _kioskViewport.Location = new Point(x, y);
    }

    private ScreenPanel CreateScreenPanel(string id)
    {
        var p = new ScreenPanel
        {
            Bounds = R(0, 0, 1224, 624),
            Visible = false
        };
        _screens[id] = p;
        _screenHost.Controls.Add(p);
        return p;
    }

    private GlassPanel CreateGlassCard(int x, int y, int w, int h, int radius = 22, Color? accentBorder = null)
    {
        return new GlassPanel
        {
            Bounds = R(x, y, w, h),
            CornerRadius = S(radius),
            AccentBorderColor = accentBorder
        };
    }

    private void BuildAllScreens()
    {
        BuildScreenS01();
        BuildScreenS02();
        BuildScreenS03();
        BuildScreenS04a();
        BuildScreenS04b();
        BuildScreenS05a();
        BuildScreenS05b();
        BuildScreenS06();
        BuildScreenS07();
        BuildScreenS08();
        ApplyLocalization();
    }

    private void BuildScreenS01()
    {
        ScreenPanel s = CreateScreenPanel("S01");
        GlassPanel hero = CreateGlassCard(0, 0, 1224, 536);

        _lblS01Badge.Bounds = R(362, 44, 500, 46);
        _lblS01Badge.FillColor = KioskTheme.SuccessGreen;
        _lblS01Badge.ForeColor = Color.White;
        _lblS01Badge.CornerRadius = S(23);
        _lblS01Badge.TextAlign = ContentAlignment.MiddleCenter;
        _lblS01Badge.WordWrap = false;

        _lblS01Title.Bounds = R(48, 114, 1128, 76);
        _lblS01Title.ForeColor = KioskTheme.DarkNavyText;
        _lblS01Title.TextAlign = ContentAlignment.MiddleCenter;

        _lblS01Desc.Bounds = R(90, 200, 1044, 92);
        _lblS01Desc.ForeColor = KioskTheme.DarkNavyText;
        _lblS01Desc.TextAlign = ContentAlignment.MiddleCenter;

        _btnS01Start.Bounds = R(352, 326, 520, 92);
        _btnS01Start.CornerRadius = S(20);
        _btnS01Start.Variant = KioskButtonVariant.PrimarySolid;
        _btnS01Start.Click += (_, _) =>
        {
            if (!_controller.TryStartSession(out string fault))
            {
                ShowOutOfService(fault, wasCharged: false);
                return;
            }
            PopulateStationButtons();
            NavigateTo("S02");
        };

        hero.Controls.AddRange(new Control[] { _lblS01Badge, _lblS01Title, _lblS01Desc, _btnS01Start });

        GlassPanel infoPanel = CreateGlassCard(0, 548, 930, 72, radius: 18);
        _lblS01FareInfo.Bounds = R(22, 12, 886, 48);
        _lblS01FareInfo.ForeColor = KioskTheme.DarkNavyText;
        _lblS01FareInfo.TextAlign = ContentAlignment.MiddleLeft;
        _lblS01FareInfo.WordWrap = false;
        infoPanel.Controls.Add(_lblS01FareInfo);

        _btnS01Cancel.Bounds = R(948, 550, 276, 70);
        _btnS01Cancel.CornerRadius = S(16);
        _btnS01Cancel.Variant = KioskButtonVariant.CancelRedOutline;
        _btnS01Cancel.Click += (_, _) => ResetToIdleHome();

        s.Controls.AddRange(new Control[] { hero, infoPanel, _btnS01Cancel });
    }

    private void BuildScreenS02()
    {
        ScreenPanel s = CreateScreenPanel("S02");
        GlassPanel mainCard = CreateGlassCard(0, 0, 1224, 536);

        _lblS02Title.Bounds = R(24, 14, 700, 54);
        _lblS02Title.ForeColor = KioskTheme.DarkNavyText;
        _lblS02Title.TextAlign = ContentAlignment.MiddleLeft;
        _lblS02Title.WordWrap = false;

        _lblS02QtyCaption.Bounds = R(730, 18, 220, 48);
        _lblS02QtyCaption.ForeColor = KioskTheme.DarkNavyText;
        _lblS02QtyCaption.TextAlign = ContentAlignment.MiddleRight;
        _lblS02QtyCaption.WordWrap = false;

        var btnMinus = new KioskButton
        {
            Text = "−",
            Bounds = R(960, 11, 68, 64),
            CornerRadius = S(14),
            Variant = KioskButtonVariant.SecondaryGlass,
            Font = FFixed(28f, FontStyle.Bold)
        };
        btnMinus.Click += (_, _) => AdjustQuantity(-1);

        _lblS02QtyVal.Bounds = R(1034, 11, 88, 64);
        _lblS02QtyVal.FillColor = Color.White;
        _lblS02QtyVal.BorderColor = KioskTheme.DeepBlue;
        _lblS02QtyVal.CornerRadius = S(14);
        _lblS02QtyVal.ForeColor = KioskTheme.DeepBlue;
        _lblS02QtyVal.TextAlign = ContentAlignment.MiddleCenter;
        _lblS02QtyVal.WordWrap = false;

        var btnPlus = new KioskButton
        {
            Text = "+",
            Bounds = R(1128, 11, 68, 64),
            CornerRadius = S(14),
            Variant = KioskButtonVariant.SecondaryGlass,
            Font = FFixed(28f, FontStyle.Bold)
        };
        btnPlus.Click += (_, _) => AdjustQuantity(1);

        _stationGrid.Bounds = R(18, 84, 1188, 434);
        _stationGrid.ColumnCount = 4;
        _stationGrid.RowCount = 3;
        for (int c = 0; c < 4; c++) _stationGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        for (int r = 0; r < 3; r++) _stationGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

        mainCard.Controls.AddRange(new Control[] { _lblS02Title, _lblS02QtyCaption, btnMinus, _lblS02QtyVal, btnPlus, _stationGrid });

        _btnS02Cancel.Bounds = R(0, 550, 252, 70);
        _btnS02Cancel.CornerRadius = S(16);
        _btnS02Cancel.Variant = KioskButtonVariant.CancelRedOutline;
        _btnS02Cancel.Click += (_, _) => PromptCancelConfirmation("S02");

        GlassPanel summaryGlass = CreateGlassCard(266, 548, 636, 74, radius: 18);
        _lblS02Summary.Bounds = R(14, 10, 608, 52);
        _lblS02Summary.ForeColor = KioskTheme.DarkNavyText;
        _lblS02Summary.TextAlign = ContentAlignment.MiddleCenter;
        _lblS02Summary.WordWrap = false;
        summaryGlass.Controls.Add(_lblS02Summary);

        _btnS02Next.Bounds = R(916, 550, 308, 70);
        _btnS02Next.CornerRadius = S(16);
        _btnS02Next.Variant = KioskButtonVariant.PrimarySolid;
        _btnS02Next.Click += (_, _) =>
        {
            if (_selectedDestination == null) return;
            _controller.SelectDestinationAndQuantity(_selectedDestination, _selectedQuantity);
            if (!_controller.TryProceedToPaymentMode(out string fault))
            {
                ShowOutOfService(fault, wasCharged: false);
                return;
            }
            UpdateOrderDisplays();
            NavigateTo("S03");
        };

        s.Controls.AddRange(new Control[] { mainCard, _btnS02Cancel, summaryGlass, _btnS02Next });
    }

    private void BuildScreenS03()
    {
        ScreenPanel s = CreateScreenPanel("S03");
        GlassPanel topSummary = CreateGlassCard(0, 0, 1224, 118);

        _lblS03Title.Bounds = R(24, 12, 760, 48);
        _lblS03Title.ForeColor = KioskTheme.DarkNavyText;
        _lblS03Title.TextAlign = ContentAlignment.MiddleLeft;
        _lblS03Title.WordWrap = false;

        _lblS03RouteInfo.Bounds = R(24, 62, 760, 40);
        _lblS03RouteInfo.ForeColor = KioskTheme.DarkNavyText;
        _lblS03RouteInfo.TextAlign = ContentAlignment.MiddleLeft;
        _lblS03RouteInfo.WordWrap = false;

        _lblS03Total.Bounds = R(790, 20, 406, 76);
        _lblS03Total.ForeColor = KioskTheme.DeepBlue;
        _lblS03Total.TextAlign = ContentAlignment.MiddleRight;
        _lblS03Total.WordWrap = false;

        topSummary.Controls.AddRange(new Control[] { _lblS03Title, _lblS03RouteInfo, _lblS03Total });

        GlassPanel modesCard = CreateGlassCard(0, 132, 1224, 404);

        _btnModeCard.Bounds = R(24, 24, 372, 292);
        _btnModeCard.CornerRadius = S(18);
        _btnModeCard.Variant = KioskButtonVariant.SecondaryGlass;
        _btnModeCard.IsSelectedCard = true;
        _btnModeCard.Click += (_, _) => SelectPaymentCard(PaymentModeType.CreditCard);

        _btnModeBankQr.Bounds = R(426, 24, 372, 292);
        _btnModeBankQr.CornerRadius = S(18);
        _btnModeBankQr.Variant = KioskButtonVariant.SecondaryGlass;
        _btnModeBankQr.Click += (_, _) => SelectPaymentCard(PaymentModeType.BankQR);

        _btnModeWallet.Bounds = R(828, 24, 372, 292);
        _btnModeWallet.CornerRadius = S(18);
        _btnModeWallet.Variant = KioskButtonVariant.SecondaryGlass;
        _btnModeWallet.Click += (_, _) => SelectPaymentCard(PaymentModeType.DigitalWallet);

        _lblWalletPick.Bounds = R(828, 330, 152, 46);
        _lblWalletPick.ForeColor = KioskTheme.DarkNavyText;
        _lblWalletPick.TextAlign = ContentAlignment.MiddleLeft;
        _lblWalletPick.WordWrap = false;

        _cboWalletBrand.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboWalletBrand.Items.AddRange(new object[] { "MoMo", "VNPay", "ZaloPay" });
        _cboWalletBrand.SelectedIndex = 0;
        _cboWalletBrand.Font = FFixed(18f, FontStyle.Bold);
        _cboWalletBrand.Bounds = R(984, 334, 216, 42);
        _cboWalletBrand.SelectedIndexChanged += (_, _) =>
        {
            SelectPaymentCard(PaymentModeType.DigitalWallet);
        };

        modesCard.Controls.AddRange(new Control[] { _btnModeCard, _btnModeBankQr, _btnModeWallet, _lblWalletPick, _cboWalletBrand });

        _btnS03Cancel.Bounds = R(0, 550, 252, 70);
        _btnS03Cancel.CornerRadius = S(16);
        _btnS03Cancel.Variant = KioskButtonVariant.CancelRedOutline;
        _btnS03Cancel.Click += (_, _) => PromptCancelConfirmation("S03");

        _btnS03Back.Bounds = R(270, 550, 290, 70);
        _btnS03Back.CornerRadius = S(16);
        _btnS03Back.Variant = KioskButtonVariant.SecondaryGlass;
        _btnS03Back.Click += (_, _) => NavigateTo("S02");

        _btnS03Pay.Bounds = R(856, 550, 368, 70);
        _btnS03Pay.CornerRadius = S(16);
        _btnS03Pay.Variant = KioskButtonVariant.PrimarySolid;
        _btnS03Pay.Click += async (_, _) => await StartPaymentExecutionAsync();

        s.Controls.AddRange(new Control[] { topSummary, modesCard, _btnS03Cancel, _btnS03Back, _btnS03Pay });
    }

    private void BuildScreenS04a()
    {
        ScreenPanel s = CreateScreenPanel("S04a");
        GlassPanel card = CreateGlassCard(0, 0, 1224, 536);

        _lblS04aTitle.Bounds = R(30, 24, 880, 56);
        _lblS04aTitle.ForeColor = KioskTheme.DarkNavyText;
        _lblS04aTitle.TextAlign = ContentAlignment.MiddleLeft;
        _lblS04aTitle.WordWrap = false;

        _lblS04aTimer.Bounds = R(950, 20, 242, 64);
        _lblS04aTimer.FillColor = KioskTheme.DarkNavyText;
        _lblS04aTimer.CornerRadius = S(16);
        _lblS04aTimer.ForeColor = Color.White;
        _lblS04aTimer.TextAlign = ContentAlignment.MiddleCenter;
        _lblS04aTimer.WordWrap = false;

        _lblS04aPrompt.Bounds = R(30, 104, 720, 138);
        _lblS04aPrompt.ForeColor = KioskTheme.DarkNavyText;

        _lblS04aSummary.Bounds = R(30, 264, 720, 136);
        _lblS04aSummary.FillColor = Color.FromArgb(228, 240, 255);
        _lblS04aSummary.BorderColor = Color.FromArgb(147, 197, 253);
        _lblS04aSummary.CornerRadius = S(16);
        _lblS04aSummary.ForeColor = KioskTheme.DeepBlue;
        _lblS04aSummary.TextAlign = ContentAlignment.MiddleLeft;
        _lblS04aSummary.Padding = new Padding(S(20));

        _lblS04aReaderStatus.Bounds = R(780, 110, 412, 378);
        _lblS04aReaderStatus.FillColor = KioskTheme.DarkNavyText;
        _lblS04aReaderStatus.BorderColor = KioskTheme.Teal;
        _lblS04aReaderStatus.BorderWidth = 2.2f;
        _lblS04aReaderStatus.CornerRadius = S(20);
        _lblS04aReaderStatus.ForeColor = Color.White;
        _lblS04aReaderStatus.TextAlign = ContentAlignment.MiddleCenter;
        _lblS04aReaderStatus.Padding = new Padding(S(18));

        card.Controls.AddRange(new Control[] { _lblS04aTitle, _lblS04aTimer, _lblS04aPrompt, _lblS04aSummary, _lblS04aReaderStatus });

        _btnS04aCancel.Bounds = R(0, 550, 320, 70);
        _btnS04aCancel.CornerRadius = S(16);
        _btnS04aCancel.Variant = KioskButtonVariant.CancelRedOutline;
        _btnS04aCancel.Click += (_, _) => CancelInFlightAndOpenS06("S03");

        s.Controls.AddRange(new Control[] { card, _btnS04aCancel });
    }

    private void BuildScreenS04b()
    {
        ScreenPanel s = CreateScreenPanel("S04b");
        GlassPanel card = CreateGlassCard(0, 0, 1224, 536);

        var qrFrame = new Panel
        {
            Bounds = R(54, 62, 280, 280),
            BackColor = Color.White,
            Padding = new Padding(S(10))
        };

        _picQrCode.Dock = DockStyle.Fill;
        _picQrCode.SizeMode = PictureBoxSizeMode.Zoom;
        _picQrCode.BackColor = Color.White;
        qrFrame.Controls.Add(_picQrCode);

        _lblS04bProvider.Bounds = R(34, 352, 320, 48);
        _lblS04bProvider.ForeColor = KioskTheme.DeepBlue;
        _lblS04bProvider.TextAlign = ContentAlignment.MiddleCenter;
        _lblS04bProvider.WordWrap = false;

        _lblS04bTitle.Bounds = R(386, 24, 548, 56);
        _lblS04bTitle.ForeColor = KioskTheme.DarkNavyText;
        _lblS04bTitle.TextAlign = ContentAlignment.MiddleLeft;
        _lblS04bTitle.WordWrap = false;

        _lblS04bTimer.Bounds = R(950, 20, 242, 64);
        _lblS04bTimer.FillColor = KioskTheme.DarkNavyText;
        _lblS04bTimer.CornerRadius = S(16);
        _lblS04bTimer.ForeColor = Color.White;
        _lblS04bTimer.TextAlign = ContentAlignment.MiddleCenter;
        _lblS04bTimer.WordWrap = false;

        _lblS04bPrompt.Bounds = R(386, 104, 806, 160);
        _lblS04bPrompt.ForeColor = KioskTheme.DarkNavyText;

        _lblS04bSummary.Bounds = R(386, 282, 806, 136);
        _lblS04bSummary.FillColor = Color.FromArgb(228, 240, 255);
        _lblS04bSummary.BorderColor = Color.FromArgb(147, 197, 253);
        _lblS04bSummary.CornerRadius = S(16);
        _lblS04bSummary.ForeColor = KioskTheme.DeepBlue;
        _lblS04bSummary.TextAlign = ContentAlignment.MiddleLeft;
        _lblS04bSummary.Padding = new Padding(S(20));

        card.Controls.AddRange(new Control[] { qrFrame, _lblS04bProvider, _lblS04bTitle, _lblS04bTimer, _lblS04bPrompt, _lblS04bSummary });

        _btnS04bCancel.Bounds = R(0, 550, 320, 70);
        _btnS04bCancel.CornerRadius = S(16);
        _btnS04bCancel.Variant = KioskButtonVariant.CancelRedOutline;
        _btnS04bCancel.Click += (_, _) => CancelInFlightAndOpenS06("S03");

        s.Controls.AddRange(new Control[] { card, _btnS04bCancel });
    }

    private void BuildScreenS05a()
    {
        ScreenPanel s = CreateScreenPanel("S05a");
        GlassPanel leftTicketCard = CreateGlassCard(0, 0, 580, 536);

        _lblS05aBadge.Bounds = R(24, 18, 532, 42);
        _lblS05aBadge.FillColor = KioskTheme.SuccessGreen;
        _lblS05aBadge.CornerRadius = S(21);
        _lblS05aBadge.ForeColor = Color.White;
        _lblS05aBadge.TextAlign = ContentAlignment.MiddleCenter;
        _lblS05aBadge.WordWrap = false;

        _lblS05aTitle.Bounds = R(24, 68, 532, 48);
        _lblS05aTitle.ForeColor = KioskTheme.DarkNavyText;
        _lblS05aTitle.TextAlign = ContentAlignment.MiddleLeft;
        _lblS05aTitle.WordWrap = false;

        var paperBox = new Panel
        {
            Bounds = R(24, 126, 532, 386),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblS05aTicketHeader.Bounds = R(18, 12, 496, 36);
        _lblS05aTicketHeader.ForeColor = KioskTheme.DeepBlue;
        _lblS05aTicketHeader.TextAlign = ContentAlignment.MiddleLeft;
        _lblS05aTicketHeader.WordWrap = false;

        _lblS05aTicketDetails.Bounds = R(18, 54, 496, 202);
        _lblS05aTicketDetails.ForeColor = KioskTheme.DarkNavyText;

        _picBarcode.Bounds = R(46, 264, 440, 96);
        _picBarcode.SizeMode = PictureBoxSizeMode.Zoom;
        _picBarcode.BackColor = Color.White;

        paperBox.Controls.AddRange(new Control[] { _lblS05aTicketHeader, _lblS05aTicketDetails, _picBarcode });
        leftTicketCard.Controls.AddRange(new Control[] { _lblS05aBadge, _lblS05aTitle, paperBox });

        // Right panel: Embedded Joined DB Transaction Output Report (DataGridView)
        GlassPanel rightReportCard = CreateGlassCard(598, 0, 626, 536);
        var lblGridHeader = new KioskLabel
        {
            Text = "▤ Recent DB Transactions (Joined Output Report)",
            Bounds = R(20, 16, 586, 38),
            ForeColor = KioskTheme.DarkNavyText,
            TextAlign = ContentAlignment.MiddleLeft,
            WordWrap = false,
            Font = FFixed(19f, FontStyle.Bold)
        };

        _miniReportGrid.Bounds = R(20, 60, 586, 386);
        _miniReportGrid.ReadOnly = true;
        _miniReportGrid.AllowUserToAddRows = false;
        _miniReportGrid.AutoGenerateColumns = false;
        _miniReportGrid.RowHeadersVisible = false;
        _miniReportGrid.BackgroundColor = Color.White;
        _miniReportGrid.BorderStyle = BorderStyle.FixedSingle;
        _miniReportGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _miniReportGrid.EnableHeadersVisualStyles = false;
        _miniReportGrid.ColumnHeadersHeight = S(36);
        _miniReportGrid.RowTemplate.Height = S(32);
        _miniReportGrid.ColumnHeadersDefaultCellStyle.BackColor = KioskTheme.DeepBlue;
        _miniReportGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _miniReportGrid.ColumnHeadersDefaultCellStyle.Font = FFixed(12.5f, FontStyle.Bold);
        _miniReportGrid.Font = FFixed(12.5f);

        _miniReportGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Station", DataPropertyName = "StationNameEn", Width = S(115) });
        _miniReportGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total", DataPropertyName = "TotalAmountVnd", Width = S(80), DefaultCellStyle = { Format = "N0" } });
        _miniReportGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PayStatus", DataPropertyName = "PaymentStatus", Width = S(82) });
        _miniReportGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TxnStatus", DataPropertyName = "TransactionStatus", Width = S(84) });
        _miniReportGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ticket #", DataPropertyName = "TicketNumbers", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

        _btnS05aOpenFullReport.Bounds = R(20, 456, 586, 64);
        _btnS05aOpenFullReport.CornerRadius = S(14);
        _btnS05aOpenFullReport.Variant = KioskButtonVariant.SecondaryGlass;
        _btnS05aOpenFullReport.Click += (_, _) => OpenFullReportDialog();

        rightReportCard.Controls.AddRange(new Control[] { lblGridHeader, _miniReportGrid, _btnS05aOpenFullReport });

        _btnS05aCancel.Bounds = R(0, 550, 260, 70);
        _btnS05aCancel.CornerRadius = S(16);
        _btnS05aCancel.Variant = KioskButtonVariant.CancelRedOutline;
        _btnS05aCancel.Click += (_, _) => ResetToIdleHome();

        _btnS05aDone.Bounds = R(784, 550, 440, 70);
        _btnS05aDone.CornerRadius = S(16);
        _btnS05aDone.Variant = KioskButtonVariant.SuccessSolid;
        _btnS05aDone.Click += (_, _) => ResetToIdleHome();

        s.Controls.AddRange(new Control[] { leftTicketCard, rightReportCard, _btnS05aCancel, _btnS05aDone });
    }

    private void BuildScreenS05b()
    {
        ScreenPanel s = CreateScreenPanel("S05b");
        GlassPanel card = CreateGlassCard(0, 0, 1224, 536, accentBorder: KioskTheme.ErrorRed);

        _lblS05bTitle.Bounds = R(34, 34, 1156, 60);
        _lblS05bTitle.ForeColor = KioskTheme.ErrorRed;
        _lblS05bTitle.TextAlign = ContentAlignment.MiddleLeft;
        _lblS05bTitle.WordWrap = false;

        _lblS05bDetail.Bounds = R(34, 110, 1156, 130);
        _lblS05bDetail.ForeColor = KioskTheme.DarkNavyText;

        _lblS05bChargeBanner.Bounds = R(34, 264, 1156, 102);
        _lblS05bChargeBanner.FillColor = Color.FromArgb(236, 253, 245);
        _lblS05bChargeBanner.BorderColor = KioskTheme.SuccessGreen;
        _lblS05bChargeBanner.BorderWidth = 2f;
        _lblS05bChargeBanner.CornerRadius = S(16);
        _lblS05bChargeBanner.ForeColor = KioskTheme.SuccessGreen;
        _lblS05bChargeBanner.TextAlign = ContentAlignment.MiddleLeft;
        _lblS05bChargeBanner.Padding = new Padding(S(22), S(8), S(22), S(8));

        card.Controls.AddRange(new Control[] { _lblS05bTitle, _lblS05bDetail, _lblS05bChargeBanner });

        _btnS05bCancel.Bounds = R(0, 550, 300, 70);
        _btnS05bCancel.CornerRadius = S(16);
        _btnS05bCancel.Variant = KioskButtonVariant.CancelRedOutline;
        _btnS05bCancel.Click += (_, _) => ResetToIdleHome();

        _btnS05bRetry.Bounds = R(784, 550, 440, 70);
        _btnS05bRetry.CornerRadius = S(16);
        _btnS05bRetry.Variant = KioskButtonVariant.PrimarySolid;
        _btnS05bRetry.Click += (_, _) => NavigateTo("S03");

        s.Controls.AddRange(new Control[] { card, _btnS05bCancel, _btnS05bRetry });
    }

    private void BuildScreenS06()
    {
        ScreenPanel s = CreateScreenPanel("S06");
        GlassPanel card = CreateGlassCard(0, 0, 1224, 536);

        _lblS06Title.Bounds = R(34, 34, 1156, 60);
        _lblS06Title.ForeColor = KioskTheme.DarkNavyText;
        _lblS06Title.TextAlign = ContentAlignment.MiddleLeft;
        _lblS06Title.WordWrap = false;

        _lblS06Detail.Bounds = R(34, 112, 1156, 118);
        _lblS06Detail.ForeColor = KioskTheme.DarkNavyText;

        _lblS06ChargeBanner.Bounds = R(34, 256, 1156, 102);
        _lblS06ChargeBanner.FillColor = Color.FromArgb(236, 253, 245);
        _lblS06ChargeBanner.BorderColor = KioskTheme.SuccessGreen;
        _lblS06ChargeBanner.BorderWidth = 2f;
        _lblS06ChargeBanner.CornerRadius = S(16);
        _lblS06ChargeBanner.ForeColor = KioskTheme.SuccessGreen;
        _lblS06ChargeBanner.TextAlign = ContentAlignment.MiddleLeft;
        _lblS06ChargeBanner.Padding = new Padding(S(22), S(8), S(22), S(8));

        card.Controls.AddRange(new Control[] { _lblS06Title, _lblS06Detail, _lblS06ChargeBanner });

        _btnS06ConfirmCancel.Bounds = R(0, 550, 380, 70);
        _btnS06ConfirmCancel.CornerRadius = S(16);
        _btnS06ConfirmCancel.Variant = KioskButtonVariant.DangerSolid;
        _btnS06ConfirmCancel.Click += async (_, _) =>
        {
            await _controller.ConfirmUserCancelAsync();
            ResetToIdleHome();
        };

        _btnS06Continue.Bounds = R(824, 550, 400, 70);
        _btnS06Continue.CornerRadius = S(16);
        _btnS06Continue.Variant = KioskButtonVariant.PrimarySolid;
        _btnS06Continue.Click += (_, _) => NavigateTo(_returnScreenBeforeCancel);

        s.Controls.AddRange(new Control[] { card, _btnS06ConfirmCancel, _btnS06Continue });
    }

    private void BuildScreenS07()
    {
        ScreenPanel s = CreateScreenPanel("S07");
        GlassPanel card = CreateGlassCard(0, 0, 1224, 536, accentBorder: KioskTheme.WarningAmber);

        _lblS07Title.Bounds = R(34, 34, 1156, 60);
        _lblS07Title.ForeColor = KioskTheme.WarningAmber;
        _lblS07Title.TextAlign = ContentAlignment.MiddleLeft;
        _lblS07Title.WordWrap = false;

        _lblS07Detail.Bounds = R(34, 110, 1156, 130);
        _lblS07Detail.ForeColor = KioskTheme.DarkNavyText;

        _lblS07ChargeBanner.Bounds = R(34, 264, 1156, 104);
        _lblS07ChargeBanner.FillColor = Color.FromArgb(236, 253, 245);
        _lblS07ChargeBanner.BorderColor = KioskTheme.SuccessGreen;
        _lblS07ChargeBanner.BorderWidth = 2f;
        _lblS07ChargeBanner.CornerRadius = S(16);
        _lblS07ChargeBanner.ForeColor = KioskTheme.SuccessGreen;
        _lblS07ChargeBanner.TextAlign = ContentAlignment.MiddleLeft;
        _lblS07ChargeBanner.Padding = new Padding(S(22), S(8), S(22), S(8));

        card.Controls.AddRange(new Control[] { _lblS07Title, _lblS07Detail, _lblS07ChargeBanner });

        _btnS07Cancel.Bounds = R(0, 550, 320, 70);
        _btnS07Cancel.CornerRadius = S(16);
        _btnS07Cancel.Variant = KioskButtonVariant.CancelRedOutline;
        _btnS07Cancel.Click += (_, _) => ResetToIdleHome();

        _btnS07Retry.Bounds = R(784, 550, 440, 70);
        _btnS07Retry.CornerRadius = S(16);
        _btnS07Retry.Variant = KioskButtonVariant.PrimarySolid;
        _btnS07Retry.Click += (_, _) => NavigateTo("S03");

        s.Controls.AddRange(new Control[] { card, _btnS07Cancel, _btnS07Retry });
    }

    private void BuildScreenS08()
    {
        ScreenPanel s = CreateScreenPanel("S08");
        GlassPanel card = CreateGlassCard(0, 0, 1224, 536, accentBorder: KioskTheme.ErrorRed);

        _lblS08Title.Bounds = R(34, 32, 1156, 60);
        _lblS08Title.ForeColor = KioskTheme.ErrorRed;
        _lblS08Title.TextAlign = ContentAlignment.MiddleLeft;
        _lblS08Title.WordWrap = false;

        _lblS08Reason.Bounds = R(34, 106, 1156, 134);
        _lblS08Reason.ForeColor = KioskTheme.DarkNavyText;

        _lblS08ChargeBanner.Bounds = R(34, 256, 1156, 106);
        _lblS08ChargeBanner.BorderWidth = 2f;
        _lblS08ChargeBanner.CornerRadius = S(16);
        _lblS08ChargeBanner.TextAlign = ContentAlignment.MiddleLeft;
        _lblS08ChargeBanner.Padding = new Padding(S(22), S(8), S(22), S(8));

        _lblS08Support.Bounds = R(34, 386, 1156, 54);
        _lblS08Support.ForeColor = KioskTheme.DarkNavyText;
        _lblS08Support.TextAlign = ContentAlignment.MiddleLeft;
        _lblS08Support.WordWrap = false;

        card.Controls.AddRange(new Control[] { _lblS08Title, _lblS08Reason, _lblS08ChargeBanner, _lblS08Support });

        _btnS08Cancel.Bounds = R(0, 550, 360, 70);
        _btnS08Cancel.CornerRadius = S(16);
        _btnS08Cancel.Variant = KioskButtonVariant.CancelRedOutline;
        _btnS08Cancel.Click += async (_, _) => await InitializeSystemAsync();

        _btnS08DisabledInfo.Bounds = R(744, 550, 480, 70);
        _btnS08DisabledInfo.CornerRadius = S(16);
        _btnS08DisabledInfo.Enabled = false;

        s.Controls.AddRange(new Control[] { card, _btnS08Cancel, _btnS08DisabledInfo });
    }

    private void BuildDemoSimulationBar()
    {
        var simBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = S(52),
            BackColor = Color.FromArgb(15, 23, 42)
        };
        simBar.Paint += (_, e) =>
        {
            using var topBorder = new Pen(Color.FromArgb(51, 65, 85), 1f);
            e.Graphics.DrawLine(topBorder, 0, 0, simBar.Width, 0);
        };

        var lblSim = new Label
        {
            Text = "⚙ SIMULATOR • Gateway:",
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = Color.FromArgb(103, 232, 249),
            Font = FFixed(13f, FontStyle.Bold),
            Location = new Point(S(16), S(17))
        };

        _cboGatewaySim.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboGatewaySim.Items.AddRange(new object[] { "Success", "Declined", "Timeout" });
        _cboGatewaySim.SelectedIndex = 0;
        _cboGatewaySim.Font = FFixed(13f, FontStyle.Bold);
        _cboGatewaySim.Bounds = R(206, 13, 114, 28);
        _cboGatewaySim.SelectedIndexChanged += (_, _) =>
        {
            _controller.MockGateway.SimulationMode = Enum.Parse<GatewaySimulationMode>(_cboGatewaySim.SelectedItem!.ToString()!);
        };

        _chkPrinterFault.Text = "Printer Fault";
        _chkPrinterFault.UseMnemonic = false;
        _chkPrinterFault.AutoSize = true;
        _chkPrinterFault.ForeColor = Color.White;
        _chkPrinterFault.Font = FFixed(13f, FontStyle.Bold);
        _chkPrinterFault.Location = new Point(S(336), S(16));
        _chkPrinterFault.CheckedChanged += (_, _) =>
        {
            _controller.TicketPrinter.SimulateHardwareFault = _chkPrinterFault.Checked;
        };

        var lblDb = new Label
        {
            Text = "SQL Server:",
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = Color.White,
            Font = FFixed(13f, FontStyle.Bold),
            Location = new Point(S(468), S(17))
        };

        _cboDbProfile.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (string key in _appConfig.ConnectionStrings.Keys)
        {
            _cboDbProfile.Items.Add(key);
        }
        _cboDbProfile.SelectedItem = _appConfig.ActiveProfile;
        if (_cboDbProfile.SelectedIndex < 0 && _cboDbProfile.Items.Count > 0) _cboDbProfile.SelectedIndex = 0;
        _cboDbProfile.Font = FFixed(13f);
        _cboDbProfile.Bounds = R(558, 13, 142, 28);

        var btnReconnect = new Button
        {
            Text = "Connect DB",
            UseMnemonic = false,
            Bounds = R(708, 10, 106, 32),
            BackColor = KioskTheme.Teal,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = FFixed(12.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnReconnect.FlatAppearance.BorderSize = 0;
        btnReconnect.Click += async (_, _) =>
        {
            string key = _cboDbProfile.SelectedItem?.ToString() ?? "LocalDb";
            _appConfig.ActiveProfile = key;
            _controller.Repository.ConnectionString = _appConfig.GetActiveConnectionString();
            await InitializeSystemAsync();
        };

        _lblDbStatus.Bounds = R(824, 12, 220, 28);
        _lblDbStatus.UseMnemonic = false;
        _lblDbStatus.ForeColor = Color.LightGreen;
        _lblDbStatus.Font = FFixed(13f, FontStyle.Bold);
        _lblDbStatus.TextAlign = ContentAlignment.MiddleLeft;

        var btnReport = new Button
        {
            Text = "▤ Output Report (DB)",
            UseMnemonic = false,
            Size = new Size(S(204), S(34)),
            BackColor = KioskTheme.DeepBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = FFixed(13f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnReport.FlatAppearance.BorderSize = 1;
        btnReport.FlatAppearance.BorderColor = Color.FromArgb(125, 211, 252);
        btnReport.Click += (_, _) => OpenFullReportDialog();

        simBar.Resize += (_, _) =>
        {
            btnReport.Location = new Point(Math.Max(S(1050), simBar.ClientSize.Width - btnReport.Width - S(16)), S(9));
        };

        simBar.Controls.AddRange(new Control[]
        {
            lblSim, _cboGatewaySim, _chkPrinterFault, lblDb, _cboDbProfile, btnReconnect, _lblDbStatus, btnReport
        });
        Controls.Add(simBar);
    }

    private void ApplyModeTypography()
    {
        // S01
        _lblS01Badge.Font = FFixed(17f, FontStyle.Bold);
        _lblS01Title.Font = FScale(36f, FontStyle.Bold);
        _lblS01Desc.Font = FScale(21f);
        _btnS01Start.Font = FScale(27f, FontStyle.Bold);
        _lblS01FareInfo.Font = FScale(18.5f, FontStyle.Bold);
        _btnS01Cancel.Font = FFixed(18.5f, FontStyle.Bold);

        // S02
        _lblS02Title.Font = FFixed(30f, FontStyle.Bold);
        _lblS02QtyCaption.Font = FFixed(19.5f, FontStyle.Bold);
        _lblS02QtyVal.Font = FFixed(28f, FontStyle.Bold);
        _lblS02Summary.Font = FScale(20.5f, FontStyle.Bold);
        _btnS02Cancel.Font = FFixed(18.5f, FontStyle.Bold);
        _btnS02Next.Font = FFixed(19.5f, FontStyle.Bold);

        // S03
        _lblS03Title.Font = FFixed(30f, FontStyle.Bold);
        _lblS03RouteInfo.Font = FScale(19.5f);
        _lblS03Total.Font = FFixed(34f, FontStyle.Bold);
        _btnModeCard.Font = FScale(19.5f, FontStyle.Bold);
        _btnModeBankQr.Font = FScale(19.5f, FontStyle.Bold);
        _btnModeWallet.Font = FScale(19.5f, FontStyle.Bold);
        _lblWalletPick.Font = FFixed(17.5f, FontStyle.Bold);
        _btnS03Cancel.Font = FFixed(18.5f, FontStyle.Bold);
        _btnS03Back.Font = FFixed(18.5f, FontStyle.Bold);
        _btnS03Pay.Font = FFixed(19.5f, FontStyle.Bold);

        // S04a & S04b
        _lblS04aTitle.Font = FFixed(32f, FontStyle.Bold);
        _lblS04aTimer.Font = FFixed(26f, FontStyle.Bold);
        _lblS04aPrompt.Font = FScale(21f);
        _lblS04aSummary.Font = FScale(21.5f, FontStyle.Bold);
        _lblS04aReaderStatus.Font = FScale(22.5f, FontStyle.Bold);
        _btnS04aCancel.Font = FFixed(18.5f, FontStyle.Bold);

        _lblS04bTitle.Font = FFixed(32f, FontStyle.Bold);
        _lblS04bTimer.Font = FFixed(26f, FontStyle.Bold);
        _lblS04bPrompt.Font = FScale(20.5f);
        _lblS04bSummary.Font = FScale(21.5f, FontStyle.Bold);
        _lblS04bProvider.Font = FFixed(19.5f, FontStyle.Bold);
        _btnS04bCancel.Font = FFixed(18.5f, FontStyle.Bold);

        // S05a
        _lblS05aBadge.Font = FFixed(17.5f, FontStyle.Bold);
        _lblS05aTitle.Font = FFixed(25f, FontStyle.Bold);
        _lblS05aTicketHeader.Font = FFixed(18f, FontStyle.Bold);
        _lblS05aTicketDetails.Font = FFixed(16.5f);
        _btnS05aOpenFullReport.Font = FFixed(17.5f, FontStyle.Bold);
        _btnS05aCancel.Font = FFixed(18.5f, FontStyle.Bold);
        _btnS05aDone.Font = FFixed(19.5f, FontStyle.Bold);

        // S05b, S06, S07, S08
        _lblS05bTitle.Font = FFixed(32f, FontStyle.Bold);
        _lblS05bDetail.Font = FScale(21.5f);
        _lblS05bChargeBanner.Font = FFixed(20.5f, FontStyle.Bold);
        _btnS05bCancel.Font = FFixed(18.5f, FontStyle.Bold);
        _btnS05bRetry.Font = FFixed(19.5f, FontStyle.Bold);

        _lblS06Title.Font = FFixed(32f, FontStyle.Bold);
        _lblS06Detail.Font = FScale(21.5f);
        _lblS06ChargeBanner.Font = FFixed(20.5f, FontStyle.Bold);
        _btnS06ConfirmCancel.Font = FFixed(19f, FontStyle.Bold);
        _btnS06Continue.Font = FFixed(19.5f, FontStyle.Bold);

        _lblS07Title.Font = FFixed(32f, FontStyle.Bold);
        _lblS07Detail.Font = FScale(21.5f);
        _lblS07ChargeBanner.Font = FFixed(20f, FontStyle.Bold);
        _btnS07Cancel.Font = FFixed(18.5f, FontStyle.Bold);
        _btnS07Retry.Font = FFixed(19.5f, FontStyle.Bold);

        _lblS08Title.Font = FFixed(32f, FontStyle.Bold);
        _lblS08Reason.Font = FScale(20.5f);
        _lblS08ChargeBanner.Font = FFixed(19.5f, FontStyle.Bold);
        _lblS08Support.Font = FFixed(18.5f, FontStyle.Bold);
        _btnS08Cancel.Font = FFixed(18.5f, FontStyle.Bold);
        _btnS08DisabledInfo.Font = FFixed(19f, FontStyle.Bold);
    }

    private async Task InitializeSystemAsync()
    {
        _lblDbStatus.Text = "Checking DB...";
        _lblDbStatus.ForeColor = Color.Khaki;

        var (ok, err) = await _controller.InitializeOrRefreshAsync();
        if (!ok)
        {
            _lblDbStatus.Text = "● DB OFFLINE";
            _lblDbStatus.ForeColor = Color.Salmon;
            ShowOutOfService(err ?? "SQL Server unavailable.", wasCharged: false);
            return;
        }

        _lblDbStatus.Text = $"● DB OK ({_controller.CachedDestinations.Count} stn)";
        _lblDbStatus.ForeColor = Color.LightGreen;
        _selectedDestination = _controller.CachedDestinations.LastOrDefault();
        _selectedQuantity = 1;
        PopulateStationButtons();
        NavigateTo("S01");
    }

    private void PopulateStationButtons()
    {
        _stationGrid.SuspendLayout();
        _stationGrid.Controls.Clear();
        _stationButtons.Clear();

        for (int i = 0; i < _controller.CachedDestinations.Count && i < 12; i++)
        {
            Destination dest = _controller.CachedDestinations[i];
            var btn = new KioskButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(S(6)),
                CornerRadius = S(14),
                Variant = KioskButtonVariant.SecondaryGlass,
                Tag = dest,
                IsSelectedCard = _selectedDestination?.DestinationId == dest.DestinationId,
                Font = FScale(16.5f, FontStyle.Bold)
            };
            string modeTag = dest.LineType == "BUS" ? "[BUS]" : "[MRT]";
            btn.Text = $"{modeTag} {dest.GetDisplayName(_isVietnamese)}\n{dest.UnitFareVnd:N0} VND";
            btn.Click += (_, _) =>
            {
                _selectedDestination = dest;
                foreach (KioskButton b in _stationButtons)
                {
                    b.IsSelectedCard = ReferenceEquals(b.Tag, dest);
                }
                UpdateOrderDisplays();
            };

            _stationButtons.Add(btn);
            _stationGrid.Controls.Add(btn, i % 4, i / 4);
        }

        _stationGrid.ResumeLayout();
        UpdateOrderDisplays();
    }

    private void AdjustQuantity(int delta)
    {
        _selectedQuantity = Math.Clamp(_selectedQuantity + delta, Fare.MinQuantity, Fare.MaxQuantity);
        UpdateOrderDisplays();
    }

    private void UpdateOrderDisplays()
    {
        _lblS02QtyVal.Text = _selectedQuantity.ToString();
        if (_selectedDestination == null) return;

        var fare = new Fare(_selectedDestination, _selectedQuantity);
        string stName = _selectedDestination.GetDisplayName(_isVietnamese);
        string origin = _isVietnamese ? "Bến Thành" : "Ben Thanh";

        _lblS02Summary.Text = $"{stName} × {_selectedQuantity}  =  {Fare.FormatVnd(fare.TotalAmountVnd)}";
        _lblS03RouteInfo.Text = _isVietnamese
            ? $"Hành trình: {origin} → {stName}  •  Số lượng: {_selectedQuantity} vé"
            : $"Route: {origin} → {stName}  •  Quantity: {_selectedQuantity} ticket(s)";
        _lblS03Total.Text = Fare.FormatVnd(fare.TotalAmountVnd);
    }

    private void SelectPaymentCard(PaymentModeType mode)
    {
        string wallet = _cboWalletBrand.SelectedItem?.ToString() ?? "MoMo";
        _controller.SelectPaymentMode(mode, wallet);

        _btnModeCard.IsSelectedCard = mode == PaymentModeType.CreditCard;
        _btnModeBankQr.IsSelectedCard = mode == PaymentModeType.BankQR;
        _btnModeWallet.IsSelectedCard = mode == PaymentModeType.DigitalWallet;
    }

    private async Task StartPaymentExecutionAsync()
    {
        _btnS03Pay.Enabled = false;
        var (ok, err) = await _controller.BeginPaymentStepAsync();
        _btnS03Pay.Enabled = true;

        if (!ok || _controller.ActiveTransaction == null || _controller.ActivePayment == null)
        {
            ShowOutOfService(err ?? "Failed to create transaction.", wasCharged: false);
            return;
        }

        Transaction txn = _controller.ActiveTransaction;
        Payment payment = _controller.ActivePayment;
        string stName = txn.Destination?.GetDisplayName(_isVietnamese) ?? "";

        if (txn.PaymentMode == PaymentModeType.CreditCard)
        {
            _remainingSeconds = 45;
            _lblS04aTimer.Text = $"⏱ 00:{_remainingSeconds:00}";
            _lblS04aSummary.Text = _isVietnamese
                ? $"Mã GD: {txn.TransactionCode}\nGa đến: {stName} (×{txn.Quantity})  •  Tổng thu: {txn.TotalAmountVnd:N0} VND"
                : $"Ref: {txn.TransactionCode}\nDestination: {stName} (×{txn.Quantity})  •  Amount: {txn.TotalAmountVnd:N0} VND";
            _lblS04aReaderStatus.Text = _isVietnamese
                ? "💳\n\nĐẦU ĐỌC EMV / NFC\nĐANG CHỜ CHẠM THẺ..."
                : "💳\n\nEMV / NFC READER\nAWAITING CARD TAP...";
            NavigateTo("S04a");
        }
        else
        {
            // FR13 (Member M3 QRPayment.ExpiresAt): QR expires after 120 seconds (02:00)
            _remainingSeconds = SmartTicketing.TVM.QRPayment.QrValiditySeconds;
            _lblS04bTimer.Text = "⏱ 02:00";
            string payload = payment.BuildPaymentInstruction(txn.TransactionCode);
            _picQrCode.Image?.Dispose();
            _picQrCode.Image = TicketPrinter.RenderQrBitmap(payload, 240);
            _lblS04bProvider.Text = payment.ProviderName;
            _lblS04bSummary.Text = _isVietnamese
                ? $"Mã GD: {txn.TransactionCode}\nGa đến: {stName} (×{txn.Quantity})  •  Tổng thu: {txn.TotalAmountVnd:N0} VND"
                : $"Ref: {txn.TransactionCode}\nDestination: {stName} (×{txn.Quantity})  •  Total: {txn.TotalAmountVnd:N0} VND";
            NavigateTo("S04b");
        }

        _cancelRequestedDuringPayment = false;
        _paymentCts?.Cancel();
        _paymentCts?.Dispose();
        _paymentCts = new CancellationTokenSource();
        _countdownTimer.Start();

        PurchaseFlowResult result = await _controller.ExecutePaymentAndPrintAsync(_paymentCts.Token);
        _countdownTimer.Stop();

        if (_cancelRequestedDuringPayment)
        {
            return; // Screen S06 is already active
        }

        await HandlePurchaseFlowResultAsync(result);
    }

    private async void OnCountdownTickAsync(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        string formatted = $"⏱ {_remainingSeconds / 60:00}:{Math.Max(0, _remainingSeconds % 60):00}";
        _lblS04aTimer.Text = formatted;
        _lblS04bTimer.Text = formatted;

        if (_remainingSeconds <= 0)
        {
            _countdownTimer.Stop();
            _cancelRequestedDuringPayment = true;
            _paymentCts?.Cancel();
            await _controller.HandleSessionCountdownExpiredAsync();
            NavigateTo("S07");
        }
    }

    private void CancelInFlightAndOpenS06(string fallbackContinueScreen)
    {
        _countdownTimer.Stop();
        _cancelRequestedDuringPayment = true;
        _paymentCts?.Cancel();
        PromptCancelConfirmation(fallbackContinueScreen);
    }

    private async Task HandlePurchaseFlowResultAsync(PurchaseFlowResult res)
    {
        switch (res.Outcome)
        {
            case PurchaseFlowOutcome.TicketIssued_S05a:
                if (res.Transaction != null && res.Transaction.Tickets.Count > 0)
                {
                    Ticket first = res.Transaction.Tickets[0];
                    string stName = res.Transaction.Destination?.GetDisplayName(_isVietnamese) ?? "";
                    string modeLabel = res.Transaction.Destination?.LineType == "BUS" ? "HCMC SMART BUS" : "HCMC METRO LINE 1";
                    _lblS05aTicketHeader.Text = $"{modeLabel}   •   #{first.TicketNumber}";
                    _lblS05aTicketDetails.Text =
                        $"Origin: Ben Thanh  →  Destination: {stName}\n" +
                        $"Quantity: {res.Transaction.Quantity} ticket(s)   •   Paid: {res.Transaction.TotalAmountVnd:N0} VND\n" +
                        $"Payment: {res.Transaction.Payment?.ProviderName} ({res.Transaction.Payment?.GatewayReference})\n" +
                        $"Issued: {first.IssuedAt:yyyy-MM-dd HH:mm:ss}   •   Valid 24h\n" +
                        $"All Ticket(s): {string.Join(", ", res.Transaction.Tickets.Select(t => t.TicketNumber))}";

                    _picBarcode.Image?.Dispose();
                    _picBarcode.Image = TicketPrinter.RenderBarcodeBitmap(first.BarcodeValue, 420, 88);
                }

                await RefreshMiniReportAsync();
                NavigateTo("S05a");
                break;

            case PurchaseFlowOutcome.PaymentDeclined_S05b:
                NavigateTo("S05b");
                break;

            case PurchaseFlowOutcome.Timeout_S07:
                NavigateTo("S07");
                break;

            case PurchaseFlowOutcome.OutOfService_S08:
                ShowOutOfService(_isVietnamese ? res.MessageVi : res.MessageEn, res.WasPassengerCharged);
                break;

            default:
                ResetToIdleHome();
                break;
        }
    }

    private async Task RefreshMiniReportAsync()
    {
        try
        {
            List<TransactionReportRow> rows = await _controller.LoadReportRowsAsync(12);
            _miniReportGrid.DataSource = rows;
        }
        catch
        {
            // Keep existing grid if refresh fails
        }
    }

    private void PromptCancelConfirmation(string returnScreen)
    {
        _returnScreenBeforeCancel = returnScreen;
        NavigateTo("S06");
    }

    private void ShowOutOfService(string diagnosticReason, bool wasCharged)
    {
        _lastS08WasCharged = wasCharged;
        _lblS08Reason.Text = diagnosticReason;
        UpdateS08ChargeBanner();
        NavigateTo("S08");
    }

    private void UpdateS08ChargeBanner()
    {
        if (_lastS08WasCharged)
        {
            _lblS08ChargeBanner.FillColor = Color.FromArgb(254, 242, 242);
            _lblS08ChargeBanner.BorderColor = KioskTheme.ErrorRed;
            _lblS08ChargeBanner.ForeColor = KioskTheme.ErrorRed;
            _lblS08ChargeBanner.Text = _isVietnamese
                ? "⚠ TRẠNG THÁI TRỪ TIỀN: ĐÃ THU TIỀN NHƯNG LỖI IN VÉ — Đã xếp hàng hoàn tiền tự động (hoặc nhận vé tại Quầy #2)."
                : "⚠ CHARGE STATUS: PAYMENT CAPTURED BEFORE PRINTER FAULT — Auto-refund queued (or claim ticket at Counter #2).";
        }
        else
        {
            _lblS08ChargeBanner.FillColor = Color.FromArgb(236, 253, 245);
            _lblS08ChargeBanner.BorderColor = KioskTheme.SuccessGreen;
            _lblS08ChargeBanner.ForeColor = KioskTheme.SuccessGreen;
            _lblS08ChargeBanner.Text = _isVietnamese
                ? "✔ TRẠNG THÁI TRỪ TIỀN: CHƯA BỊ TRỪ TIỀN — Máy khóa trước khi thu tiền (0 VND)."
                : "✔ CHARGE STATUS: NOT CHARGED — Kiosk locked prior to payment capture (0 VND).";
        }
    }

    private void ResetToIdleHome()
    {
        _countdownTimer.Stop();
        _paymentCts?.Cancel();
        _selectedQuantity = 1;
        UpdateOrderDisplays();
        NavigateTo("S01");
    }

    /// <summary>
    /// Navigates to the specified screen identifier (<c>S01</c> through <c>S08</c>) and updates the 4-step progress bar.
    /// </summary>
    public void NavigateTo(string screenId)
    {
        _activeScreenId = screenId;
        foreach ((string id, ScreenPanel panel) in _screens)
        {
            panel.Visible = id == screenId;
        }

        _progressBar.ActiveStep = screenId switch
        {
            "S02" => 1,
            "S03" => 2,
            "S04a" or "S04b" or "S05b" or "S07" => 3,
            "S05a" => 4,
            "S06" => 2,
            _ => 0
        };
        _progressBar.Invalidate();
    }

    private void OpenFullReportDialog()
    {
        using var dlg = new TransactionReportForm(_controller, _isVietnamese);
        dlg.ShowDialog(this);
    }

    private void ApplyAccessibilityMode()
    {
        _btnA11y.IsSelectedCard = _isAccessibilityMode;
        _kioskViewport.HighContrastMode = _isAccessibilityMode;
        _headerBar.HighContrastMode = _isAccessibilityMode;
        _progressBar.HighContrastMode = _isAccessibilityMode;

        foreach (ScreenPanel p in _screens.Values)
        {
            foreach (Control c in p.Controls)
            {
                if (c is GlassPanel gp)
                {
                    gp.HighContrastMode = _isAccessibilityMode;
                }
            }
        }

        ApplyModeTypography();
        PopulateStationButtons();
        _kioskViewport.Invalidate(true);
    }

    private void ApplyLocalization()
    {
        _progressBar.IsVietnamese = _isVietnamese;
        _progressBar.Invalidate();

        _lblHeaderTitle.Text = _isVietnamese
            ? "Giao thông Thông minh TP.HCM • Tuyến Metro Số 1"
            : "HCMC Smart Transit • Metro Line 1";
        _lblHeaderSub.Text = _isVietnamese
            ? "Ga đi: Bến Thành • Máy bán vé tự động TVM-BT-01"
            : "Origin: Ben Thanh Station • Kiosk TVM-BT-01";
        _btnA11y.Text = _isVietnamese ? "♿ Chữ lớn / Tương phản" : "♿ Large / High Contrast";

        // S01
        _lblS01Badge.Text = _isVietnamese ? "● ĐANG HOẠT ĐỘNG — SẴN SÀNG PHỤC VỤ" : "● IN SERVICE — PAPER & PAYMENT READY";
        _lblS01Title.Text = _isVietnamese ? "Xin chào! Mua vé tàu Metro & Xe buýt tại đây" : "Welcome! Buy Metro & Bus Tickets Here";
        _lblS01Desc.Text = _isVietnamese
            ? "Mua vé nhanh từ Ga Bến Thành. Thanh toán bằng Thẻ EMV/NFC, Mã VietQR Ngân hàng, hoặc Ví MoMo / VNPay / ZaloPay."
            : "Fast self-service ticketing from Ben Thanh Station. Pay via EMV/NFC Card, Bank VietQR, or MoMo / VNPay / ZaloPay.";
        _btnS01Start.Text = _isVietnamese ? "▶ CHẠM ĐỂ BẮT ĐẦU" : "▶ TOUCH TO START";
        _lblS01FareInfo.Text = _isVietnamese
            ? "Giá vé lượt: 8.000đ – 20.000đ • Vé giấy in mã vạch 1D"
            : "Single-Journey Fares: 8,000 – 20,000 VND • Printed Barcode Paper Ticket";
        _btnS01Cancel.Text = _isVietnamese ? "✕ Hủy / Đặt lại" : "✕ Cancel / Reset";

        // S02
        _lblS02Title.Text = _isVietnamese ? "Chọn Ga đến & Số lượng vé" : "Select Destination & Ticket Quantity";
        _lblS02QtyCaption.Text = _isVietnamese ? "Số vé (1–5):" : "Tickets (1–5):";
        _btnS02Cancel.Text = _isVietnamese ? "✕ Hủy giao dịch" : "✕ Cancel";
        _btnS02Next.Text = _isVietnamese ? "Chọn thanh toán →" : "Select Payment →";

        // S03
        _lblS03Title.Text = _isVietnamese ? "Chọn Phương thức Thanh toán" : "Choose Payment Method";
        _lblWalletPick.Text = _isVietnamese ? "Chọn Ví:" : "Wallet App:";
        _btnModeCard.Text = _isVietnamese
            ? "💳\nThẻ Tín dụng / Ghi nợ\n\nChạm NFC hoặc cắm chip EMV\n(Visa, Mastercard, Napas)"
            : "💳\nCredit / Debit Card\n\nTap NFC or insert EMV chip\n(Visa, Mastercard, Napas)";
        _btnModeBankQr.Text = _isVietnamese
            ? "🏦\nMã QR Ngân hàng\n\nQuét mã VietQR Napas 24/7\nbằng ứng dụng ngân hàng"
            : "🏦\nBank QR (VietQR)\n\nScan dynamic Napas 24/7 QR\nwith any banking app";
        _btnModeWallet.Text = _isVietnamese
            ? "📱\nVí Điện tử\n\nQuét mã QR bằng ví\nMoMo • VNPay • ZaloPay"
            : "📱\nDigital Wallet\n\nScan QR with mobile wallet\nMoMo • VNPay • ZaloPay";
        _btnS03Cancel.Text = _isVietnamese ? "✕ Hủy giao dịch" : "✕ Cancel";
        _btnS03Back.Text = _isVietnamese ? "← Đổi ga đến" : "← Back to Stations";
        _btnS03Pay.Text = _isVietnamese ? "Tiến hành thanh toán →" : "Proceed to Pay →";

        // S04a
        _lblS04aTitle.Text = _isVietnamese ? "Chạm hoặc Cắm Thẻ Ngân hàng" : "Tap or Insert Your Bank Card";
        _lblS04aPrompt.Text = _isVietnamese
            ? "Đưa thẻ không tiếp xúc lại gần đầu đọc phía dưới màn hình, hoặc cắm thẻ chip EMV.\nKhông rút thẻ cho đến khi có thông báo xác nhận."
            : "Hold your contactless card near the illuminated reader below, or insert chip-first.\nPlease wait while PaymentService authorizes your transaction.";
        _btnS04aCancel.Text = _isVietnamese ? "✕ Hủy thanh toán" : "✕ Cancel Payment";

        // S04b
        _lblS04bTitle.Text = _isVietnamese ? "Quét Mã QR để Thanh toán" : "Scan QR Code to Pay";
        _lblS04bPrompt.Text = _isVietnamese
            ? "1. Mở ứng dụng Ngân hàng hoặc Ví MoMo / VNPay / ZaloPay trên điện thoại.\n2. Quét mã QR bên trái.\n3. Vé giấy in mã vạch sẽ tự động in ngay khi nhận callback."
            : "1. Open your Banking app or MoMo / VNPay / ZaloPay wallet.\n2. Scan the dynamic QR code on the left.\n3. Your barcode paper ticket prints automatically upon callback.";
        _btnS04bCancel.Text = _isVietnamese ? "✕ Hủy thanh toán" : "✕ Cancel Payment";

        // S05a
        _lblS05aBadge.Text = _isVietnamese ? "✓ THANH TOÁN THÀNH CÔNG & ĐÃ IN VÉ" : "✓ PAYMENT APPROVED & TICKET PRINTED";
        _lblS05aTitle.Text = _isVietnamese ? "Vui lòng Nhận vé tại Khe trả vé" : "Please Collect Your Paper Ticket";
        _btnS05aOpenFullReport.Text = _isVietnamese ? "▤ Mở Báo cáo Giao dịch Đầy đủ (DataGridView)" : "▤ Open Full DB Transaction Report (DataGridView)";
        _btnS05aCancel.Text = _isVietnamese ? "✕ Đóng / Thoát" : "✕ Close / Cancel";
        _btnS05aDone.Text = _isVietnamese ? "✓ Hoàn tất – Khách tiếp theo" : "✓ Done – Next Passenger";

        // S05b
        _lblS05bTitle.Text = _isVietnamese ? "Thanh toán Bị Từ chối (Payment Declined)" : "Payment Declined by Issuer";
        _lblS05bDetail.Text = _isVietnamese
            ? "Ngân hàng phát hành hoặc ví điện tử đã từ chối yêu cầu thanh toán (không đủ số dư hoặc vượt hạn mức).\nBạn có thể chọn phương thức thanh toán khác hoặc hủy giao dịch."
            : "The card issuer or digital wallet rejected this authorization (insufficient balance or card limit).\nYou may retry with another payment method or cancel.";
        _lblS05bChargeBanner.Text = _isVietnamese
            ? "✔ TRẠNG THÁI TRỪ TIỀN: CHƯA BỊ TRỪ TIỀN — Tài khoản/thẻ của bạn không bị thu phí (0 VND)."
            : "✔ CHARGE STATUS: NOT CHARGED — 0 VND was deducted from your card or wallet.";
        _btnS05bCancel.Text = _isVietnamese ? "✕ Hủy & Thoát" : "✕ Cancel & Exit";
        _btnS05bRetry.Text = _isVietnamese ? "↻ Chọn Phương thức Khác" : "↻ Try Another Payment Method";

        // S06
        _lblS06Title.Text = _isVietnamese ? "Xác nhận Hủy Giao dịch Mua vé?" : "Cancel Current Ticket Purchase?";
        _lblS06Detail.Text = _isVietnamese
            ? "Ga đến đã chọn và phiên thanh toán đang chờ sẽ bị hủy ngay lập tức."
            : "Your selected destination and pending payment session will be cancelled immediately.";
        _lblS06ChargeBanner.Text = _isVietnamese
            ? "✔ TRẠNG THÁI TRỪ TIỀN: CHƯA BỊ TRỪ TIỀN — Hủy giao dịch lúc này KHÔNG trừ tiền của bạn."
            : "✔ CHARGE STATUS: NOT CHARGED — Cancelling now will NOT bill your card or wallet.";
        _btnS06ConfirmCancel.Text = _isVietnamese ? "✕ Đồng ý Hủy giao dịch" : "✕ Yes, Cancel Purchase";
        _btnS06Continue.Text = _isVietnamese ? "← Tiếp tục Mua vé" : "← Continue Buying Ticket";

        // S07
        _lblS07Title.Text = _isVietnamese ? "Hết Thời gian Chờ Thanh toán (Timeout)" : "Payment Session Timed Out";
        _lblS07Detail.Text = _isVietnamese
            ? "Hệ thống không nhận được xác nhận thanh toán trong thời gian quy định. Vui lòng không quét lại mã QR cũ."
            : "No confirmation arrived from the card reader or payment gateway within the countdown window. Do not scan expired QR codes.";
        _lblS07ChargeBanner.Text = _isVietnamese
            ? "✔ TRẠNG THÁI TRỪ TIỀN: CHƯA BỊ TRỪ TIỀN — Nếu ngân hàng trừ tiền muộn, hệ thống tự động hoàn tiền trong 24h."
            : "✔ CHARGE STATUS: NOT CHARGED — Session invalidated. Any late bank transfer is auto-refunded within 24h.";
        _btnS07Cancel.Text = _isVietnamese ? "✕ Hủy & Về Trang chủ" : "✕ Cancel & Return Home";
        _btnS07Retry.Text = _isVietnamese ? "↻ Thử lại (Tạo phiên mới)" : "↻ Retry Payment (New Timer)";

        // S08
        _lblS08Title.Text = _isVietnamese ? "⚠ Máy Bán Vé Tạm Ngưng Phục Vụ" : "⚠ Machine Temporarily Out of Service";
        _lblS08Support.Text = _isVietnamese
            ? "Đường dây nóng Nhà ga: 1900-638761  •  Quầy hỗ trợ #2 (cách 15m bên phải)  •  Mã máy: TVM-BT-01"
            : "Station Support Hotline: 1900-638761  •  Counter Window #2 (15m right)  •  Kiosk ID: TVM-BT-01";
        _btnS08Cancel.Text = _isVietnamese ? "✕ Hủy / Kiểm tra lại kết nối" : "✕ Cancel / Re-check System";
        _btnS08DisabledInfo.Text = _isVietnamese ? "Tạm khóa bán vé" : "Ticket Sales Locked";
        UpdateS08ChargeBanner();

        PopulateStationButtons();
    }
}
