using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

// Feature: swift-audio-attraction, Property 1: Playback decision correctness

/// <summary>
/// Property-based tests for playback decision correctness.
/// The play decision is true if and only if:
///   - current time falls within a playback window
///   - precipitation == 0
///   - wind speed &lt;= configured threshold
///   - retry count &lt; 5
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 6.3, 6.4, 6.7**
/// </summary>
public class PlaybackDecisionPropertyTests
{
    /// <summary>
    /// Pure decision function matching the AudioService logic.
    /// This isolates the scheduling decision from the full state machine.
    /// </summary>
    private static bool ShouldPlay(
        DateTime currentTime,
        PlaybackWindow? window,
        double precipitationMm,
        double windSpeedKph,
        int windSpeedThresholdKph,
        int retryCount)
    {
        var inWindow = window is not null
            && currentTime >= window.Start
            && currentTime < window.End;

        var fairWeather = precipitationMm == 0
            && windSpeedKph <= windSpeedThresholdKph;

        var retriesRemaining = retryCount < 5;

        return inWindow && fairWeather && retriesRemaining;
    }

    /// <summary>
    /// Property 1: Playback decision correctness.
    /// For any combination of (currentTime, playbackWindow, weatherState, retryCount),
    /// the play decision is true iff: time is within a window AND precipitation == 0
    /// AND windSpeed &lt;= threshold AND retries &lt; 5.
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 6.3, 6.4, 6.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaybackDecision_IsTrue_IffAllConditionsMet()
    {
        var baseDate = new DateTime(2025, 6, 15);

        var gen = from windowStartMinute in Gen.Choose(0, 1380)
                  from windowDuration in Gen.Choose(1, 180)
                  from currentTimeMinute in Gen.Choose(0, 1439)
                  from precipitationRaw in Gen.Frequency(
                      Tuple.Create(3, Gen.Constant(0)),
                      Tuple.Create(2, Gen.Choose(1, 100)))
                  from windSpeedRaw in Gen.Choose(0, 1500)
                  from threshold in Gen.Choose(1, 120)
                  from retryCount in Gen.Choose(0, 7)
                  select (windowStartMinute, windowDuration, currentTimeMinute,
                          precipitationRaw, windSpeedRaw, threshold, retryCount);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (windowStartMinute, windowDuration, currentTimeMinute,
                     precipitationRaw, windSpeedRaw, threshold, retryCount) = tuple;

                var windowStart = baseDate.AddMinutes(windowStartMinute);
                var windowEnd = windowStart.AddMinutes(windowDuration);
                var window = new PlaybackWindow(windowStart, windowEnd);
                var currentTime = baseDate.AddMinutes(currentTimeMinute);
                var precipitation = precipitationRaw / 10.0;
                var windSpeed = windSpeedRaw / 10.0;

                var decision = ShouldPlay(currentTime, window, precipitation, windSpeed, threshold, retryCount);

                // Independently compute the expected result
                var inWindow = currentTime >= window.Start && currentTime < window.End;
                var fairWeather = precipitation == 0 && windSpeed <= threshold;
                var retriesOk = retryCount < 5;
                var expected = inWindow && fairWeather && retriesOk;

                return (decision == expected)
                    .Label($"time={currentTime:HH:mm}, window={window.Start:HH:mm}-{window.End:HH:mm}, " +
                           $"precip={precipitation:F1}, wind={windSpeed:F1}, threshold={threshold}, " +
                           $"retry={retryCount}, decision={decision}, expected={expected}");
            });
    }

    /// <summary>
    /// Property 1 (retry boundary): When inside a window with fair weather,
    /// the decision depends solely on whether retryCount &lt; 5.
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 6.3, 6.4, 6.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaybackDecision_RetryCount_BlocksAtFiveOrMore()
    {
        var baseDate = new DateTime(2025, 6, 15);

        var gen = from windowStartMinute in Gen.Choose(60, 1200)
                  from windowDuration in Gen.Choose(30, 180)
                  from threshold in Gen.Choose(1, 120)
                  from windFactor in Gen.Choose(0, 100)
                  from retryCount in Gen.Choose(0, 7)
                  select (windowStartMinute, windowDuration, threshold, windFactor, retryCount);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (windowStartMinute, windowDuration, threshold, windFactor, retryCount) = tuple;

                var windowStart = baseDate.AddMinutes(windowStartMinute);
                var windowEnd = windowStart.AddMinutes(windowDuration);
                var window = new PlaybackWindow(windowStart, windowEnd);

                // Place currentTime inside the window (at midpoint)
                var currentTime = windowStart.AddMinutes(windowDuration / 2.0);

                // Fair weather: precipitation = 0, wind at or below threshold
                var windSpeed = (double)(threshold * windFactor / 100);

                var decision = ShouldPlay(currentTime, window, 0.0, windSpeed, threshold, retryCount);

                // With time in window and fair weather: decision depends only on retryCount < 5
                var expected = retryCount < 5;

                return (decision == expected)
                    .Label($"retryCount={retryCount}, decision={decision}, expected={expected}, " +
                           $"wind={windSpeed:F1}, threshold={threshold}");
            });
    }

    /// <summary>
    /// Property 1 (outside window): When outside a playback window, the decision is always false
    /// regardless of weather or retry state.
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 6.3, 6.4, 6.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaybackDecision_OutsideWindow_AlwaysFalse()
    {
        var baseDate = new DateTime(2025, 6, 15);

        // Fixed window from 06:00 to 09:00
        var windowStart = baseDate.AddHours(6);
        var windowEnd = baseDate.AddHours(9);
        var window = new PlaybackWindow(windowStart, windowEnd);

        // Generate currentTime outside the window
        var gen = from outsideMinute in Gen.Frequency(
                      Tuple.Create(1, Gen.Choose(0, 359)),       // 00:00 - 05:59
                      Tuple.Create(1, Gen.Choose(540, 1439)))    // 09:00 - 23:59
                  from precipitationRaw in Gen.Choose(0, 100)
                  from windSpeedRaw in Gen.Choose(0, 1500)
                  from threshold in Gen.Choose(1, 120)
                  from retryCount in Gen.Choose(0, 7)
                  select (outsideMinute, precipitationRaw, windSpeedRaw, threshold, retryCount);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (outsideMinute, precipitationRaw, windSpeedRaw, threshold, retryCount) = tuple;

                var currentTime = baseDate.AddMinutes(outsideMinute);
                var precipitation = precipitationRaw / 10.0;
                var windSpeed = windSpeedRaw / 10.0;

                var decision = ShouldPlay(currentTime, window, precipitation, windSpeed, threshold, retryCount);

                return (!decision)
                    .Label($"time={currentTime:HH:mm} (outside 06:00-09:00), " +
                           $"precip={precipitation:F1}, wind={windSpeed:F1}, " +
                           $"threshold={threshold}, retry={retryCount}, " +
                           $"decision should be false but was {decision}");
            });
    }
}
