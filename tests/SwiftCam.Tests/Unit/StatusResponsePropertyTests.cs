// Feature: swift-audio-attraction, Property 8: Status response well-formedness
using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for status response well-formedness.
/// **Validates: Requirements 7.2, 7.3, 7.4, 7.5**
/// </summary>
public class StatusResponsePropertyTests
{
    private static readonly string[] ValidStates = ["Playing", "Stopped", "Suppressed", "Idle", "Error"];

    /// <summary>
    /// Property 8: Status response well-formedness.
    /// For any AudioState and PlaybackWindow combination, the constructed AudioStatusResponse
    /// has state in {"Playing","Stopped","Suppressed","Idle","Error"}, reason length ≤ 200,
    /// and window times in valid ISO 8601 format.
    /// **Validates: Requirements 7.2, 7.3, 7.4, 7.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StatusResponse_IsWellFormed_ForAnyStateAndWindow()
    {
        var audioStateGen = Gen.Elements(
            AudioState.Idle,
            AudioState.Playing,
            AudioState.Suppressed,
            AudioState.Stopped,
            AudioState.Error);

        // Generate reason strings of various lengths, including some > 200 chars
        var reasonGen = Gen.OneOf(
            Gen.Elements("Morning session", "Outside playback window", "Paused: rain detected", "Paused: high wind", "Audio file not found", ""),
            Arb.Generate<NonEmptyString>().Select(s => s.Get),
            Gen.Constant(new string('x', 250)));

        // Generate nullable PlaybackWindow values
        var dateTimeGen = Gen.Choose(2020, 2030).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).SelectMany(day =>
                    Gen.Choose(0, 23).SelectMany(hour =>
                        Gen.Choose(0, 59).Select(minute =>
                            new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc))))));

        var windowGen = dateTimeGen.SelectMany(start =>
            Gen.Choose(30, 240).Select(durationMinutes =>
                new PlaybackWindow(start, start.AddMinutes(durationMinutes))));

        var nullableWindowGen = Gen.OneOf(
            windowGen.Select<PlaybackWindow, PlaybackWindow?>(w => w),
            Gen.Constant<PlaybackWindow?>(null));

        // Combine currentWindow and nextWindow into a tuple to stay within ForAll's 4-arg limit
        var windowPairGen = nullableWindowGen.SelectMany(cw =>
            nullableWindowGen.Select(nw => (CurrentWindow: cw, NextWindow: nw)));

        return Prop.ForAll(
            audioStateGen.ToArbitrary(),
            reasonGen.ToArbitrary(),
            windowPairGen.ToArbitrary(),
            (state, reason, windows) =>
            {
                var currentWindow = windows.CurrentWindow;
                var nextWindow = windows.NextWindow;

                // Construct the response the same way the endpoint does
                var cappedReason = reason.Length > 200 ? reason[..200] : reason;

                var response = new AudioStatusResponse(
                    State: state.ToString(),
                    Reason: cappedReason,
                    CurrentWindowStart: currentWindow?.Start.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    CurrentWindowEnd: currentWindow?.End.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    NextWindowStart: nextWindow?.Start.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                // Assert 1: State is one of the valid values
                var stateValid = ValidStates.Contains(response.State);

                // Assert 2: Reason length ≤ 200 characters
                var reasonValid = response.Reason.Length <= 200;

                // Assert 3: Window times are either null or valid ISO 8601 (parseable by DateTime.TryParse)
                var windowStartValid = response.CurrentWindowStart is null
                    || DateTime.TryParse(response.CurrentWindowStart, out _);
                var windowEndValid = response.CurrentWindowEnd is null
                    || DateTime.TryParse(response.CurrentWindowEnd, out _);
                var nextWindowValid = response.NextWindowStart is null
                    || DateTime.TryParse(response.NextWindowStart, out _);

                return (stateValid && reasonValid && windowStartValid && windowEndValid && nextWindowValid)
                    .Label($"State={response.State} (valid={stateValid}), " +
                           $"ReasonLen={response.Reason.Length} (valid={reasonValid}), " +
                           $"WindowStart={response.CurrentWindowStart} (valid={windowStartValid}), " +
                           $"WindowEnd={response.CurrentWindowEnd} (valid={windowEndValid}), " +
                           $"NextWindow={response.NextWindowStart} (valid={nextWindowValid})");
            });
    }
}
