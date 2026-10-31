// Smart Ticketing System - Ticket Vendor Machine (TVM)
// Integrated from Member M3's Forward-Engineered Skeleton (TVM.cs / Section 3.3.3 Class Diagram)
// and implemented for Member M5's C# .NET 8 WinForms & MSSQL Demo (Section 5.3).
// Every class, enum, attribute, and method name matches M3's class_diagram_1_core.puml & class_diagram_2_supporting.puml.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace SmartTicketing.TVM;

// ------------------------------------------------------------------ enumerations (M3 3.3.3)
public enum MachineState
{
    Idle, SelectingDestination, ReviewingOrder, SelectingPayment, AwaitingPayment,
    PaymentConfirmed, Printing, Completed, Refunding, Cancelled,
    OutOfService, Maintenance, Recovering
}

public enum TransactionStatus
{
    Created, AwaitingPayment, Confirmed, Completed, Failed, Cancelled,
    RefundPending, Refunded, PendingReconciliation
}

public enum TransportMode { Bus, Mrt }

public enum PaymentMethod { CreditCard, BankQR, DigitalWallet }

public enum PaymentStatus { Pending, Approved, Declined, Expired, Cancelled }

public enum RefundStatus { Requested, Confirmed, Escalated }

public enum WalletProvider { Momo, ZaloPay, VNPay }

// ------------------------------------------------------------------ small data types (M3 2/2)
public class CardData
{
    public string GatewayToken { get; set; } = string.Empty;
    public string CardScheme { get; set; } = "VISA";
    public string Last4 { get; set; } = "4242"; // NFR9: never the full card number
}

public class GatewayCallback
{
    public string TransactionRef { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Signature { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
}

public class PaymentResult
{
    public PaymentStatus Status { get; set; }
    public string GatewayRef { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

// ------------------------------------------------------------------ domain entities (M3 1/2)
public class Destination
{
    public int DestinationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TransportMode Mode { get; set; } = TransportMode.Mrt;
    public string Origin { get; set; } = "Bến Thành (M1-01)";
}

public class Fare
{
    public int FareId { get; set; }
    public decimal AmountVND { get; set; } // DR1: whole VND
    public DateTime EffectiveFrom { get; set; } = DateTime.Now;

    public decimal GetAmount() => AmountVND;
}

public class Ticket
{
    private static readonly Random Rng = new();

    public string TicketNumber { get; set; } = string.Empty; // 16 digits with Luhn check digit (DR4)
    public string Barcode { get; set; } = string.Empty;      // Code 128 content
    public TransportMode Mode { get; set; }                  // printed on the ticket (FR15, Q15)
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; } = DateTime.Now;
    public DateTime ValidUntil { get; set; } = DateTime.Now.AddHours(24);
    public decimal Fare { get; set; }

    // sequence = 0..4, 15 digits = yyMMddHHmmss (12) + sequence (1) + random (2), + Luhn check digit = 16 digits (FR16, DR4)
    public string GenerateBarcode(int sequence)
    {
        if (sequence < 0 || sequence >= Transaction.MaxQuantity)
            throw new ArgumentOutOfRangeException(nameof(sequence));

        string body = DateTime.Now.ToString("yyMMddHHmmss") + sequence.ToString("D1") + Rng.Next(0, 100).ToString("D2");
        TicketNumber = body + LuhnCheckDigit(body);
        Barcode = TicketNumber;
        return Barcode;
    }

    public static int LuhnCheckDigit(string digits)
    {
        int sum = 0;
        bool doubleIt = true;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int n = digits[i] - '0';
            if (doubleIt)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            doubleIt = !doubleIt;
        }
        return (10 - (sum % 10)) % 10;
    }
}

public class Refund
{
    public string RefundId { get; set; } = $"RFD-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.Now;
    public RefundStatus Status { get; set; } = RefundStatus.Requested;
}

public class Transaction
{
    public const int MaxQuantity = 5;        // FR3
    public const int MaxAttempts = 3;        // FR12
    public const int MaxQrRegenerations = 2; // FR13

    public string TransactionId { get; set; } = $"TXN-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(10, 99)}";
    public int Quantity { get; set; } = 1;
    public decimal FareSnapshot { get; private set; }
    public decimal TotalAmount { get; private set; }
    public int RetryCount { get; set; }
    public int QrRegenCount { get; set; }
    public TransactionStatus Status { get; private set; } = TransactionStatus.Created;
    public DateTime CreatedAt { get; } = DateTime.Now;
    public string GatewayRef { get; set; } = string.Empty;

    public Destination? Destination { get; set; }
    public List<Payment> Payments { get; } = new();
    public List<Ticket> Tickets { get; } = new(); // 1..5
    public Refund? Refund { get; set; }           // 0..1

    public void LockFare(Fare fare) => FareSnapshot = fare.GetAmount(); // FR4

    public decimal CalculateTotal()
    {
        if (Quantity < 1 || Quantity > MaxQuantity)
            throw new ArgumentOutOfRangeException(nameof(Quantity), "Quantity must be 1 to 5.");
        TotalAmount = FareSnapshot * Quantity;
        return TotalAmount;
    }

    public bool CanRetry() => RetryCount < MaxAttempts;
    public bool CanRegenerateQR() => QrRegenCount < MaxQrRegenerations;

    public void MarkAwaitingPayment() => Status = TransactionStatus.AwaitingPayment;
    public void MarkConfirmed() => Status = TransactionStatus.Confirmed;
    public void MarkFailed(string reason) { RetryCount++; Status = TransactionStatus.Failed; }
    public void MarkCancelled() => Status = TransactionStatus.Cancelled;
    public void MarkCompleted() => Status = TransactionStatus.Completed;
    public void MarkRefundPending() => Status = TransactionStatus.RefundPending;
    public void MarkPendingReconciliation() => Status = TransactionStatus.PendingReconciliation;
}

// ------------------------------------------------------------------ payment & gateway adapters (M3 1/2 & 2/2)
public abstract class Payment
{
    public string PaymentId { get; protected set; } = $"PAY-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
    public decimal Amount { get; protected set; }
    public PaymentStatus Status { get; protected set; } = PaymentStatus.Pending;
    public string GatewayRef { get; protected set; } = string.Empty;
    public IPaymentGateway? Gateway { get; set; }

    protected Payment(decimal amount, IPaymentGateway? gateway = null)
    {
        Amount = amount;
        Gateway = gateway;
    }

    public abstract void Start();

    public bool Cancel()
    {
        if (Status != PaymentStatus.Pending) return false;
        bool ok = Gateway?.Cancel(GatewayRef) ?? true;
        if (ok) Status = PaymentStatus.Cancelled;
        return ok;
    }

    public void ApplyGatewayStatus(PaymentStatus newStatus, string? gwRef = null)
    {
        Status = newStatus;
        if (!string.IsNullOrWhiteSpace(gwRef)) GatewayRef = gwRef;
    }
}

public class CardPayment : Payment
{
    public string CardScheme { get; set; } = "VISA";
    public string Last4 { get; set; } = "4242";
    public string GatewayToken { get; set; } = string.Empty;

    public CardPayment(decimal amount, IPaymentGateway? gateway = null) : base(amount, gateway) { }

    public override void Start()
    {
        Status = PaymentStatus.Pending;
        if (Gateway != null)
            GatewayRef = Gateway.CreatePayment(PaymentId, Amount);
    }
}

public class QRPayment : Payment
{
    public const int QrValiditySeconds = 120; // FR13: 120 s countdown

    public string QrContent { get; protected set; } = string.Empty;
    public DateTime ExpiresAt { get; protected set; } // created + 120 s (FR13)
    public WalletProvider? Provider { get; set; }     // null = bank (VietQR) code, else Momo/ZaloPay/VNPay

    public QRPayment(decimal amount, WalletProvider? provider, IPaymentGateway? gateway = null) : base(amount, gateway)
    {
        Provider = provider;
        ExpiresAt = DateTime.Now.AddSeconds(QrValiditySeconds);
    }

    public string GenerateQR()
    {
        string tag = Provider?.ToString().ToUpperInvariant() ?? "VIETQR-NAPAS247";
        ExpiresAt = DateTime.Now.AddSeconds(QrValiditySeconds);
        QrContent = $"0002010102123857{tag}|REF={PaymentId}|AMT={Amount:0}|EXP={ExpiresAt:HHmmss}";
        return QrContent;
    }

    public bool IsExpired() => DateTime.Now >= ExpiresAt;

    public override void Start()
    {
        Status = PaymentStatus.Pending;
        GenerateQR();
        if (Gateway != null)
            GatewayRef = Gateway.CreatePayment(PaymentId, Amount);
    }
}

public interface IPaymentGateway
{
    string CreatePayment(string txnId, decimal amount);
    PaymentStatus QueryStatus(string reference);
    bool Cancel(string reference);
    string Refund(string reference, decimal amount);
    bool VerifyCallback(GatewayCallback callback); // signature, amount, reference (FR10)
}

/// <summary>
/// Shared mock simulation base realizing <see cref="IPaymentGateway"/> for the 5 concrete gateway adapters
/// in Member M3's Class Diagram 2/2 (CardAcquirerAdapter, VietQRAdapter, MomoAdapter, ZaloPayAdapter, VNPayAdapter).
/// </summary>
public abstract class SimulatedGatewayAdapterBase : IPaymentGateway
{
    private const string DemoWebhookSecret = "TVM-HMAC-DEMO-SECRET-2026";
    protected abstract string Prefix { get; }
    public PaymentStatus SimulatedOutcome { get; set; } = PaymentStatus.Approved;

    public string CreatePayment(string txnId, decimal amount) =>
        $"{Prefix}-{DateTime.Now:HHmmss}-{Random.Shared.Next(1000, 9999)}";

    public PaymentStatus QueryStatus(string reference) => SimulatedOutcome;

    public bool Cancel(string reference) => true;

    public string Refund(string reference, decimal amount) =>
        $"RFD-{Prefix}-{Random.Shared.Next(10000, 99999)}";

    public bool VerifyCallback(GatewayCallback callback)
    {
        if (string.IsNullOrWhiteSpace(callback.TransactionRef) || callback.Amount <= 0)
            return false;
        string expected = ComputeHmac(callback.TransactionRef, callback.Amount, callback.Status);
        return string.Equals(callback.Signature, expected, StringComparison.OrdinalIgnoreCase);
    }

    public static string ComputeHmac(string txRef, decimal amount, PaymentStatus status)
    {
        byte[] key = Encoding.UTF8.GetBytes(DemoWebhookSecret);
        byte[] msg = Encoding.UTF8.GetBytes($"{txRef}|{amount:0}|{status}");
        return Convert.ToHexString(HMACSHA256.HashData(key, msg));
    }
}

public class CardAcquirerAdapter : SimulatedGatewayAdapterBase { protected override string Prefix => "CARD"; }
public class VietQRAdapter : SimulatedGatewayAdapterBase { protected override string Prefix => "VIETQR"; }
public class MomoAdapter : SimulatedGatewayAdapterBase { protected override string Prefix => "MOMO"; }
public class ZaloPayAdapter : SimulatedGatewayAdapterBase { protected override string Prefix => "ZALOPAY"; }
public class VNPayAdapter : SimulatedGatewayAdapterBase { protected override string Prefix => "VNPAY"; }

public class PaymentService
{
    private readonly CardAcquirerAdapter _card = new();
    private readonly VietQRAdapter _vietQr = new();
    private readonly MomoAdapter _momo = new();
    private readonly ZaloPayAdapter _zalo = new();
    private readonly VNPayAdapter _vnpay = new();

    public WalletProvider SelectedWallet { get; set; } = WalletProvider.Momo;

    public IPaymentGateway ResolveGateway(PaymentMethod method, WalletProvider? provider = null) =>
        method switch
        {
            PaymentMethod.CreditCard => _card,
            PaymentMethod.BankQR => _vietQr,
            PaymentMethod.DigitalWallet => (provider ?? SelectedWallet) switch
            {
                WalletProvider.ZaloPay => _zalo,
                WalletProvider.VNPay => _vnpay,
                _ => _momo
            },
            _ => _card
        };

    public Payment StartPayment(Transaction txn, PaymentMethod method)
    {
        decimal amt = txn.CalculateTotal();
        IPaymentGateway gw = ResolveGateway(method, method == PaymentMethod.DigitalWallet ? SelectedWallet : null);
        Payment payment = method == PaymentMethod.CreditCard
            ? new CardPayment(amt, gw)
            : new QRPayment(amt, method == PaymentMethod.BankQR ? null : SelectedWallet, gw);

        payment.Start();
        txn.Payments.Add(payment);
        txn.GatewayRef = payment.GatewayRef;
        txn.MarkAwaitingPayment();
        return payment;
    }

    public PaymentResult HandleCallback(GatewayCallback callback)
    {
        bool valid = _vietQr.VerifyCallback(callback);
        if (!valid)
        {
            return new PaymentResult
            {
                Status = PaymentStatus.Declined,
                GatewayRef = callback.TransactionRef,
                Reason = "HMAC-SHA256 callback signature verification failed (FR10)."
            };
        }

        return new PaymentResult
        {
            Status = callback.Status,
            GatewayRef = callback.TransactionRef,
            Reason = callback.Status == PaymentStatus.Approved ? "Approved" : callback.Status.ToString()
        };
    }

    public PaymentStatus QueryStatus(Transaction txn) =>
        txn.Payments.Count == 0 ? PaymentStatus.Pending : txn.Payments[^1].Gateway?.QueryStatus(txn.GatewayRef) ?? PaymentStatus.Pending;

    public bool CancelPayment(Transaction txn) =>
        txn.Payments.Count == 0 || txn.Payments[^1].Cancel();

    public Refund Refund(Transaction txn)
    {
        IPaymentGateway gw = txn.Payments.Count > 0 && txn.Payments[^1].Gateway != null
            ? txn.Payments[^1].Gateway!
            : _card;
        string rfdRef = gw.Refund(txn.GatewayRef, txn.TotalAmount);
        var refund = new Refund
        {
            Amount = txn.TotalAmount,
            Reference = rfdRef,
            RequestedAt = DateTime.Now,
            Status = RefundStatus.Confirmed
        };
        txn.Refund = refund;
        txn.MarkRefundPending();
        return refund;
    }
}

// ------------------------------------------------------------------ hardware and infrastructure (M3 1/2 & 2/2)
public class TicketPrinter
{
    public int PaperRemaining { get; private set; } = 200;
    public bool Ready { get; private set; } = true;

    public bool IsReady() => Ready && PaperRemaining > 0;
    public int GetPaperLevel() => PaperRemaining;

    public bool Print(Ticket ticket)
    {
        if (!IsReady()) return false;
        PaperRemaining--;
        if (PaperRemaining <= 0) Ready = false;
        return true;
    }
}

public class CardReader
{
    public CardData ReadCard() => new()
    {
        CardScheme = "VISA",
        Last4 = "4242",
        GatewayToken = $"TOK-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}"
    };

    public void Eject() { /* Hardware card-eject no-op for contactless / motorized slot */ }
}

public class Display
{
    public string LastScreenMessage { get; private set; } = string.Empty;
    public void ShowMenu(List<Destination> destinations) => LastScreenMessage = $"Menu ({destinations.Count} stations)";
    public void ShowSummary(Transaction txn) => LastScreenMessage = $"Order {txn.TransactionId}: {txn.TotalAmount:N0} VND";
    public void ShowPaymentModes(List<PaymentMethod> modes) => LastScreenMessage = $"Modes ({modes.Count})";
    public void ShowQR(string qrContent, int secondsLeft) => LastScreenMessage = $"QR ({secondsLeft}s): {qrContent}";
    public void ShowMessage(string text) => LastScreenMessage = text;
    public void ShowOutOfService() => LastScreenMessage = "OUT OF SERVICE";
}

public class TransactionRepository
{
    public string ConnectionString { get; set; } =
        @"Server=(localdb)\MSSQLLocalDB;Database=TvmDemo;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=4;";

    public void Save(Transaction txn)
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        const string sql = @"
            MERGE dbo.[Transaction] AS target
            USING (SELECT @Id AS TransactionId) AS src
            ON target.TransactionId = src.TransactionId
            WHEN MATCHED THEN
                UPDATE SET Status = @Status, RetryCount = @Retry, QrRegenCount = @Regen, GatewayRef = @GwRef, UpdatedAt = SYSDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (TransactionId, DestinationId, Quantity, FareSnapshot, TotalAmount, RetryCount, QrRegenCount, Status, GatewayRef)
                VALUES (@Id, @DestId, @Qty, @Fare, @Total, @Retry, @Regen, @Status, @GwRef);";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", txn.TransactionId);
        cmd.Parameters.AddWithValue("@DestId", txn.Destination?.DestinationId ?? 1);
        cmd.Parameters.AddWithValue("@Qty", txn.Quantity);
        cmd.Parameters.AddWithValue("@Fare", txn.FareSnapshot);
        cmd.Parameters.AddWithValue("@Total", txn.TotalAmount);
        cmd.Parameters.AddWithValue("@Retry", txn.RetryCount);
        cmd.Parameters.AddWithValue("@Regen", txn.QrRegenCount);
        cmd.Parameters.AddWithValue("@Status", txn.Status.ToString());
        cmd.Parameters.AddWithValue("@GwRef", (object?)txn.GatewayRef ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public Transaction? FindById(string id)
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(
            "SELECT TransactionId, DestinationId, Quantity, Status, GatewayRef FROM dbo.[Transaction] WHERE TransactionId = @Id;", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Transaction
        {
            TransactionId = r.GetString(0),
            Quantity = r.GetInt32(2),
            GatewayRef = r.IsDBNull(4) ? string.Empty : r.GetString(4)
        };
    }

    public List<Transaction> FindUnfinished()
    {
        var list = new List<Transaction>();
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(
            "SELECT TransactionId, Quantity FROM dbo.[Transaction] WHERE Status IN ('Created','AwaitingPayment','Confirmed');", conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Transaction { TransactionId = r.GetString(0), Quantity = r.GetInt32(1) });
        return list;
    }

    public List<Destination> GetDestinations()
    {
        var list = new List<Destination>();
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT DestinationId, Name, Mode, Origin FROM dbo.Destination WHERE IsActive = 1 ORDER BY ZoneOrder;", conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Destination
            {
                DestinationId = r.GetInt32(0),
                Name = r.GetString(1),
                Mode = Enum.TryParse<TransportMode>(r.GetString(2), true, out var m) ? m : TransportMode.Mrt,
                Origin = r.GetString(3)
            });
        }
        return list;
    }

    public Fare GetFare(int destinationId)
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(
            "SELECT TOP (1) FareId, AmountVND, EffectiveFrom FROM dbo.Fare WHERE DestinationId = @Id ORDER BY EffectiveFrom DESC;", conn);
        cmd.Parameters.AddWithValue("@Id", destinationId);
        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            return new Fare
            {
                FareId = r.GetInt32(0),
                AmountVND = r.GetDecimal(1),
                EffectiveFrom = r.GetDateTime(2)
            };
        }
        return new Fare { FareId = 0, AmountVND = 10000, EffectiveFrom = DateTime.Now };
    }
}

public class AlertService
{
    public List<string> SentAlerts { get; } = new();
    public void SendAlert(string type, string detail) =>
        SentAlerts.Add($"[{DateTime.Now:HH:mm:ss}] {type}: {detail}");
}

// ------------------------------------------------------------------ controller (M3 1/2)
public class TVMController
{
    private MachineState state = MachineState.Idle;
    private int idleSeconds;

    public MachineState State => state;
    public Display Display { get; } = new();
    public TicketPrinter Printer { get; } = new();
    public CardReader CardReader { get; } = new();
    public PaymentService PaymentService { get; } = new();
    public TransactionRepository Repository { get; } = new();
    public AlertService Alerts { get; } = new();
    public Transaction? Current { get; private set; }

    public void OnStart()
    {
        idleSeconds = 0;
        if (!Printer.IsReady())
        {
            state = MachineState.OutOfService;
            Alerts.SendAlert("PRINTER_OUT_OF_PAPER", "Thermal printer not ready at session start.");
            Display.ShowOutOfService();
            return;
        }
        state = MachineState.SelectingDestination;
        Display.ShowMenu(Repository.GetDestinations());
    }

    public void SelectDestination(int destinationId, int quantity)
    {
        List<Destination> all = Repository.GetDestinations();
        Destination dest = all.First(d => d.DestinationId == destinationId);
        Fare fare = Repository.GetFare(destinationId);

        Current = new Transaction
        {
            Destination = dest,
            Quantity = quantity
        };
        Current.LockFare(fare);
        Current.CalculateTotal();
        state = MachineState.ReviewingOrder;
        Display.ShowSummary(Current);
    }

    public void ConfirmOrder()
    {
        if (Current == null) return;
        Repository.Save(Current);
        state = MachineState.SelectingPayment;
        Display.ShowPaymentModes(Enum.GetValues<PaymentMethod>().ToList());
    }

    public void SelectPaymentMethod(PaymentMethod method)
    {
        if (Current == null) return;
        Payment p = PaymentService.StartPayment(Current, method);
        Repository.Save(Current);
        state = MachineState.AwaitingPayment;
        if (p is QRPayment qr)
            Display.ShowQR(qr.QrContent, QRPayment.QrValiditySeconds);
    }

    public void OnPaymentResult(PaymentResult result)
    {
        if (Current == null) return;
        if (result.Status == PaymentStatus.Approved)
        {
            Current.MarkConfirmed();
            state = MachineState.PaymentConfirmed;
            PrintTickets();
        }
        else
        {
            Current.MarkFailed(result.Reason);
            Repository.Save(Current);
            state = Current.CanRetry() ? MachineState.SelectingPayment : MachineState.Cancelled;
            Display.ShowMessage(result.Reason);
        }
    }

    public void Cancel()
    {
        if (Current != null)
        {
            PaymentService.CancelPayment(Current);
            Current.MarkCancelled();
            Repository.Save(Current);
        }
        state = MachineState.Cancelled;
        state = MachineState.Idle;
    }

    public void OnIdleTimeout()
    {
        idleSeconds += 120;
        Cancel();
    }

    public void PrintTickets()
    {
        if (Current == null || Current.Destination == null) return;
        state = MachineState.Printing;

        for (int i = 0; i < Current.Quantity; i++)
        {
            var ticket = new Ticket
            {
                Mode = Current.Destination.Mode,
                Origin = Current.Destination.Origin,
                Destination = Current.Destination.Name,
                Fare = Current.FareSnapshot,
                IssuedAt = DateTime.Now,
                ValidUntil = DateTime.Now.AddHours(24)
            };
            ticket.GenerateBarcode(i);
            if (!Printer.Print(ticket))
            {
                Alerts.SendAlert("PRINT_FAILURE", $"Failed printing ticket #{i + 1} for {Current.TransactionId}");
                PaymentService.Refund(Current);
                Repository.Save(Current);
                state = MachineState.OutOfService;
                Display.ShowOutOfService();
                return;
            }
            Current.Tickets.Add(ticket);
        }

        Current.MarkCompleted();
        Repository.Save(Current);
        state = MachineState.Completed;
    }

    public void RecoverLastTransaction()
    {
        state = MachineState.Recovering;
        foreach (Transaction unfinished in Repository.FindUnfinished())
        {
            unfinished.MarkPendingReconciliation();
            Repository.Save(unfinished);
        }
        state = Printer.IsReady() ? MachineState.Idle : MachineState.OutOfService;
    }
}
