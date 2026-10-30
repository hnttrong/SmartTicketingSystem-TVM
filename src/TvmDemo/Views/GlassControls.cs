using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TvmDemo.Views;

/// <summary>
/// Centralized UI design tokens matching the Figma / HTML glassmorphism specification (Task 1).
/// Uses <see cref="GraphicsUnit.Pixel"/> fonts so typography scales identically with pixel bounds at any Windows DPI.
/// </summary>
public static class KioskTheme
{
    public static readonly Color DeepBlue = Color.FromArgb(11, 61, 145);        // #0B3D91
    public static readonly Color MidCyan = Color.FromArgb(18, 111, 163);        // #126FA3
    public static readonly Color Teal = Color.FromArgb(26, 166, 183);           // #1AA6B7
    public static readonly Color HeaderNavyStart = Color.FromArgb(8, 32, 72);   // #082048
    public static readonly Color HeaderNavyEnd = Color.FromArgb(13, 53, 108);   // #0D356C
    public static readonly Color DarkNavyText = Color.FromArgb(11, 42, 92);     // #0B2A5C
    public static readonly Color SuccessGreen = Color.FromArgb(27, 127, 59);    // #1B7F3B
    public static readonly Color ErrorRed = Color.FromArgb(179, 38, 30);        // #B3261E
    public static readonly Color WarningAmber = Color.FromArgb(154, 91, 0);     // #9A5B00

    public static Font PxFont(float pixelSize, FontStyle style = FontStyle.Regular)
        => new("Segoe UI", Math.Max(9f, pixelSize), style, GraphicsUnit.Pixel);

    public static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        int d = Math.Min(Math.Max(2, radius * 2), Math.Min(bounds.Width, bounds.Height));
        if (d <= 2)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Implemented by custom kiosk surfaces capable of painting an exact translated background slice
/// into child controls, eliminating WinForms <c>Color.Transparent</c> corner/buffer artifacts.
/// </summary>
public interface IKioskSurface
{
    void PaintSurfaceForChild(Graphics childGraphics, Point offsetInSurface);
}

/// <summary>
/// Marker interface for layout wrapper containers that delegate background painting to their ancestor <see cref="IKioskSurface"/>.
/// </summary>
public interface IPassThroughContainer
{
}

/// <summary>
/// Deterministic background compositor for nested WinForms controls.
/// </summary>
public static class KioskSurfaceHelper
{
    public static void PaintParentBackground(Graphics g, Control child)
    {
        Point offset = child.Location;
        Control? p = child.Parent;

        while (p != null)
        {
            if (p is IKioskSurface surface)
            {
                surface.PaintSurfaceForChild(g, offset);
                return;
            }

            if (p is IPassThroughContainer)
            {
                offset.Offset(p.Left, p.Top);
                p = p.Parent;
                continue;
            }

            if (p.BackColor.A == 255)
            {
                g.Clear(p.BackColor);
                return;
            }

            offset.Offset(p.Left, p.Top);
            p = p.Parent;
        }

        g.Clear(KioskTheme.DeepBlue);
    }
}

/// <summary>
/// Double-buffered root canvas rendering the deep-blue-to-teal gradient and ambient station bokeh orbs.
/// </summary>
public class GradientBackdropPanel : Panel, IKioskSurface
{
    private bool _highContrastMode;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HighContrastMode
    {
        get => _highContrastMode;
        set
        {
            if (_highContrastMode == value) return;
            _highContrastMode = value;
            Invalidate(true);
        }
    }

    public GradientBackdropPanel()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    public void PaintSurfaceForChild(Graphics childGraphics, Point offsetInSurface)
    {
        GraphicsState state = childGraphics.Save();
        childGraphics.TranslateTransform(-offsetInSurface.X, -offsetInSurface.Y);
        RenderBackdrop(childGraphics);
        childGraphics.Restore(state);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        RenderBackdrop(e.Graphics);
    }

    private void RenderBackdrop(Graphics g)
    {
        Rectangle rect = new(0, 0, Math.Max(1, Width), Math.Max(1, Height));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_highContrastMode)
        {
            g.Clear(Color.FromArgb(6, 26, 58));
            return;
        }

        using var brush = new LinearGradientBrush(rect, KioskTheme.DeepBlue, KioskTheme.Teal, 35f);
        g.FillRectangle(brush, rect);

        // Soft ambient bokeh circles evoking a modern metro concourse
        int orbLarge = Math.Max(220, (int)(rect.Width * 0.38f));
        int orbMid = Math.Max(180, (int)(rect.Width * 0.27f));
        using var orb1 = new SolidBrush(Color.FromArgb(36, 125, 225, 255));
        using var orb2 = new SolidBrush(Color.FromArgb(28, 255, 255, 255));

        g.FillEllipse(orb1, rect.Width - (int)(orbLarge * 0.82f), -orbLarge / 5, orbLarge, orbLarge);
        g.FillEllipse(orb1, rect.Width / 32, rect.Height - (int)(orbLarge * 0.72f), (int)(orbLarge * 0.88f), (int)(orbLarge * 0.88f));
        g.FillEllipse(orb2, rect.Width / 2 - orbMid / 2, rect.Height / 4, orbMid, orbMid);
    }
}

/// <summary>
/// Persistent top utility header bar with clean gradient rendering and zero child transparency artifacts.
/// </summary>
public class HeaderBarPanel : Panel, IKioskSurface
{
    private bool _highContrastMode;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HighContrastMode
    {
        get => _highContrastMode;
        set
        {
            if (_highContrastMode == value) return;
            _highContrastMode = value;
            Invalidate(true);
        }
    }

    public HeaderBarPanel()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    public void PaintSurfaceForChild(Graphics childGraphics, Point offsetInSurface)
    {
        GraphicsState state = childGraphics.Save();
        childGraphics.TranslateTransform(-offsetInSurface.X, -offsetInSurface.Y);
        RenderHeader(childGraphics);
        childGraphics.Restore(state);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        RenderHeader(e.Graphics);
    }

    private void RenderHeader(Graphics g)
    {
        Rectangle rect = new(0, 0, Math.Max(1, Width), Math.Max(1, Height));
        if (_highContrastMode)
        {
            g.Clear(Color.FromArgb(4, 18, 42));
        }
        else
        {
            using var brush = new LinearGradientBrush(rect, KioskTheme.HeaderNavyStart, KioskTheme.HeaderNavyEnd, 0f);
            g.FillRectangle(brush, rect);
        }

        using var borderPen = new Pen(Color.FromArgb(_highContrastMode ? 200 : 95, 165, 220, 255), 1.5f);
        g.DrawLine(borderPen, 0, rect.Height - 1, rect.Width, rect.Height - 1);
    }
}

/// <summary>
/// Flicker-free layout host panel that delegates background painting directly to the ancestor kiosk surface.
/// </summary>
public class ScreenPanel : Panel, IPassThroughContainer
{
    public ScreenPanel()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        KioskSurfaceHelper.PaintParentBackground(e.Graphics, this);
    }
}

/// <summary>
/// Double-buffered <see cref="TableLayoutPanel"/> that delegates background painting cleanly to its parent <see cref="GlassPanel"/>.
/// </summary>
public class GlassGridPanel : TableLayoutPanel, IPassThroughContainer
{
    public GlassGridPanel()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        KioskSurfaceHelper.PaintParentBackground(e.Graphics, this);
    }
}

/// <summary>
/// Translucent frosted-glass card panel simulating CSS <c>backdrop-filter: blur()</c> in WinForms GDI+
/// while providing exact translated surface slices to child controls.
/// </summary>
public class GlassPanel : Panel, IKioskSurface
{
    private bool _highContrastMode;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 22;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int FillOpacity { get; set; } = 212; // ~83% white for >= 7:1 text contrast on blue-teal gradient

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? AccentBorderColor { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HighContrastMode
    {
        get => _highContrastMode;
        set
        {
            if (_highContrastMode == value) return;
            _highContrastMode = value;
            Invalidate(true);
        }
    }

    public GlassPanel()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Padding = new Padding(16);
    }

    public void PaintSurfaceForChild(Graphics childGraphics, Point offsetInSurface)
    {
        GraphicsState state = childGraphics.Save();
        childGraphics.TranslateTransform(-offsetInSurface.X, -offsetInSurface.Y);
        RenderGlassCard(childGraphics);
        childGraphics.Restore(state);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        RenderGlassCard(e.Graphics);
    }

    private void RenderGlassCard(Graphics g)
    {
        // 1. Paint exact slice of underlying backdrop so rounded outer corners blend seamlessly
        KioskSurfaceHelper.PaintParentBackground(g, this);

        if (Width <= 6 || Height <= 6) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle shadowRect = new(3, 5, Width - 6, Height - 6);
        Rectangle cardRect = new(1, 1, Width - 4, Height - 4);

        if (!_highContrastMode)
        {
            using GraphicsPath shadowPath = KioskTheme.CreateRoundedRect(shadowRect, CornerRadius);
            using var shadowBrush = new SolidBrush(Color.FromArgb(34, 0, 12, 32));
            g.FillPath(shadowBrush, shadowPath);
        }

        Color fill = _highContrastMode
            ? Color.White
            : Color.FromArgb(FillOpacity, 255, 255, 255);

        Color border = AccentBorderColor
            ?? (_highContrastMode ? KioskTheme.DarkNavyText : Color.FromArgb(185, 255, 255, 255));

        float borderWidth = AccentBorderColor.HasValue || _highContrastMode ? 2.4f : 1.5f;

        using GraphicsPath path = KioskTheme.CreateRoundedRect(cardRect, CornerRadius);
        using var fillBrush = new SolidBrush(fill);
        using var borderPen = new Pen(border, borderWidth);

        g.FillPath(fillBrush, path);
        g.DrawPath(borderPen, path);
    }
}

/// <summary>
/// Artifact-free text, badge, and banner control that always preserves <c>&amp;</c> characters (<c>NoPrefix</c>)
/// and supports smooth rounded pill/banner fills over glass or gradient surfaces.
/// </summary>
public class KioskLabel : Control
{
    private ContentAlignment _textAlign = ContentAlignment.TopLeft;
    private Color? _fillColor;
    private Color? _borderColor;
    private float _borderWidth = 1.5f;
    private int _cornerRadius = 12;
    private bool _wordWrap = true;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ContentAlignment TextAlign
    {
        get => _textAlign;
        set { _textAlign = value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? FillColor
    {
        get => _fillColor;
        set { _fillColor = value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float BorderWidth
    {
        get => _borderWidth;
        set { _borderWidth = value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool WordWrap
    {
        get => _wordWrap;
        set { _wordWrap = value; Invalidate(); }
    }

    public KioskLabel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        TabStop = false;
        ForeColor = KioskTheme.DarkNavyText;
        Font = KioskTheme.PxFont(18f);
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Invalidate(); }
    protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); Invalidate(); }
    protected override void OnPaddingChanged(EventArgs e) { base.OnPaddingChanged(e); Invalidate(); }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Handled atomically in OnPaint to avoid flicker
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        KioskSurfaceHelper.PaintParentBackground(g, this);

        if (Width <= 2 || Height <= 2) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        if (_fillColor.HasValue || _borderColor.HasValue)
        {
            Rectangle badgeRect = new(1, 1, Width - 3, Height - 3);
            using GraphicsPath path = KioskTheme.CreateRoundedRect(badgeRect, _cornerRadius);

            if (_fillColor.HasValue)
            {
                using var fillBrush = new SolidBrush(_fillColor.Value);
                g.FillPath(fillBrush, path);
            }

            if (_borderColor.HasValue)
            {
                using var pen = new Pen(_borderColor.Value, _borderWidth);
                g.DrawPath(pen, path);
            }
        }

        Rectangle textRect = new(
            Padding.Left,
            Padding.Top,
            Math.Max(1, Width - Padding.Horizontal),
            Math.Max(1, Height - Padding.Vertical));

        TextFormatFlags flags = ResolveAlignmentFlags(_textAlign) |
                                TextFormatFlags.NoPrefix |
                                TextFormatFlags.EndEllipsis |
                                (_wordWrap ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine);

        TextRenderer.DrawText(g, Text, Font, textRect, ForeColor, flags);
    }

    private static TextFormatFlags ResolveAlignmentFlags(ContentAlignment align) => align switch
    {
        ContentAlignment.TopLeft => TextFormatFlags.Top | TextFormatFlags.Left,
        ContentAlignment.TopCenter => TextFormatFlags.Top | TextFormatFlags.HorizontalCenter,
        ContentAlignment.TopRight => TextFormatFlags.Top | TextFormatFlags.Right,
        ContentAlignment.MiddleLeft => TextFormatFlags.VerticalCenter | TextFormatFlags.Left,
        ContentAlignment.MiddleCenter => TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
        ContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter | TextFormatFlags.Right,
        ContentAlignment.BottomLeft => TextFormatFlags.Bottom | TextFormatFlags.Left,
        ContentAlignment.BottomCenter => TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter,
        ContentAlignment.BottomRight => TextFormatFlags.Bottom | TextFormatFlags.Right,
        _ => TextFormatFlags.Top | TextFormatFlags.Left
    };
}

/// <summary>
/// Button visual variants aligned with Task 1 design tokens.
/// </summary>
public enum KioskButtonVariant
{
    PrimarySolid,
    SecondaryGlass,
    CancelRedOutline,
    DangerSolid,
    SuccessSolid,
    HeaderUtility
}

/// <summary>
/// Large-target rounded touchscreen button with pressed, hover, selected, and disabled states.
/// Paints its parent's exact surface slice first to prevent corner/VRAM artifacts.
/// </summary>
public class KioskButton : Button
{
    private bool _isPressed;
    private bool _isHovered;
    private KioskButtonVariant _variant = KioskButtonVariant.PrimarySolid;
    private bool _isSelectedCard;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public KioskButtonVariant Variant
    {
        get => _variant;
        set { _variant = value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 16;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsSelectedCard
    {
        get => _isSelectedCard;
        set { _isSelectedCard = value; Invalidate(); }
    }

    public KioskButton()
    {
        Height = 64;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        UseMnemonic = false;
        Font = KioskTheme.PxFont(19f, FontStyle.Bold);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
    }

    protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _isHovered = false; _isPressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { _isPressed = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { _isPressed = false; Invalidate(); base.OnMouseUp(mevent); }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Suppress default Button background erase; OnPaint composites cleanly onto parent surface
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        Graphics g = pevent.Graphics;

        // 1. Always paint clean parent surface first (prevents black corners & shared-buffer ghosting)
        KioskSurfaceHelper.PaintParentBackground(g, this);

        if (Width <= 4 || Height <= 4) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        Rectangle rect = new(2, 2, Width - 5, Height - 5);
        if (_isPressed && Enabled)
        {
            rect.Offset(0, 1);
        }

        (Color bg, Color fg, Color border, float borderW) = ResolveColors();

        using GraphicsPath path = KioskTheme.CreateRoundedRect(rect, CornerRadius);
        using var bgBrush = new SolidBrush(bg);
        using var borderPen = new Pen(border, borderW);

        g.FillPath(bgBrush, path);
        g.DrawPath(borderPen, path);

        // Selected badge indicator on selectable station/payment cards
        if (_isSelectedCard && Enabled && _variant != KioskButtonVariant.HeaderUtility)
        {
            int badgeSize = Math.Clamp(Height / 5, 18, 26);
            Rectangle checkCircle = new(rect.Right - badgeSize - 8, rect.Top + 8, badgeSize, badgeSize);
            using var checkBg = new SolidBrush(Color.White);
            g.FillEllipse(checkBg, checkCircle);
            using var checkFont = KioskTheme.PxFont(badgeSize * 0.65f, FontStyle.Bold);
            TextRenderer.DrawText(
                g,
                "✓",
                checkFont,
                checkCircle,
                KioskTheme.DeepBlue,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        Rectangle textBounds = new(rect.X + 8, rect.Y + 4, Math.Max(1, rect.Width - 16), Math.Max(1, rect.Height - 8));
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            textBounds,
            fg,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.WordBreak |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    private (Color Bg, Color Fg, Color Border, float BorderW) ResolveColors()
    {
        if (!Enabled)
        {
            return (Color.FromArgb(148, 163, 184), Color.FromArgb(241, 245, 249), Color.FromArgb(100, 116, 139), 1.5f);
        }

        if (_isSelectedCard && _variant == KioskButtonVariant.HeaderUtility)
        {
            return (Color.FromArgb(245, 255, 255, 255), KioskTheme.DarkNavyText, Color.White, 2.2f);
        }

        if (_isSelectedCard)
        {
            return (KioskTheme.DeepBlue, Color.White, Color.White, 2.5f);
        }

        return _variant switch
        {
            KioskButtonVariant.PrimarySolid => (
                _isPressed ? Color.FromArgb(7, 41, 99) : (_isHovered ? Color.FromArgb(16, 82, 188) : KioskTheme.DeepBlue),
                Color.White,
                Color.FromArgb(190, 255, 255, 255),
                1.6f),

            KioskButtonVariant.SecondaryGlass => (
                _isPressed ? Color.FromArgb(219, 234, 254) : (_isHovered ? Color.FromArgb(240, 247, 255) : Color.FromArgb(248, 251, 255)),
                KioskTheme.DarkNavyText,
                KioskTheme.DeepBlue,
                1.8f),

            KioskButtonVariant.CancelRedOutline => (
                _isPressed ? Color.FromArgb(254, 226, 226) : (_isHovered ? Color.FromArgb(255, 241, 242) : Color.FromArgb(252, 255, 255)),
                KioskTheme.ErrorRed,
                KioskTheme.ErrorRed,
                2.2f),

            KioskButtonVariant.DangerSolid => (
                _isPressed ? Color.FromArgb(140, 28, 22) : (_isHovered ? Color.FromArgb(198, 45, 36) : KioskTheme.ErrorRed),
                Color.White,
                Color.White,
                1.6f),

            KioskButtonVariant.SuccessSolid => (
                _isPressed ? Color.FromArgb(20, 98, 45) : (_isHovered ? Color.FromArgb(32, 148, 69) : KioskTheme.SuccessGreen),
                Color.White,
                Color.White,
                1.6f),

            KioskButtonVariant.HeaderUtility => (
                _isPressed ? Color.FromArgb(95, 255, 255, 255) : (_isHovered ? Color.FromArgb(68, 255, 255, 255) : Color.FromArgb(36, 255, 255, 255)),
                Color.White,
                Color.FromArgb(205, 255, 255, 255),
                1.6f),

            _ => (KioskTheme.DeepBlue, Color.White, KioskTheme.DeepBlue, 2f)
        };
    }
}

/// <summary>
/// 4-step kiosk progress bar: (1) Destination, (2) Payment, (3) Pay, (4) Ticket.
/// </summary>
public class StepProgressControl : Control
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ActiveStep { get; set; } // 0 = idle, 1..4 = active step

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsVietnamese { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HighContrastMode { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float UiScale { get; set; } = 1.0f;

    public StepProgressControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Height = 56;
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Composited cleanly in OnPaint
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        KioskSurfaceHelper.PaintParentBackground(g, this);

        if (Width <= 8 || Height <= 8) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        int radius = Math.Max(10, (int)(16 * UiScale));
        Rectangle box = new(1, 1, Width - 3, Height - 3);
        using GraphicsPath bgPath = KioskTheme.CreateRoundedRect(box, radius);
        using var bgBrush = new SolidBrush(HighContrastMode ? Color.White : Color.FromArgb(222, 255, 255, 255));
        using var borderPen = new Pen(HighContrastMode ? KioskTheme.DarkNavyText : Color.FromArgb(180, 255, 255, 255), HighContrastMode ? 2f : 1.4f);
        g.FillPath(bgBrush, bgPath);
        g.DrawPath(borderPen, bgPath);

        string[] labels = IsVietnamese
            ? new[] { "Điểm đến", "Phương thức", "Thanh toán", "Nhận vé" }
            : new[] { "Destination", "Payment", "Pay", "Ticket" };

        int colW = box.Width / 4;
        int circleDiam = Math.Max(22, (int)(32 * UiScale));
        using Font labelFont = KioskTheme.PxFont(17f * UiScale, FontStyle.Bold);
        using Font numFont = KioskTheme.PxFont(15f * UiScale, FontStyle.Bold);

        for (int i = 0; i < 4; i++)
        {
            int stepIndex = i + 1;
            bool isDone = ActiveStep > stepIndex || (ActiveStep == 4 && stepIndex == 4);
            bool isCurrent = ActiveStep == stepIndex && ActiveStep < 4;

            Color circleColor = isDone
                ? KioskTheme.SuccessGreen
                : (isCurrent ? KioskTheme.DeepBlue : Color.FromArgb(148, 163, 184));
            Color textColor = isDone
                ? KioskTheme.SuccessGreen
                : (isCurrent ? KioskTheme.DeepBlue : Color.FromArgb(60, 76, 98));

            int startX = box.X + i * colW + (int)(22 * UiScale);
            int centerY = box.Y + box.Height / 2;

            Rectangle circle = new(startX, centerY - circleDiam / 2, circleDiam, circleDiam);
            using var cBrush = new SolidBrush(circleColor);
            g.FillEllipse(cBrush, circle);

            string badgeText = isDone ? "✓" : stepIndex.ToString();
            TextRenderer.DrawText(
                g,
                badgeText,
                numFont,
                circle,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            int textX = circle.Right + (int)(10 * UiScale);
            Rectangle textRect = new(textX, box.Y, Math.Max(20, (box.X + (i + 1) * colW) - textX - 8), box.Height);
            TextRenderer.DrawText(
                g,
                labels[i],
                labelFont,
                textRect,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // Subtle connector line to next step
            if (i < 3)
            {
                int nextCircleX = box.X + (i + 1) * colW + (int)(22 * UiScale);
                int lineStart = box.X + (i + 1) * colW - (int)(38 * UiScale);
                int lineEnd = nextCircleX - (int)(8 * UiScale);
                if (lineEnd > lineStart)
                {
                    using var connPen = new Pen(isDone ? KioskTheme.SuccessGreen : Color.FromArgb(190, 203, 218), 2f);
                    g.DrawLine(connPen, lineStart, centerY, lineEnd, centerY);
                }
            }
        }
    }
}

/// <summary>
/// Double-buffered <see cref="DataGridView"/> for smooth, flicker-free transaction report rendering.
/// </summary>
public class BufferedDataGridView : DataGridView
{
    public BufferedDataGridView()
    {
        DoubleBuffered = true;
    }
}
