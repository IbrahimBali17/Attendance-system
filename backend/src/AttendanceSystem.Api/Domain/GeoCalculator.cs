namespace AttendanceSystem.Api.Domain;

/// <summary>Pure, side-effect-free geo maths (Haversine).</summary>
public static class GeoCalculator
{
    public const double EarthRadiusMeters = 6_371_000;

    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    public static bool IsValidCoordinate(double? lat, double? lon) =>
        lat is >= -90 and <= 90 && lon is >= -180 and <= 180;

    public static bool IsWithinRadius(double distanceMeters, double radiusMeters) => distanceMeters <= radiusMeters;

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
