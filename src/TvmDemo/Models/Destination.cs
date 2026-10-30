namespace TvmDemo.Models;

/// <summary>
/// Represents a selectable transit station destination (MRT or Bus) on the TVM menu.
/// Maps directly to the <c>dbo.Destination</c> table in <c>TvmDemo</c>.
/// </summary>
public class Destination
{
    /// <summary>Database primary key.</summary>
    public int DestinationId { get; set; }

    /// <summary>Unique station code (e.g., M1-02 through M1-14).</summary>
    public string StationCode { get; set; } = string.Empty;

    /// <summary>Station name in English.</summary>
    public string StationNameEn { get; set; } = string.Empty;

    /// <summary>Station name in Vietnamese.</summary>
    public string StationNameVi { get; set; } = string.Empty;

    /// <summary>Transport line type ("MRT" or "Bus").</summary>
    public string LineType { get; set; } = "MRT";

    /// <summary>Fare zone number (1–5).</summary>
    public int ZoneNumber { get; set; }

    /// <summary>Route distance from Ben Thanh origin in kilometers.</summary>
    public decimal DistanceKm { get; set; }

    /// <summary>Single-journey adult fare in VND.</summary>
    public decimal UnitFareVnd { get; set; }

    /// <summary>Estimated travel time in minutes.</summary>
    public int EstimatedMinutes { get; set; }

    /// <summary>Indicates whether the station is currently open for ticket sales.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Returns the localized station display name.
    /// </summary>
    /// <param name="isVietnamese">True for Vietnamese, false for English.</param>
    public string GetDisplayName(bool isVietnamese) =>
        isVietnamese ? StationNameVi : StationNameEn;

    /// <inheritdoc />
    public override string ToString() => $"{StationCode} - {StationNameEn} ({UnitFareVnd:N0} VND)";
}
