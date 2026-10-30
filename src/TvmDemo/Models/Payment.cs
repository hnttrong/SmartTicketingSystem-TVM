namespace TvmDemo.Models;

/// <summary>
/// Payment authorization statuses matching <c>dbo.Payment.PaymentStatus</c>.
/// </summary>
public enum PaymentResultStatus
{
    Pending,
    Success,
    Declined,
    Timeout,
    Cancelled
}

/// <summary>
/// Abstract base class for all TVM payment instruments (<see cref="CardPayment"/>,
/// <see cref="QRPayment"/>, and <see cref="WalletPayment"/>).
/// </summary>
public abstract class Payment
{
    /// <summary>Database primary key.</summary>
    public int PaymentId { get; set; }

    /// <summary>Foreign key to parent <see cref="Transaction"/>.</summary>
    public int TransactionId { get; set; }

    /// <summary>Discriminator matching <c>dbo.Payment.PaymentType</c>.</summary>
    public abstract string PaymentType { get; }

    /// <summary>Acquirer or wallet provider name (e.g., Visa/Mastercard, VietQR-Bank, MoMo, VNPay, ZaloPay).</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Transaction amount in VND.</summary>
    public decimal AmountVnd { get; set; }

    /// <summary>Authorization outcome.</summary>
    public PaymentResultStatus PaymentStatus { get; set; } = PaymentResultStatus.Pending;

    /// <summary>External gateway transaction reference.</summary>
    public string? GatewayReference { get; set; }

    /// <summary>PCI-safe masked token or QR payload metadata (never raw card PAN/CVV).</summary>
    public string? MaskedDetail { get; set; }

    /// <summary>Timestamp when processed.</summary>
    public DateTime ProcessedAt { get; set; } = DateTime.Now;

    /// <summary>Builds the display payload or reader prompt metadata prior to gateway submission.</summary>
    public abstract string BuildPaymentInstruction(string transactionCode);
}

/// <summary>
/// Represents an EMV chip or NFC contactless bank card payment.
/// Never stores raw PAN or CVV; stores only a masked token.
/// </summary>
public class CardPayment : Payment
{
    /// <inheritdoc />
    public override string PaymentType => "CardPayment";

    /// <summary>Card entry method ("NFC-TAP" or "EMV-CHIP").</summary>
    public string EntryMode { get; set; } = "NFC-TAP";

    /// <summary>Masked last-4 card digits from P2PE reader token.</summary>
    public string MaskedPan { get; set; } = "****-4242";

    public CardPayment()
    {
        ProviderName = "Visa/Mastercard";
        MaskedDetail = "NFC-TAP ****-4242";
    }

    /// <inheritdoc />
    public override string BuildPaymentInstruction(string transactionCode) =>
        $"EMV/NFC Reader Active • Ref {transactionCode} • Amount {AmountVnd:N0} VND";
}

/// <summary>
/// Represents an interbank dynamic QR code payment (Napas 24/7 VietQR).
/// </summary>
public class QRPayment : Payment
{
    /// <inheritdoc />
    public override string PaymentType => "QRPayment";

    /// <summary>Dynamic VietQR payload string encoded into the displayed 2D QR image.</summary>
    public string QrPayload { get; set; } = string.Empty;

    public QRPayment()
    {
        ProviderName = "VietQR-Bank";
    }

    /// <inheritdoc />
    public override string BuildPaymentInstruction(string transactionCode)
    {
        QrPayload = $"00020101021238570010A000000727012700069704360113TVMBT01{transactionCode}530370454{AmountVnd:0}5802VN";
        MaskedDetail = $"VIETQR:{transactionCode}";
        return QrPayload;
    }
}

/// <summary>
/// Represents a Digital Wallet QR payment (MoMo, VNPay, or ZaloPay).
/// </summary>
public class WalletPayment : Payment
{
    /// <inheritdoc />
    public override string PaymentType => "WalletPayment";

    /// <summary>Dynamic wallet deep-link / QR content scanned by the passenger's phone.</summary>
    public string WalletQrPayload { get; set; } = string.Empty;

    public WalletPayment(string walletProvider = "MoMo")
    {
        ProviderName = walletProvider is "MoMo" or "VNPay" or "ZaloPay" ? walletProvider : "MoMo";
    }

    /// <inheritdoc />
    public override string BuildPaymentInstruction(string transactionCode)
    {
        WalletQrPayload = $"{ProviderName.ToUpperInvariant()}://pay?merchant=HCMC_METRO&ref={transactionCode}&amt={AmountVnd:0}";
        MaskedDetail = $"WALLET-{ProviderName.ToUpperInvariant()}:{transactionCode}";
        return WalletQrPayload;
    }
}
