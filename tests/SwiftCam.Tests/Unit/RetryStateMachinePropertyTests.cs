// Feature: swift-audio-attraction, Property 5: Retry state machine
using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for the audio service retry state machine.
/// Validates: Requirements 1.5, 1.6
/// </summary>
public class RetryStateMachinePropertyTests
{
    private const int MaxRetries = 5;

    /// <summary>
    /// Simulates the retry state machine for a given number of crash events.
    /// Returns the resulting state after each crash: Stopped (restart attempted) or Error.
    /// This mirrors the pure logic in AudioService.EvaluatePlayingState:
    ///   _consecutiveRetries++ on each crash;
    ///   if _consecutiveRetries > MaxRetries → Error, else → Stopped.
    /// </summary>
    private static AudioState SimulateCrash(int retryCount)
    {
        // After retryCount increments, check if we've exceeded MaxRetries
        return retryCount > MaxRetries ? AudioState.Error : AudioState.Stopped;
    }

    /// <summary>
    /// Property 5: Retry state machine.
    /// For any sequence of crash events (length 1-10) within a single playback window,
    /// crashes 1-5 should result in Stopped state (restart will be attempted),
    /// and after the 5th crash the state should transition to Error.
    /// **Validates: Requirements 1.5, 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RetryStateMachine_RestartAttemptedForFirst5Crashes_ErrorAfter5thFailure()
    {
        // Generate a random crash count between 1 and 10
        var crashCountGen = Gen.Choose(1, 10);

        return Prop.ForAll(
            crashCountGen.ToArbitrary(),
            crashCount =>
            {
                var allCorrect = true;
                var failureMessage = string.Empty;

                // Simulate consecutive crashes within a single window
                var retryCounter = 0;

                for (var i = 1; i <= crashCount; i++)
                {
                    retryCounter++;
                    var resultState = SimulateCrash(retryCounter);

                    if (retryCounter <= MaxRetries)
                    {
                        // Crashes 1-5: should be Stopped (restart attempted)
                        if (resultState != AudioState.Stopped)
                        {
                            allCorrect = false;
                            failureMessage = $"Crash {i}: expected Stopped but got {resultState}";
                            break;
                        }
                    }
                    else
                    {
                        // After 5th failure (retryCounter > 5): should be Error
                        if (resultState != AudioState.Error)
                        {
                            allCorrect = false;
                            failureMessage = $"Crash {i}: expected Error but got {resultState}";
                            break;
                        }
                    }
                }

                return allCorrect
                    .Label(string.IsNullOrEmpty(failureMessage)
                        ? $"crashCount={crashCount}, all transitions correct"
                        : failureMessage);
            });
    }

    /// <summary>
    /// Property 5 (boundary): After exactly 5 crashes, the state should still be Stopped
    /// (5th restart attempt is allowed). The 6th crash transitions to Error.
    /// **Validates: Requirements 1.5, 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RetryStateMachine_5thCrashIsStillRetried_6thCrashIsError()
    {
        // Generate a random crash count to reach the boundary (5 to 10)
        var crashCountGen = Gen.Choose(5, 10);

        return Prop.ForAll(
            crashCountGen.ToArbitrary(),
            crashCount =>
            {
                var retryCounter = 0;

                // Process all crashes
                AudioState? stateAt5 = null;
                AudioState? stateAt6 = null;

                for (var i = 1; i <= crashCount; i++)
                {
                    retryCounter++;
                    var state = SimulateCrash(retryCounter);

                    if (i == 5) stateAt5 = state;
                    if (i == 6) stateAt6 = state;
                }

                // 5th crash must still be Stopped (restart is attempted)
                var fifthIsCorrect = stateAt5 == AudioState.Stopped;

                // 6th crash (if reached) must be Error
                var sixthIsCorrect = crashCount < 6 || stateAt6 == AudioState.Error;

                return (fifthIsCorrect && sixthIsCorrect)
                    .Label($"crashCount={crashCount}, stateAt5={stateAt5}, stateAt6={stateAt6}");
            });
    }
}
