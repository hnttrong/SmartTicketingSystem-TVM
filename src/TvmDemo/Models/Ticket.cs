namespace TvmDemo.Models;

/// <summary>
/// Domain entity representing an issued paper ticket with a printed barcode.
/// Maps to <c>dbo.Ticket</c>.
/// </summary>
public class Ticket
{
    /// <summary>Database primary key.</summary>
    public int TicketId { get; set; }

    /// <summary>Foreign key to parent <see cref="Transaction"/>.</summary>
    public int TransactionId { get; set; }

    /// <summary>Unique human-readable ticket serial number (e.g., TKT-20261029-143012-1).</summary>
    public string TicketNumber { get; set; } = string.Empty;

    /// <summary>Encoded 1D barcode payload printed on the ticket (e.g., M1|BT|M1-14|20261029|A1B2C3).</summary>
    public string BarcodeValue { get; set; } = string.Empty;

    /// <summary>Sequence index within the transaction (1..Quantity).</summary>
    public int PassengerSeq { get; set; } = 1;

    /// <summary>Origin station label.</summary>
    public string OriginStation { get; set; } = "Bến Thành (Ben Thanh)";

    /// <summary>Timestamp when the ticket was issued.</summary>
    public DateTime IssuedAt { get; set; } = DateTime.Now;

    /// <summary>Expiration timestamp (valid for 24 hours on date of issue).</summary>
    public DateTime ValidUntil { get; set; } = DateTime.Now.AddHours(24);

    /// <summary>Indicates whether the physical thermal print job completed cleanly.</summary>
    public bool IsPrinted { get; set; } = true;
}
