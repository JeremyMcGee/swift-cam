using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

// Feature: swift-audio-attraction, Property 2: Morning window calculation

/// <summary>
/// Property-based tests for morning playback window calculation.
/// Validates: Requirements 3.1, 3.2
/// </summary>
public class MorningWindowPropertyTests
{
    /// <summary>
    /// Property 2: Morning window calculation.
    /// For any valid latitude, longitude, date (where civil twilight is determinable),
    /// morning offset, and morning duration, the calculated morning playback window SHALL
    /// have start = civil_twilight + MorningOffsetMinutes and end = start + MorningDurationMinutes.
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MorningWindow_Start_Equals_CivilTwilight_Plus_Offset_And_End_Equals_Start_Plus_Duration()
    {
        // Generate a tuple of (civilTwilight, offset, duration, date)
        var gen = from twilightMinutes in Gen.Choose(4 * 60, 8 * 60)
                  let civilTwilight = new TimeOnly(twilightMinutes / 60, twilightMinutes % 60)
                  from offset in Gen.Choose(-60, 240)
                  from duration in Gen.Choose(1, 720)
                  from dayOffset in Gen.Choose(0, 3649)
                  let date = new DateTime(2020, 1, 1).AddDays(dayOffset)
                  select (civilTwilight, offset, duration, date);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (civilTwilight, offset, duration, date) = tuple;

                var solarTimes = new SolarTimes(
                    CivilTwilight: civilTwilight,
                    Sunrise: new TimeOnly(6, 0),
                    Sunset: new TimeOnly(20, 0));

                var settings = new AudioSettings
                {
                    MorningOffsetMinutes = offset,
                    MorningDurationMinutes = duration
                };

                var (morning, _) = PlaybackWindowCalculator.Calculate(solarTimes, settings, date);

                // Morning window must not be null since CivilTwilight is provided
                var morningNotNull = morning is not null;

                if (!morningNotNull)
                {
                    return false.Label("Morning window was null despite CivilTwilight being set");
                }

                var expectedStart = date.Date
                    + civilTwilight.ToTimeSpan()
                    + TimeSpan.FromMinutes(offset);
                var expectedEnd = expectedStart + TimeSpan.FromMinutes(duration);

                var startCorrect = morning!.Start == expectedStart;
                var endCorrect = morning.End == expectedEnd;

                return (startCorrect && endCorrect)
                    .Label($"civilTwilight={civilTwilight}, offset={offset}, duration={duration}, date={date:yyyy-MM-dd}, " +
                           $"expectedStart={expectedStart:O}, actualStart={morning.Start:O}, " +
                           $"expectedEnd={expectedEnd:O}, actualEnd={morning.End:O}");
            });
    }
}
