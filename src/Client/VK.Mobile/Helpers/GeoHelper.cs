namespace VK.Mobile.Helpers;

/// <summary>
/// Shared geographic calculations for the mobile client.
/// </summary>
internal static class GeoHelper
{
    private const double EarthRadiusKm = 6371;

    /// <summary>
    /// Returns the great-circle distance in kilometres between two points
    /// using the Haversine formula.
    /// </summary>
    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    /// <summary>
    /// Returns the great-circle distance in metres between two points.
    /// </summary>
    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        => HaversineKm(lat1, lon1, lat2, lon2) * 1000;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
