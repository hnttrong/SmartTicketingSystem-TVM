namespace TvmDemo.Models;

/// <summary>
/// Domain class responsible for fare calculation and validation for a selected destination and ticket quantity.
/// </summary>
public class Fare
{
    /// <summary>Minimum tickets allowed in a single transaction.</summary>
    public const int MinQuantity = 1;

    /// <summary>Maximum tickets allowed in a single transaction.</summary>
    public const int MaxQuantity = 5;

    /// <summary>Associated destination station.</summary>
    public Destination Destination { get; }

    /// <summary>Unit fare per passenger in VND.</summary>
    public decimal UnitFareVnd => Destination.UnitFareVnd;

    /// <summary>Requested passenger ticket count (1–5).</summary>
    public int Quantity { get; private set; }

    /// <summary>Computed total fare in VND.</summary>
    public decimal TotalAmountVnd => CalculateTotal();

    /// <summary>
    /// Initializes a new <see cref="Fare"/> calculation for the given destination and quantity.
    /// </summary>
    public Fare(Destination destination, int quantity = 1)
    {
        Destination = destination ?? throw new ArgumentNullException(nameof(destination));
        SetQuantity(quantity);
    }

    /// <summary>
    /// Updates and validates the passenger ticket quantity (clamped or validated to 1–5).
    /// </summary>
    public void SetQuantity(int quantity)
    {
        if (quantity < MinQuantity || quantity > MaxQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                $"Ticket quantity must be between {MinQuantity} and {MaxQuantity}.");
        }

        Quantity = quantity;
    }

    /// <summary>
    /// Calculates the total payable fare in VND.
    /// </summary>
    public decimal CalculateTotal() => Destination.UnitFareVnd * Quantity;

    /// <summary>
    /// Formats a VND amount cleanly for kiosk display (e.g., "40,000 VND").
    /// </summary>
    public static string FormatVnd(decimal amountVnd) => $"{amountVnd:N0} VND";
}
