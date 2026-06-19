using Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Unit tests for SolarCalculatorWrapper polar edge cases and PlaybackWindowCalculator
/// behaviour when solar times are null.
/// Validates: Requirements 5.6, 3.5, 4.5
/// </summary>
public class SolarCalculatorTests
{
    private readonly SolarCalculatorWrapper _calculator = new();

    #region SolarCalculatorWrapper — Normal Latitude

    [Fact]
    public void Calculate_NormalLatitude_ReturnsNonNullTimes()
    {
        // Cheltenham, UK on a typical mid-year date
        var result = _calculator.Calculate(51.9, -2.07, new DateTime(2024, 6, 15));

        Assert.NotNull(result.CivilTwilight);
        Assert.NotNull(result.Sunrise);
        Assert.NotNull(result.Sunset);
    }

    [Fact]
    public void Calculate_NormalLatitude_Winter_ReturnsNonNullTimes()
    {
        // Cheltenham, UK in winter — still has normal sunrise/sunset
        var result = _calculator.Calculate(51.9, -2.07, new DateTime(2024, 12, 21));

        Assert.NotNull(result.CivilTwilight);
        Assert.NotNull(result.Sunrise);
        Assert.NotNull(result.Sunset);
    }

    #endregion

    #region SolarCalculatorWrapper — Extreme Latitude (Polar Day / Polar Night)

    [Theory]
    [InlineData(70.0, 25.0, 2024, 6, 21)]   // Northern Norway, summer solstice (polar day)
    [InlineData(78.0, 15.0, 2024, 6, 21)]   // Svalbard, summer solstice (polar day)
    public void Calculate_ExtremeLatitude_Summer_ReturnsNullTimes(
        double latitude, double longitude, int year, int month, int day)
    {
        // At extreme northern latitudes during summer, the sun doesn't set (polar day)
        var result = _calculator.Calculate(latitude, longitude, new DateTime(year, month, day));

        // During polar day, all times should be null (IsPolarDay = true)
        Assert.Null(result.CivilTwilight);
        Assert.Null(result.Sunrise);
        Assert.Null(result.Sunset);
    }

    [Theory]
    [InlineData(70.0, 25.0, 2024, 12, 21)]  // Northern Norway, winter solstice (polar night)
    [InlineData(78.0, 15.0, 2024, 12, 21)]  // Svalbard, winter solstice (polar night)
    public void Calculate_ExtremeLatitude_Winter_ReturnsNullTimes(
        double latitude, double longitude, int year, int month, int day)
    {
        // At extreme northern latitudes during winter, the sun doesn't rise (polar night)
        var result = _calculator.Calculate(latitude, longitude, new DateTime(year, month, day));

        // During polar night, all times should be null (IsPolarNight = true)
        Assert.Null(result.CivilTwilight);
        Assert.Null(result.Sunrise);
        Assert.Null(result.Sunset);
    }

    #endregion

    #region PlaybackWindowCalculator — Null Solar Times (Polar Edge Cases)

    [Fact]
    public void Calculate_NullCivilTwilight_MorningWindowIsNull()
    {
        // Validates: Requirement 3.5
        var solarTimes = new SolarTimes(
            CivilTwilight: null,
            Sunrise: null,
            Sunset: new TimeOnly(20, 0));

        var settings = new AudioSettings();
        var date = new DateTime(2024, 6, 21);

        var (morning, evening) = PlaybackWindowCalculator.Calculate(solarTimes, settings, date);

        Assert.Null(morning);
        Assert.NotNull(evening);
    }

    [Fact]
    public void Calculate_NullSunset_EveningWindowIsNull()
    {
        // Validates: Requirement 4.5
        var solarTimes = new SolarTimes(
            CivilTwilight: new TimeOnly(4, 30),
            Sunrise: new TimeOnly(5, 0),
            Sunset: null);

        var settings = new AudioSettings();
        var date = new DateTime(2024, 6, 21);

        var (morning, evening) = PlaybackWindowCalculator.Calculate(solarTimes, settings, date);

        Assert.NotNull(morning);
        Assert.Null(evening);
    }

    [Fact]
    public void Calculate_AllNullSolarTimes_BothWindowsAreNull()
    {
        // Validates: Requirements 3.5, 4.5, 5.6
        var solarTimes = new SolarTimes(
            CivilTwilight: null,
            Sunrise: null,
            Sunset: null);

        var settings = new AudioSettings();
        var date = new DateTime(2024, 6, 21);

        var (morning, evening) = PlaybackWindowCalculator.Calculate(solarTimes, settings, date);

        Assert.Null(morning);
        Assert.Null(evening);
    }

    #endregion
}
