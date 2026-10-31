namespace TvmDemo.Models;

/// <summary>
/// Persistence lifecycle status of a ticket purchase transaction in <c>dbo.[Transaction]</c>.
/// Matches the SQL Server CHECK constraint ('Pending', 'Completed', 'Failed', 'Cancelled').
/// </summary>
public enum TransactionStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Supported high-level payment modes matching <c>dbo.[Transaction].PaymentMode</c>.
/// </summary>
public enum PaymentModeType
{
    CreditCard,
    BankQR,
    DigitalWallet
}

/// <summary>
/// Domain entity representing a TVM ticket purchase transaction.
/// Maps to <c>dbo.[Transaction]</c>.
/// </summary>
public class Transaction
{
    /// <summary>Database primary key.</summary>
    public int TransactionId { get; set; }

    /// <summary>Human-readable unique reference code (e.g., TXN-20261029-143012).</summary>
    public string TransactionCode { get; set; } = string.Empty;

    /// <summary>Kiosk machine identifier (e.g., TVM-BT-01).</summary>
    public string MachineCode { get; set; } = "TVM-BT-01";

    /// <summary>Foreign key to <see cref="Destination"/>.</summary>
    public int DestinationId { get; set; }

    /// <summary>Navigation reference to selected <see cref="Destination"/>.</summary>
    public Destination? Destination { get; set; }

    /// <summary>Number of tickets purchased (1–5).</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Unit fare at the time of purchase in VND.</summary>
    public decimal UnitFareVnd { get; set; }

    /// <summary>Total payable amount in VND (<c>Quantity * UnitFareVnd</c>).</summary>
    public decimal TotalAmountVnd { get; set; }

    /// <summary>Selected payment mode.</summary>
    public PaymentModeType PaymentMode { get; set; } = PaymentModeType.CreditCard;

    /// <summary>Current transaction lifecycle state.</summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    /// <summary>Optional failure or cancellation diagnostic note.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Last status update timestamp.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>Associated payment attempt.</summary>
    public Payment? Payment { get; set; }

    /// <summary>Issued tickets (populated only when <see cref="Status"/> is <see cref="TransactionStatus.Completed"/>).</summary>
    public List<Ticket> Tickets { get; set; } = new();
}

/// <summary>
/// Read-model DTO representing a joined row from <c>dbo.vw_TransactionReport</c> / <c>dbo.usp_GetRecentTransactions</c>.
/// </summary>
public class TransactionReportRow
{
    public int TransactionId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string StationNameEn { get; set; } = string.Empty;
    public string StationNameVi { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitFareVnd { get; set; }
    public decimal TotalAmountVnd { get; set; }
    public string PaymentMode { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string TransactionStatus { get; set; } = string.Empty;
    public string TicketNumbers { get; set; } = string.Empty;
    public string PrimaryBarcode { get; set; } = string.Empty;
    public int TicketsIssuedCount { get; set; }
    public string Notes { get; set; } = string.Empty;
}
