namespace ModMove.Domain.Enums;

/// <summary>What <c>DriverProfile.UpdateLocation</c> did with a GPS point (DECISIONS B19, B21).</summary>
public enum LocationUpdateResult
{
    /// <summary>Saved as the driver's last position.</summary>
    Applied,

    /// <summary>Not newer than the saved position: kept out of it, may still be logged (backlog point).</summary>
    Stale,

    /// <summary>Accuracy worse than 100 m: dropped entirely.</summary>
    Inaccurate,
}
