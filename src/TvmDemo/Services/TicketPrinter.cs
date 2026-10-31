using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Text;
using TvmDemo.Models;

namespace TvmDemo.Services;

/// <summary>
/// Result returned by <see cref="TicketPrinter"/> after attempting to print paper tickets.
/// </summary>
public record PrintTicketResult(bool Success, List<Ticket> IssuedTickets, string? ErrorMessage);

/// <summary>
/// Hardware abstraction and simulator for the kiosk ESC/POS thermal receipt and barcode printer.
/// Matches the shared class name <c>TicketPrinter</c>.
/// </summary>
public class TicketPrinter
{
    /// <summary>Gets or sets a demo toggle to simulate an unexpected thermal printer jam / paper-out fault.</summary>
    public bool SimulateHardwareFault { get; set; }

    /// <summary>
    /// Generates and simulates printing paper tickets with unique 1D barcode strings for a confirmed transaction.
    /// </summary>
    public PrintTicketResult PrintTickets(Transaction transaction, TicketVendorMachine machine)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(machine);

        if (SimulateHardwareFault || machine.RemainingPaperSheets < transaction.Quantity)
        {
            return new PrintTicketResult(
                false,
                new List<Ticket>(),
                "HW-PRN-01: Thermal printer paper exhausted or paper jam detected during print job.");
        }

        var tickets = new List<Ticket>(transaction.Quantity);

        for (int seq = 1; seq <= transaction.Quantity; seq++)
        {
            // Use Member M3's 16-digit Luhn check-digit TicketNumber & Barcode generator (DR4, FR16)
            var m3Ticket = new SmartTicketing.TVM.Ticket();
            string luhn16 = m3Ticket.GenerateBarcode(seq - 1);

            var ticket = new Ticket
            {
                TransactionId = transaction.TransactionId,
                PassengerSeq = seq,
                TicketNumber = luhn16,
                BarcodeValue = luhn16,
                OriginStation = "Bến Thành (Ben Thanh)",
                IssuedAt = DateTime.Now,
                ValidUntil = DateTime.Now.AddHours(24),
                IsPrinted = true
            };
            tickets.Add(ticket);
        }

        machine.RemainingPaperSheets -= transaction.Quantity;
        return new PrintTicketResult(true, tickets, null);
    }

    /// <summary>
    /// Renders a crisp 1D linear barcode bitmap via GDI+ from a ticket barcode string.
    /// </summary>
    public static Bitmap RenderBarcodeBitmap(string barcodeText, int width = 380, int height = 76)
    {
        var bmp = new Bitmap(width, height);
        using Graphics g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.None;

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(barcodeText));
        int x = 14;
        int maxBarX = width - 14;
        int barHeight = height - 24;

        using var darkBrush = new SolidBrush(Color.FromArgb(11, 42, 92));

        // Start guard bars
        g.FillRectangle(darkBrush, x, 6, 3, barHeight);
        x += 5;
        g.FillRectangle(darkBrush, x, 6, 2, barHeight);
        x += 5;

        int idx = 0;
        while (x < maxBarX - 12)
        {
            byte b = hash[idx % hash.Length];
            int barW = (b % 3) + 1;
            int gapW = ((b >> 2) % 3) + 2;
            if (x + barW < maxBarX - 10)
            {
                g.FillRectangle(darkBrush, x, 6, barW, barHeight - 4);
            }
            x += barW + gapW;
            idx++;
        }

        // End guard bars
        g.FillRectangle(darkBrush, maxBarX - 8, 6, 2, barHeight);
        g.FillRectangle(darkBrush, maxBarX - 4, 6, 3, barHeight);

        using var font = new Font("Consolas", 8.5f, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString(barcodeText, font, darkBrush, new RectangleF(0, height - 18, width, 18), sf);

        return bmp;
    }

    /// <summary>
    /// Renders a deterministic 21x21 QR-like 2D matrix bitmap via GDI+ for screen S04b.
    /// </summary>
    public static Bitmap RenderQrBitmap(string payload, int size = 210)
    {
        const int modules = 21;
        var bmp = new Bitmap(size, size);
        using Graphics g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.None;

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        int margin = 12;
        float cell = (size - margin * 2f) / modules;
        using var brush = new SolidBrush(Color.FromArgb(11, 42, 92));

        void DrawFinder(int startCol, int startRow)
        {
            for (int r = 0; r < 7; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    bool border = r == 0 || r == 6 || c == 0 || c == 6;
                    bool center = r >= 2 && r <= 4 && c >= 2 && c <= 4;
                    if (border || center)
                    {
                        g.FillRectangle(brush, margin + (startCol + c) * cell, margin + (startRow + r) * cell, cell + 0.4f, cell + 0.4f);
                    }
                }
            }
        }

        DrawFinder(0, 0);
        DrawFinder(modules - 7, 0);
        DrawFinder(0, modules - 7);

        int bitIdx = 0;
        for (int r = 0; r < modules; r++)
        {
            for (int c = 0; c < modules; c++)
            {
                bool inFinderZone =
                    (r < 8 && c < 8) ||
                    (r < 8 && c >= modules - 8) ||
                    (r >= modules - 8 && c < 8);
                if (inFinderZone) continue;

                bool on = ((digest[(bitIdx / 8) % digest.Length] >> (bitIdx % 8)) & 1) == 1;
                if (on)
                {
                    g.FillRectangle(brush, margin + c * cell, margin + r * cell, cell + 0.3f, cell + 0.3f);
                }
                bitIdx++;
            }
        }

        return bmp;
    }
}
