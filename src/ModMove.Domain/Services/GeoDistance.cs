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

    /// <summary>
    /// Computes an inclusive lat/lon bounding box around (<paramref name="lat"/>, <paramref name="lon"/>)
    /// that covers every point within <paramref name="radiusKm"/> (with a small margin for rounding).
    /// </summary>
    public static (decimal MinLat, decimal MaxLat, decimal MinLon, decimal MaxLon) BoundingBox(
        decimal lat, decimal lon, double radiusKm)
    {
        var paddedKm = Math.Max(0, radiusKm) * 1.01;
        var latDelta = (paddedKm / EarthRadiusKm) * (180.0 / Math.PI);
        var cosLat = Math.Cos(ToRadians((double)lat));
        var lonDelta = cosLat > 1e-6 ? latDelta / cosLat : 180.0;

        var minLat = (decimal)Math.Clamp((double)lat - latDelta, -90.0, 90.0);
        var maxLat = (decimal)Math.Clamp((double)lat + latDelta, -90.0, 90.0);
        var minLon = (decimal)Math.Clamp((double)lon - lonDelta, -180.0, 180.0);
        var maxLon = (decimal)Math.Clamp((double)lon + lonDelta, -180.0, 180.0);
        return (minLat, maxLat, minLon, maxLon);
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}

