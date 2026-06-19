// Feature: swift-audio-attraction, Property 6: Weather suppression classification
using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for weather suppression classification.
/// Validates: Requirements 6.3, 6.4, 6.7
/// </summary>
public class WeatherSuppressionPropertyTests
{
    /// <summary>
    /// Determines whether weather conditions constitute a suppression condition.
    /// Suppression = precipitation > 0 OR windSpeed > threshold.
    /// </summary>
    private static bool IsWeatherSuppression(double precipitationMm, double windSpeedKph, int windSpeedThresholdKph) =>
        precipitationMm > 0 || windSpeedKph > windSpeedThresholdKph;

    /// <summary>
    /// Property 6: Weather suppression classification.
    /// For any random (precipitation, windSpeed, threshold) triple, suppression is true
    /// if and only if precipitation > 0 OR windSpeed > threshold.
    /// **Validates: Requirements 6.3, 6.4, 6.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WeatherSuppression_IsTrue_IfAndOnlyIf_PrecipitationOrHighWind()
    {
        // precipitation: 0.0 to 50.0 mm
        var precipitationGen = Gen.Choose(0, 5000).Select(i => i / 100.0);
        // windSpeed: 0.0 to 200.0 kph
        var windSpeedGen = Gen.Choose(0, 20000).Select(i => i / 100.0);
        // threshold: 1 to 120 kph (valid range per AudioSettings)
        var thresholdGen = Gen.Choose(1, 120);

        return Prop.ForAll(
            precipitationGen.ToArbitrary(),
            windSpeedGen.ToArbitrary(),
            thresholdGen.ToArbitrary(),
            (precipitation, windSpeed, threshold) =>
            {
                var suppression = IsWeatherSuppression(precipitation, windSpeed, threshold);

                // Expected: suppression iff precipitation > 0 OR windSpeed > threshold
                var expected = precipitation > 0 || windSpeed > threshold;

                return (suppression == expected)
                    .Label($"precipitation={precipitation:F2}mm, windSpeed={windSpeed:F2}kph, " +
                           $"threshold={threshold}kph, suppression={suppression}, expected={expected}");
            });
    }

    /// <summary>
    /// Property 6 (negative case): When precipitation == 0 AND windSpeed <= threshold,
    /// the weather SHALL NEVER be classified as a suppression condition.
    /// **Validates: Requirements 6.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WeatherSuppression_NoPrecipitationAndLowWind_ShallNeverBeSuppression()
    {
        // windSpeed: 0.0 to 120.0 kph (capped at max threshold so we can generate <= threshold cases)
        var windSpeedGen = Gen.Choose(0, 12000).Select(i => i / 100.0);
        // threshold: 1 to 120 kph
        var thresholdGen = Gen.Choose(1, 120);

        return Prop.ForAll(
            windSpeedGen.ToArbitrary(),
            thresholdGen.ToArbitrary(),
            (windSpeed, threshold) =>
            {
                // Only test cases where windSpeed <= threshold (filter)
                var windBelowOrEqual = windSpeed <= threshold;

                var suppression = IsWeatherSuppression(
                    precipitationMm: 0,
                    windSpeedKph: windSpeed,
                    windSpeedThresholdKph: threshold);

                // When precipitation == 0 AND windSpeed <= threshold, must NOT be suppression
                return (!windBelowOrEqual || !suppression)
                    .Label($"precipitation=0mm, windSpeed={windSpeed:F2}kph, " +
                           $"threshold={threshold}kph, suppression={suppression} (should be false)");
            });
    }
}
