using Microsoft.Data.SqlClient;
using TvmDemo.Data;
using TvmDemo.Models;
using TvmDemo.Services;

namespace TvmDemo.Controllers;

/// <summary>
/// High-level outcome of <see cref="TicketController.ExecutePaymentAndPrintAsync"/> mapped to UI screens.
/// </summary>
public enum PurchaseFlowOutcome
{
    TicketIssued_S05a,
    PaymentDeclined_S05b,
    Cancelled_S01,
    Timeout_S07,
    OutOfService_S08
}

/// <summary>
/// Result returned to the View after processing a ticket purchase attempt.
/// </summary>
public record PurchaseFlowResult(
    PurchaseFlowOutcome Outcome,
    string MessageEn,
    string MessageVi,
    bool WasPassengerCharged,
    Transaction? Transaction);

/// <summary>
/// MVC Controller coordinating the UC01 "Buy a Ticket" workflow across the View (<c>MainForm</c>),
/// Domain Models (<see cref="TicketVendorMachine"/>, <see cref="Destination"/>, <see cref="Fare"/>,
/// <see cref="Transaction"/>, <see cref="Payment"/>, <see cref="Ticket"/>), Services
/// (<see cref="PaymentService"/>, <see cref="TicketPrinter"/>), and <see cref="ITvmRepository"/>.
/// </summary>
public class TicketController
{
    public TicketVendorMachine Machine { get; }
    public ITvmRepository Repository { get; }
    public PaymentService PaymentService { get; }
    public MockPaymentGateway MockGateway { get; }
    public TicketPrinter TicketPrinter { get; }

    public List<Destination> CachedDestinations { get; private set; } = new();
    public Fare? CurrentFare { get; private set; }
    public PaymentModeType SelectedPaymentMode { get; private set; } = PaymentModeType.CreditCard;
    public string SelectedWalletProvider { get; private set; } = "MoMo";
    public Transaction? ActiveTransaction { get; private set; }
    public Payment? ActivePayment { get; private set; }

    public TicketController(
        TicketVendorMachine machine,
        ITvmRepository repository,
        PaymentService paymentService,
        MockPaymentGateway mockGateway,
        TicketPrinter ticketPrinter)
    {
        Machine = machine ?? throw new ArgumentNullException(nameof(machine));
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        PaymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        MockGateway = mockGateway ?? throw new ArgumentNullException(nameof(mockGateway));
        TicketPrinter = ticketPrinter ?? throw new ArgumentNullException(nameof(ticketPrinter));
    }

    /// <summary>
    /// Checks SQL Server connectivity and loads the active HCMC Metro Line 1 station menu.
    /// Transitions machine to <see cref="TvmState.OutOfService"/> cleanly if DB is unavailable.
    /// </summary>
    public async Task<(bool Ok, string? ErrorMessage)> InitializeOrRefreshAsync()
    {
        var (online, err) = await Repository.CheckDatabaseHealthAsync();
        Machine.IsDatabaseOnline = online;

        if (!online)
        {
            Machine.TransitionTo(TvmState.OutOfService, err);
            return (false, err);
        }

        try
        {
            CachedDestinations = await Repository.GetActiveDestinationsAsync();
            if (CachedDestinations.Count == 0)
            {
                string msg = "No active stations found in dbo.Destination.";
                Machine.IsDatabaseOnline = false;
                Machine.TransitionTo(TvmState.OutOfService, msg);
                return (false, msg);
            }

            if (!Machine.CanAcceptTransaction(1, out string hwFault))
            {
                Machine.TransitionTo(TvmState.OutOfService, hwFault);
                return (false, hwFault);
            }

            Machine.TransitionTo(TvmState.Idle);
            return (true, null);
        }
        catch (Exception ex)
        {
            Machine.IsDatabaseOnline = false;
            Machine.TransitionTo(TvmState.OutOfService, ex.Message);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// UC01 Step 1: Passenger presses Start on S01.
    /// </summary>
    public bool TryStartSession(out string faultMessage)
    {
        ActiveTransaction = null;
        ActivePayment = null;

        if (!Machine.CanAcceptTransaction(1, out faultMessage))
        {
            Machine.TransitionTo(TvmState.OutOfService, faultMessage);
            return false;
        }

        if (CachedDestinations.Count > 0)
        {
            CurrentFare = new Fare(CachedDestinations[^1], 1);
        }

        Machine.TransitionTo(TvmState.SelectingDestination);
        return true;
    }

    /// <summary>
    /// UC01 Step 2: Updates selected destination and ticket quantity (1–5) using <see cref="Fare"/>.
    /// </summary>
    public Fare SelectDestinationAndQuantity(Destination destination, int quantity)
    {
        CurrentFare = new Fare(destination, quantity);
        return CurrentFare;
    }

    /// <summary>
    /// Proceeds from S02 to S03 after verifying sufficient paper for the requested ticket quantity.
    /// </summary>
    public bool TryProceedToPaymentMode(out string faultMessage)
    {
        int qty = CurrentFare?.Quantity ?? 1;
        if (!Machine.CanAcceptTransaction(qty, out faultMessage))
        {
            Machine.TransitionTo(TvmState.OutOfService, faultMessage);
            return false;
        }

        Machine.TransitionTo(TvmState.SelectingPayment);
        return true;
    }

    /// <summary>
    /// UC01 Step 3: Sets the chosen payment mode and optional digital wallet provider.
    /// </summary>
    public void SelectPaymentMode(PaymentModeType mode, string walletProvider = "MoMo")
    {
        SelectedPaymentMode = mode;
        SelectedWalletProvider = walletProvider;
    }

    /// <summary>
    /// UC01 Step 4: Creates a <c>Pending</c> <see cref="Transaction"/> in SQL Server and prepares
    /// the concrete <see cref="Payment"/> object prior to showing S04a or S04b.
    /// </summary>
    public async Task<(bool Ok, string? ErrorMessage)> BeginPaymentStepAsync()
    {
        if (CurrentFare == null)
        {
            return (false, "No destination selected.");
        }

        try
        {
            string txnCode = $"TXN-{DateTime.Now:yyyyMMdd-HHmmss}-{Random.Shared.Next(10, 99)}";
            var txn = new Transaction
            {
                TransactionCode = txnCode,
                MachineCode = Machine.MachineCode,
                DestinationId = CurrentFare.Destination.DestinationId,
                Destination = CurrentFare.Destination,
                Quantity = CurrentFare.Quantity,
                UnitFareVnd = CurrentFare.UnitFareVnd,
                TotalAmountVnd = CurrentFare.TotalAmountVnd,
                PaymentMode = SelectedPaymentMode,
                Status = TransactionStatus.Pending
            };

            await Repository.CreatePendingTransactionAsync(txn);
            Payment payment = PaymentService.CreatePayment(txn, SelectedWalletProvider);
            txn.Payment = payment;

            ActiveTransaction = txn;
            ActivePayment = payment;
            Machine.TransitionTo(TvmState.AwaitingPayment);
            return (true, null);
        }
        catch (SqlException ex)
        {
            Machine.IsDatabaseOnline = false;
            string msg = $"Database error while creating transaction (SQL #{ex.Number}): {ex.Message}";
            Machine.TransitionTo(TvmState.OutOfService, msg);
            return (false, msg);
        }
        catch (Exception ex)
        {
            Machine.IsDatabaseOnline = false;
            string msg = $"Unexpected database error: {ex.Message}";
            Machine.TransitionTo(TvmState.OutOfService, msg);
            return (false, msg);
        }
    }

    /// <summary>
    /// UC01 Steps 4–6: Awaits card tap or QR gateway callback asynchronously, prints barcode tickets,
    /// and commits the final status (<c>Completed</c>, <c>Failed</c>, or <c>Cancelled</c>) to SQL Server.
    /// </summary>
    public async Task<PurchaseFlowResult> ExecutePaymentAndPrintAsync(CancellationToken cancellationToken)
    {
        if (ActiveTransaction == null || ActivePayment == null)
        {
            return new PurchaseFlowResult(
                PurchaseFlowOutcome.OutOfService_S08,
                "Invalid session state: no pending transaction.",
                "Lỗi phiên giao dịch: không tìm thấy giao dịch đang chờ.",
                false,
                null);
        }

        Transaction txn = ActiveTransaction;
        Payment payment = ActivePayment;

        try
        {
            GatewayResponse gw = await PaymentService.ProcessPaymentAsync(txn, payment, cancellationToken);

            switch (gw.Status)
            {
                case PaymentResultStatus.Success:
                {
                    Machine.TransitionTo(TvmState.PaymentConfirmed);
                    Machine.TransitionTo(TvmState.Printing);

                    PrintTicketResult printRes = TicketPrinter.PrintTickets(txn, Machine);
                    if (!printRes.Success)
                    {
                        string errReason = $"{printRes.ErrorMessage} (Payment {payment.GatewayReference} queued for auto-refund).";
                        txn.Status = TransactionStatus.Failed;
                        txn.FailureReason = errReason;
                        await Repository.FailOrCancelTransactionAsync(txn, payment, TransactionStatus.Failed, errReason);
                        Machine.TransitionTo(TvmState.OutOfService, errReason);

                        return new PurchaseFlowResult(
                            PurchaseFlowOutcome.OutOfService_S08,
                            $"{printRes.ErrorMessage} Reference #{txn.TransactionCode} recorded for automatic refund.",
                            $"Lỗi máy in hết giấy/kẹt giấy. Đã ghi nhận Mã #{txn.TransactionCode} để hoàn tiền tự động.",
                            WasPassengerCharged: true,
                            txn);
                    }

                    txn.Tickets = printRes.IssuedTickets;
                    txn.Status = TransactionStatus.Completed;
                    await Repository.CompleteTransactionWithTicketsAsync(txn, payment, printRes.IssuedTickets);
                    Machine.TransitionTo(TvmState.Completed);

                    return new PurchaseFlowResult(
                        PurchaseFlowOutcome.TicketIssued_S05a,
                        gw.MessageEn,
                        gw.MessageVi,
                        WasPassengerCharged: true,
                        txn);
                }

                case PaymentResultStatus.Declined:
                {
                    txn.Status = TransactionStatus.Failed;
                    txn.FailureReason = gw.MessageEn;
                    await Repository.FailOrCancelTransactionAsync(txn, payment, TransactionStatus.Failed, gw.MessageEn);
                    Machine.TransitionTo(TvmState.Failed, gw.MessageEn);

                    return new PurchaseFlowResult(
                        PurchaseFlowOutcome.PaymentDeclined_S05b,
                        gw.MessageEn,
                        gw.MessageVi,
                        WasPassengerCharged: false,
                        txn);
                }

                case PaymentResultStatus.Timeout:
                {
                    txn.Status = TransactionStatus.Failed;
                    txn.FailureReason = gw.MessageEn;
                    await Repository.FailOrCancelTransactionAsync(txn, payment, TransactionStatus.Failed, gw.MessageEn);
                    Machine.TransitionTo(TvmState.Failed, gw.MessageEn);

                    return new PurchaseFlowResult(
                        PurchaseFlowOutcome.Timeout_S07,
                        gw.MessageEn,
                        gw.MessageVi,
                        WasPassengerCharged: false,
                        txn);
                }

                default: // Cancelled
                {
                    txn.Status = TransactionStatus.Cancelled;
                    txn.FailureReason = gw.MessageEn;
                    await Repository.FailOrCancelTransactionAsync(txn, payment, TransactionStatus.Cancelled, gw.MessageEn);
                    Machine.TransitionTo(TvmState.Cancelled);

                    return new PurchaseFlowResult(
                        PurchaseFlowOutcome.Cancelled_S01,
                        gw.MessageEn,
                        gw.MessageVi,
                        WasPassengerCharged: false,
                        txn);
                }
            }
        }
        catch (Exception ex)
        {
            Machine.IsDatabaseOnline = false;
            string dbErr = $"Database failure during payment persistence: {ex.Message}";
            Machine.TransitionTo(TvmState.OutOfService, dbErr);

            return new PurchaseFlowResult(
                PurchaseFlowOutcome.OutOfService_S08,
                dbErr,
                $"Lỗi cơ sở dữ liệu khi lưu giao dịch: {ex.Message}",
                WasPassengerCharged: payment.PaymentStatus == PaymentResultStatus.Success,
                txn);
        }
    }

    /// <summary>
    /// Cancels an active session from S06 Cancel Confirmation (marks any Pending DB transaction as Cancelled).
    /// </summary>
    public async Task ConfirmUserCancelAsync()
    {
        if (ActiveTransaction != null && ActiveTransaction.Status == TransactionStatus.Pending)
        {
            try
            {
                if (ActivePayment != null)
                {
                    ActivePayment.PaymentStatus = PaymentResultStatus.Cancelled;
                }

                ActiveTransaction.Status = TransactionStatus.Cancelled;
                await Repository.FailOrCancelTransactionAsync(
                    ActiveTransaction,
                    ActivePayment,
                    TransactionStatus.Cancelled,
                    "Cancelled by passenger on confirmation screen S06 - Passenger not charged.");
            }
            catch
            {
                // Ignore if DB went offline mid-cancel; session resets safely
            }
        }

        ActiveTransaction = null;
        ActivePayment = null;
        Machine.TransitionTo(TvmState.Idle);
    }

    /// <summary>
    /// Handles countdown timer expiration on S04a/S04b if the timer hits 00:00 prior to gateway completion.
    /// </summary>
    public async Task HandleSessionCountdownExpiredAsync()
    {
        if (ActiveTransaction != null && ActiveTransaction.Status == TransactionStatus.Pending)
        {
            try
            {
                if (ActivePayment != null)
                {
                    ActivePayment.PaymentStatus = PaymentResultStatus.Timeout;
                }

                ActiveTransaction.Status = TransactionStatus.Failed;
                await Repository.FailOrCancelTransactionAsync(
                    ActiveTransaction,
                    ActivePayment,
                    TransactionStatus.Failed,
                    "Payment countdown expired (00:00) - Passenger not charged.");
            }
            catch
            {
                // Safe fallback
            }
        }

        Machine.TransitionTo(TvmState.Failed, "Session Timeout");
    }

    /// <summary>
    /// Queries recent joined transaction records for the Output Report <c>DataGridView</c>.
    /// </summary>
    public Task<List<TransactionReportRow>> LoadReportRowsAsync(int topN = 30, string? statusFilter = null) =>
        Repository.GetRecentTransactionsAsync(topN, statusFilter);
}
