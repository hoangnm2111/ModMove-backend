namespace ModMove.Domain.Services;

/// <summary>Great-circle distance between two WGS84 coordinates (GB-08, DECISIONS B5, cash collection radius).</summary>
public static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0;

    public static double HaversineKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        var dLat = ToRadians((double)(lat2 - lat1));
        var dLon = ToRadians((double)(lon2 - lon1));
        var rLat1 = ToRadians((double)lat1);
        var rLat2 = ToRadians((double)lat2);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(rLat1) * Math.Cos(rLat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}

