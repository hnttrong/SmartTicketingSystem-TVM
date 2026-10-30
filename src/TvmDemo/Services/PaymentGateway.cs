using TvmDemo.Models;

namespace TvmDemo.Services;

/// <summary>
/// Controls the deterministic result returned by <see cref="MockPaymentGateway"/> during demo runs.
/// </summary>
public enum GatewaySimulationMode
{
    Success,
    Declined,
    Timeout
}

/// <summary>
/// Value object representing the outcome of an external payment gateway authorization or callback wait.
/// </summary>
public record GatewayResponse(
    PaymentResultStatus Status,
    string? GatewayReference,
    string MessageEn,
    string MessageVi);

/// <summary>
/// Abstraction for external card acquirer and QR/Wallet payment gateways.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Authorizes a card tap or awaits a signed QR/Wallet gateway callback asynchronously.
    /// </summary>
    Task<GatewayResponse> AuthorizeAsync(Payment payment, string transactionCode, CancellationToken cancellationToken);
}

/// <summary>
/// Shared team base class <c>PaymentGateway</c> (aligns with M3's Class &amp; Sequence Diagrams)
/// implementing <see cref="IPaymentGateway"/>.
/// </summary>
public abstract class PaymentGateway : IPaymentGateway
{
    /// <inheritdoc />
    public abstract Task<GatewayResponse> AuthorizeAsync(
        Payment payment,
        string transactionCode,
        CancellationToken cancellationToken);
}

/// <summary>
/// Simulated payment gateway used in the local desktop prototype (replaces live Visa/MoMo/VNPay/ZaloPay calls).
/// Provides a switchable outcome (<see cref="GatewaySimulationMode.Success"/>,
/// <see cref="GatewaySimulationMode.Declined"/>, <see cref="GatewaySimulationMode.Timeout"/>)
/// with non-blocking asynchronous network latency simulation.
/// </summary>
public class MockPaymentGateway : PaymentGateway
{
    /// <summary>Gets or sets the simulated result switch controlled from the demo toolbar.</summary>
    public GatewaySimulationMode SimulationMode { get; set; } = GatewaySimulationMode.Success;

    /// <summary>Gets or sets simulated network/callback latency in milliseconds (default 2,400 ms).</summary>
    public int SimulatedDelayMs { get; set; } = 2400;

    /// <inheritdoc />
    public override async Task<GatewayResponse> AuthorizeAsync(
        Payment payment,
        string transactionCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);

        // Non-blocking async wait simulating EMV tap or mobile QR scan + HMAC webhook callback
        int delay = SimulationMode == GatewaySimulationMode.Timeout
            ? Math.Max(SimulatedDelayMs, 3500)
            : SimulatedDelayMs;

        await Task.Delay(delay, cancellationToken);

        string suffix = Random.Shared.Next(100000, 999999).ToString();

        return SimulationMode switch
        {
            GatewaySimulationMode.Success => new GatewayResponse(
                PaymentResultStatus.Success,
                $"GW-{payment.ProviderName.ToUpperInvariant().Replace('/', '-')}-{suffix}",
                "Payment authorized and verified.",
                "Thanh toán đã được xác thực thành công."),

            GatewaySimulationMode.Declined => new GatewayResponse(
                PaymentResultStatus.Declined,
                $"GW-DECLINE-{suffix}",
                "Error E-402: Issuer or wallet declined authorization (insufficient funds or card limit). You were NOT charged.",
                "Mã lỗi E-402: Ngân hàng hoặc ví từ chối giao dịch (không đủ số dư hoặc vượt hạn mức). Bạn CHƯA bị trừ tiền."),

            GatewaySimulationMode.Timeout => new GatewayResponse(
                PaymentResultStatus.Timeout,
                null,
                "Gateway callback timed out before confirmation. QR/session invalidated — you were NOT charged.",
                "Hết thời gian chờ phản hồi từ cổng thanh toán. Phiên/mã QR đã hủy — bạn CHƯA bị trừ tiền."),

            _ => throw new InvalidOperationException($"Unsupported simulation mode: {SimulationMode}")
        };
    }
}
