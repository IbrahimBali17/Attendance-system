using AttendanceSystem.Api.Domain;
using FluentAssertions;

namespace AttendanceSystem.Tests.Unit;

/// <summary>White-box tests (WB-xx) for the Haversine formula and the 30 m rule - every branch is exercised.</summary>
public class GeoCalculatorTests
{
    private const double Lat = 32.1030, Lon = 36.1820;
    private const double MetersPerDegree = 111_194.93;

    [Fact(DisplayName = "WB-01 same coordinates -> distance = 0 m")]
    public void WB01_SameCoordinates_ZeroDistance() =>
        GeoCalculator.DistanceMeters(Lat, Lon, Lat, Lon).Should().Be(0);

    [Fact(DisplayName = "WB-02 one degree of longitude on the equator ~ 111.19 km")]
    public void WB02_KnownDistance() =>
        GeoCalculator.DistanceMeters(0, 0, 0, 1).Should().BeApproximately(111_194.9, 5);

    [Fact(DisplayName = "WB-03 distance is symmetric")]
    public void WB03_Symmetric()
    {
        var ab = GeoCalculator.DistanceMeters(31.9539, 35.9106, 32.1030, 36.1820);
        var ba = GeoCalculator.DistanceMeters(32.1030, 36.1820, 31.9539, 35.9106);
        ab.Should().BeApproximately(ba, 1e-6);
    }

    [Fact(DisplayName = "WB-04 boundary: 29.9 m accepted, 30.1 m rejected")]
    public void WB04_Boundary()
    {
        var inside = GeoCalculator.DistanceMeters(Lat, Lon, Lat + 29.9 / MetersPerDegree, Lon);
        var outside = GeoCalculator.DistanceMeters(Lat, Lon, Lat + 30.1 / MetersPerDegree, Lon);
        GeoCalculator.IsWithinRadius(inside, 30).Should().BeTrue();
        GeoCalculator.IsWithinRadius(outside, 30).Should().BeFalse();
        GeoCalculator.IsWithinRadius(30, 30).Should().BeTrue();
    }

    [Fact(DisplayName = "WB-05 null GPS coordinates -> rejected")]
    public void WB05_NullCoordinates_Rejected()
    {
        GeoCalculator.IsValidCoordinate(null, null).Should().BeFalse();
        GeoCalculator.IsValidCoordinate(Lat, null).Should().BeFalse();
        GeoCalculator.IsValidCoordinate(null, Lon).Should().BeFalse();
    }

    [Theory(DisplayName = "WB-06 out-of-range / NaN coordinates -> rejected")]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    [InlineData(double.NaN, 0)]
    public void WB06_OutOfRange_Rejected(double lat, double lon) =>
        GeoCalculator.IsValidCoordinate(lat, lon).Should().BeFalse();

    [Fact(DisplayName = "WB-07 valid coordinates (including the extremes) -> accepted")]
    public void WB07_ValidCoordinates_Accepted()
    {
        GeoCalculator.IsValidCoordinate(Lat, Lon).Should().BeTrue();
        GeoCalculator.IsValidCoordinate(90, 180).Should().BeTrue();
        GeoCalculator.IsValidCoordinate(-90, -180).Should().BeTrue();
    }
}
