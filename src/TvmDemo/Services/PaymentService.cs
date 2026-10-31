using TvmDemo.Models;

namespace TvmDemo.Services;

/// <summary>
/// Domain application service orchestrating payment instrument creation and gateway authorization.
/// Matches the shared class name <c>PaymentService</c> in M3's Sequence, Class, and Deployment diagrams.
/// </summary>
public class PaymentService
{
    private readonly IPaymentGateway _paymentGateway;

    /// <summary>
    /// Initializes a new instance of <see cref="PaymentService"/>.
    /// </summary>
    public PaymentService(IPaymentGateway paymentGateway)
    {
        _paymentGateway = paymentGateway ?? throw new ArgumentNullException(nameof(paymentGateway));
    }

    /// <summary>
    /// Factory method creating the concrete <see cref="Payment"/> subclass for a transaction.
    /// </summary>
    public Payment CreatePayment(Transaction transaction, string? walletProvider = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        Payment payment = transaction.PaymentMode switch
        {
            PaymentModeType.CreditCard => new CardPayment(),
            PaymentModeType.BankQR => new QRPayment(),
            PaymentModeType.DigitalWallet => new WalletPayment(walletProvider ?? "MoMo"),
            _ => throw new ArgumentOutOfRangeException(nameof(transaction.PaymentMode))
        };

        payment.TransactionId = transaction.TransactionId;
        payment.AmountVnd = transaction.TotalAmountVnd;
        payment.PaymentStatus = PaymentResultStatus.Pending;
        payment.BuildPaymentInstruction(transaction.TransactionCode);
        return payment;
    }

    /// <summary>
    /// Executes payment authorization via <see cref="IPaymentGateway"/> and updates the <see cref="Payment"/> entity.
    /// Handles user cancellation cleanly via <paramref name="cancellationToken"/>.
    /// </summary>
    public async Task<GatewayResponse> ProcessPaymentAsync(
        Transaction transaction,
        Payment payment,
        CancellationToken cancellationToken)
    {
        try
        {
            GatewayResponse response = await _paymentGateway.AuthorizeAsync(
                payment,
                transaction.TransactionCode,
                cancellationToken);

            payment.PaymentStatus = response.Status;
            payment.GatewayReference = response.GatewayReference;
            payment.ProcessedAt = DateTime.Now;
            return response;
        }
        catch (OperationCanceledException)
        {
            payment.PaymentStatus = PaymentResultStatus.Cancelled;
            payment.ProcessedAt = DateTime.Now;
            return new GatewayResponse(
                PaymentResultStatus.Cancelled,
                null,
                "Payment cancelled before completion. You were NOT charged.",
                "Đã hủy thanh toán trước khi hoàn tất. Bạn CHƯA bị trừ tiền.");
        }
    }
}
