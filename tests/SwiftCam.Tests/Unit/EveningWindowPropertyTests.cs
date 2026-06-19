// Feature: swift-audio-attraction, Property 3: Evening window calculation
using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for evening playback window calculation.
/// Validates: Requirements 4.1, 4.2
/// </summary>
public class EveningWindowPropertyTests
{
    /// <summary>
    /// Property 3: Evening window calculation.
    /// For any valid sunset time and EveningPreSunsetMinutes value,
    /// the calculated evening playback window SHALL have
    /// start = sunset - EveningPreSunsetMinutes and end = sunset.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EveningWindow_Start_Is_Sunset_Minus_PreSunsetMinutes_And_End_Is_Sunset()
    {
        // Generate random sunset time between 16:00 and 22:00
        var sunsetGen = Gen.Choose(16 * 60, 22 * 60 - 1)
            .Select(minutes => new TimeOnly(minutes / 60, minutes % 60));

        // Generate random EveningPreSunsetMinutes in [1, 480]
        var preSunsetMinutesGen = Gen.Choose(1, 480);

        // Generate a random date
        var dateGen = Gen.Choose(1, 365)
            .Select(dayOfYear => new DateTime(2024, 1, 1).AddDays(dayOfYear - 1));

        return Prop.ForAll(
            sunsetGen.ToArbitrary(),
            preSunsetMinutesGen.ToArbitrary(),
            dateGen.ToArbitrary(),
            (sunset, preSunsetMinutes, date) =>
            {
                // Create SolarTimes with only Sunset set (CivilTwilight null to isolate evening window)
                var solarTimes = new SolarTimes(
                    CivilTwilight: null,
                    Sunrise: null,
                    Sunset: sunset);

                var settings = new AudioSettings
                {
                    EveningPreSunsetMinutes = preSunsetMinutes
                };

                var (morning, evening) = PlaybackWindowCalculator.Calculate(solarTimes, settings, date);

                // Morning should be null since CivilTwilight is null
                var morningIsNull = morning is null;

                // Evening window should exist
                var eveningExists = evening is not null;

                if (!eveningExists)
                {
                    return false.Label("Evening window should not be null when sunset is provided");
                }

                var expectedEnd = date.Date + sunset.ToTimeSpan();
                var expectedStart = expectedEnd - TimeSpan.FromMinutes(preSunsetMinutes);

                var startCorrect = evening!.Start == expectedStart;
                var endCorrect = evening.End == expectedEnd;

                return (morningIsNull && startCorrect && endCorrect)
                    .Label($"sunset={sunset}, preSunsetMinutes={preSunsetMinutes}, date={date:yyyy-MM-dd}, " +
                           $"expectedStart={expectedStart:HH:mm}, actualStart={evening.Start:HH:mm}, " +
                           $"expectedEnd={expectedEnd:HH:mm}, actualEnd={evening.End:HH:mm}");
            });
    }
}
