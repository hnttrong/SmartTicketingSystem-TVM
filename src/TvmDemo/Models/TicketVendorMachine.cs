namespace TvmDemo.Models;

/// <summary>
/// Represents the operational state of the Ticket Vendor Machine (aligned with M3's State Chart Diagram).
/// </summary>
public enum TvmState
{
    Idle,
    SelectingDestination,
    SelectingPayment,
    AwaitingPayment,
    PaymentConfirmed,
    Printing,
    Completed,
    Cancelled,
    Failed,
    OutOfService
}

/// <summary>
/// Domain entity representing the physical Ticket Vendor Machine (TVM) kiosk unit.
/// Tracks operational state, origin station, paper inventory, and database link health.
/// </summary>
public class TicketVendorMachine
{
    /// <summary>Gets or sets the unique kiosk identifier (e.g., TVM-BT-01).</summary>
    public string MachineCode { get; set; } = "TVM-BT-01";

    /// <summary>Gets or sets the English name of the station where this TVM is installed.</summary>
    public string OriginStationEn { get; set; } = "Ben Thanh Station";

    /// <summary>Gets or sets the Vietnamese name of the station where this TVM is installed.</summary>
    public string OriginStationVi { get; set; } = "Ga Bến Thành";

    /// <summary>Gets the current state-machine status of the kiosk.</summary>
    public TvmState CurrentState { get; private set; } = TvmState.Idle;

    /// <summary>Gets or sets the remaining thermal paper tickets before Out-of-Service lock.</summary>
    public int RemainingPaperSheets { get; set; } = 200;

    /// <summary>Gets or sets whether the SQL Server repository is currently reachable.</summary>
    public bool IsDatabaseOnline { get; set; } = true;

    /// <summary>Gets or sets the diagnostic error code/reason when the machine enters OutOfService.</summary>
    public string? OutOfServiceReason { get; set; }

    /// <summary>
    /// Transitions the kiosk to a new operational state.
    /// </summary>
    /// <param name="nextState">Target <see cref="TvmState"/>.</param>
    /// <param name="reason">Optional diagnostic message if transitioning to OutOfService or Failed.</param>
    public void TransitionTo(TvmState nextState, string? reason = null)
    {
        CurrentState = nextState;
        if (nextState == TvmState.OutOfService || nextState == TvmState.Failed)
        {
            OutOfServiceReason = reason;
        }
        else if (nextState == TvmState.Idle)
        {
            OutOfServiceReason = null;
        }
    }

    /// <summary>
    /// Performs a pre-transaction hardware and connectivity readiness check.
    /// </summary>
    /// <param name="requiredTickets">Number of paper tickets needed (1-5).</param>
    /// <param name="faultReason">Outputs a human-readable fault message if not operational.</param>
    /// <returns><c>true</c> if the TVM can accept a purchase; otherwise <c>false</c>.</returns>
    public bool CanAcceptTransaction(int requiredTickets, out string faultReason)
    {
        if (!IsDatabaseOnline)
        {
            faultReason = "Database server connection unavailable (SQL Server unreachable).";
            return false;
        }

        if (RemainingPaperSheets < requiredTickets)
        {
            faultReason = $"Thermal printer out of paper ({RemainingPaperSheets} sheet(s) remaining).";
            return false;
        }

        faultReason = string.Empty;
        return true;
    }
}
