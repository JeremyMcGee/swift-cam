using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

// Feature: swift-audio-attraction, Property 4: Window overlap prevention

/// <summary>
/// Property-based tests for window overlap prevention logic.
/// When the evening window start overlaps with the morning window end,
/// the evening start is adjusted to equal the morning end.
/// **Validates: Requirements 4.4**
/// </summary>
public class WindowOverlapPropertyTests
{
    /// <summary>
    /// Property 4: Window overlap prevention.
    /// For any pair of morning and evening playback windows where the calculated
    /// evening start time is earlier than the morning end time, the adjusted evening
    /// start SHALL equal the morning end time, and the evening end SHALL remain unchanged.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverlappingWindows_EveningStartAdjustedToMorningEnd_EveningEndUnchanged()
    {
        // Strategy: generate civil twilight early and sunset late, then pick
        // morningDuration and eveningPreSunset so that overlap is guaranteed
        // but morning end stays before sunset (evening end).
        // Civil twilight: 04:00-05:00 (240-300 min)
        // Morning offset: 0 (keep it simple to guarantee overlap)
        // Sunset: 20:00-21:00 (1200-1260 min)
        // We compute the gap = sunset - civilTwilight (in minutes), then:
        //   morningDuration must be > (sunset - eveningPreSunset - civilTwilight) to cause overlap
        //   morningDuration must be < (sunset - civilTwilight) to keep morningEnd < eveningEnd (sunset)
        // Simplification: pick morningDuration between (sunset - civilTwilight - 60) and (sunset - civilTwilight - 1)
        //   and eveningPreSunset large enough that rawEveningStart < morningEnd
        var gen = from civilTwilightMinutes in Gen.Choose(240, 300) // 04:00-05:00
                  from sunsetMinutes in Gen.Choose(1200, 1260) // 20:00-21:00
                  let gap = sunsetMinutes - civilTwilightMinutes // always 900-1020
                  // morningDuration: large but less than gap (to keep morningEnd < sunset)
                  from morningDuration in Gen.Choose(gap - 120, gap - 1)
                  // eveningPreSunset: ensure rawEveningStart < morningEnd
                  // rawEveningStart = sunset - preSunset; morningEnd = civilTwilight + duration
                  // overlap when: sunset - preSunset < civilTwilight + duration
                  //            => preSunset > sunset - civilTwilight - duration = gap - duration
                  // Also keep preSunset <= 480 (valid range) and <= gap (can't go before midnight)
                  let minPreSunset = gap - morningDuration + 1
                  let maxPreSunset = Math.Min(480, gap)
                  from eveningPreSunset in Gen.Choose(minPreSunset, maxPreSunset)
                  let civilTwilight = new TimeOnly(0, 0).AddMinutes(civilTwilightMinutes)
                  let sunset = new TimeOnly(0, 0).AddMinutes(sunsetMinutes)
                  let date = new DateTime(2024, 6, 15)
                  select (civilTwilight, morningDuration, sunset, eveningPreSunset, date);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (civilTwilight, morningDuration, sunset, eveningPreSunset, date) = tuple;

                var solarTimes = new SolarTimes(civilTwilight, null, sunset);
                var settings = new AudioSettings
                {
                    MorningOffsetMinutes = 0,
                    MorningDurationMinutes = morningDuration,
                    EveningPreSunsetMinutes = eveningPreSunset
                };

                var expectedEveningEnd = date.Date + sunset.ToTimeSpan();

                var (morning, evening) = PlaybackWindowCalculator.Calculate(solarTimes, settings, date);

                // Morning window should always exist
                if (morning is null)
                {
                    return false.Label("Morning window should not be null");
                }

                // Evening window should exist (we constrained morningEnd < sunset)
                if (evening is null)
                {
                    return false.Label("Evening window should not be null when adjusted start < end");
                }

                var startCorrect = evening.Start == morning.End;
                var endCorrect = evening.End == expectedEveningEnd;

                return (startCorrect && endCorrect)
                    .Label($"evening.Start={evening.Start:HH:mm} should equal morning.End={morning.End:HH:mm}, " +
                           $"evening.End={evening.End:HH:mm} should equal sunset={expectedEveningEnd:HH:mm}");
            });
    }

    /// <summary>
    /// When overlap is so severe that adjusted evening start >= evening end,
    /// the evening window should be null (fully consumed).
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverlappingWindows_WhenFullyConsumed_EveningWindowIsNull()
    {
        // Generate parameters where morning window extends past or equals sunset,
        // fully consuming the evening window.
        // To ensure morningEnd >= sunset, we need morningDuration >= gap.
        // We use a tight time range: civil twilight 06:00-08:00, sunset 16:00-18:00
        // giving gap of 480-720, which fits within the max morningDuration of 720.
        var gen = from civilTwilightMinutes in Gen.Choose(360, 480) // 06:00-08:00
                  from sunsetMinutes in Gen.Choose(960, 1080) // 16:00-18:00
                  let gap = sunsetMinutes - civilTwilightMinutes // 480-720
                  // morningDuration at least gap to ensure morningEnd >= sunset (eveningEnd)
                  from morningDuration in Gen.Choose(gap, 720)
                  from eveningPreSunset in Gen.Choose(1, 60) // short evening window
                  let civilTwilight = new TimeOnly(0, 0).AddMinutes(civilTwilightMinutes)
                  let sunset = new TimeOnly(0, 0).AddMinutes(sunsetMinutes)
                  let date = new DateTime(2024, 6, 15)
                  select (civilTwilight, morningDuration, sunset, eveningPreSunset, date);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (civilTwilight, morningDuration, sunset, eveningPreSunset, date) = tuple;

                var solarTimes = new SolarTimes(civilTwilight, null, sunset);
                var settings = new AudioSettings
                {
                    MorningOffsetMinutes = 0,
                    MorningDurationMinutes = morningDuration,
                    EveningPreSunsetMinutes = eveningPreSunset
                };

                var (_, evening) = PlaybackWindowCalculator.Calculate(solarTimes, settings, date);

                return (evening is null)
                    .Label($"Evening window should be null when morning extends past sunset");
            });
    }
}
