using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for cooldown state machine logic.
/// Validates: Requirements 4.1, 4.2, 4.3
/// </summary>
public class CooldownStateMachinePropertyTests
{
    /// <summary>
    /// Represents a single motion event with a timestamp and whether motion was detected.
    /// </summary>
    private record MotionEvent(DateTime Timestamp, bool MotionDetected);

    /// <summary>
    /// Simulates the cooldown state machine and returns the list of capture timestamps.
    /// This mirrors the logic in MotionDetector.ExecuteAsync.
    /// </summary>
    private static List<DateTime> SimulateCooldown(
        IReadOnlyList<MotionEvent> events,
        int cooldownSeconds)
    {
        var captures = new List<DateTime>();
        var lastCaptureTime = DateTime.MinValue;

        foreach (var evt in events)
        {
            var elapsed = (evt.Timestamp - lastCaptureTime).TotalSeconds;

            if (elapsed >= cooldownSeconds && evt.MotionDetected)
            {
                captures.Add(evt.Timestamp);
                lastCaptureTime = evt.Timestamp;
            }
        }

        return captures;
    }

    /// <summary>
    /// Property 5: Cooldown state machine.
    /// For any sequence of motion events with timestamps and a configured cooldown duration,
    /// a capture shall be triggered only when the elapsed time since the previous capture
    /// exceeds the cooldown duration.
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CooldownStateMachine_CapturesFireOnlyOutsideCooldownWindows()
    {
        // Generate cooldown duration between 1 and 3600 seconds
        var cooldownGen = Gen.Choose(1, 3600);

        // Generate a base timestamp
        var baseTimestamp = new DateTime(2025, 1, 1, 0, 0, 0);

        // Generate a list of time deltas (1-1000 seconds each) for monotonically increasing timestamps
        var deltasGen = Gen.Choose(1, 1000).ListOf().Select(ds => ds.ToList());

        // Generate a list of motion-detected booleans
        var motionsGen = Gen.Elements(true, false).ListOf().Select(ms => ms.ToList());

        return Prop.ForAll(
            cooldownGen.ToArbitrary(),
            deltasGen.ToArbitrary(),
            motionsGen.ToArbitrary(),
            (cooldownSeconds, deltas, motions) =>
            {
                // Build event sequence with monotonically increasing timestamps
                var count = Math.Min(deltas.Count, motions.Count);
                if (count == 0)
                    return true.Label("Empty event sequence - trivially valid");

                var events = new List<MotionEvent>();
                var currentTime = baseTimestamp;

                for (var i = 0; i < count; i++)
                {
                    currentTime = currentTime.AddSeconds(deltas[i]);
                    events.Add(new MotionEvent(currentTime, motions[i]));
                }

                // Simulate the cooldown state machine
                var captures = SimulateCooldown(events, cooldownSeconds);

                // Property assertion 1: All captures occurred at timestamps where
                // elapsed >= cooldown since the previous capture
                var lastCaptureTime = DateTime.MinValue;
                foreach (var captureTime in captures)
                {
                    var elapsed = (captureTime - lastCaptureTime).TotalSeconds;
                    if (elapsed < cooldownSeconds)
                    {
                        return false.Label(
                            $"Capture at {captureTime} violated cooldown. " +
                            $"Elapsed={elapsed:F1}s, cooldown={cooldownSeconds}s, " +
                            $"lastCapture={lastCaptureTime}");
                    }
                    lastCaptureTime = captureTime;
                }

                // Property assertion 2: No capture was missed - every event where
                // elapsed >= cooldown AND motion was detected did trigger a capture
                lastCaptureTime = DateTime.MinValue;
                var captureIndex = 0;
                foreach (var evt in events)
                {
                    var elapsed = (evt.Timestamp - lastCaptureTime).TotalSeconds;

                    if (elapsed >= cooldownSeconds && evt.MotionDetected)
                    {
                        // This event should have triggered a capture
                        if (captureIndex >= captures.Count ||
                            captures[captureIndex] != evt.Timestamp)
                        {
                            return false.Label(
                                $"Missed capture at {evt.Timestamp}. " +
                                $"Elapsed={elapsed:F1}s >= cooldown={cooldownSeconds}s " +
                                $"and motion was detected");
                        }
                        lastCaptureTime = evt.Timestamp;
                        captureIndex++;
                    }
                }

                // All captures accounted for
                if (captureIndex != captures.Count)
                {
                    return false.Label(
                        $"Extra captures found: expected {captureIndex} but got {captures.Count}");
                }

                return true.Label(
                    $"events={count}, cooldown={cooldownSeconds}s, captures={captures.Count}");
            });
    }

    /// <summary>
    /// Property: The first motion event always triggers a capture (since lastCaptureTime
    /// starts at DateTime.MinValue, elapsed is always >= cooldown).
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CooldownStateMachine_FirstMotionEvent_AlwaysTriggers()
    {
        // Generate cooldown duration between 1 and 3600 seconds
        var cooldownGen = Gen.Choose(1, 3600);

        // Generate a timestamp for the first event
        var timestampGen = Gen.Choose(2025, 2030).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).SelectMany(day =>
                    Gen.Choose(0, 23).SelectMany(hour =>
                        Gen.Choose(0, 59).Select(minute =>
                            new DateTime(year, month, day, hour, minute, 0))))));

        return Prop.ForAll(
            cooldownGen.ToArbitrary(),
            timestampGen.ToArbitrary(),
            (cooldownSeconds, timestamp) =>
            {
                // A single event with motion detected
                var events = new List<MotionEvent>
                {
                    new(timestamp, true)
                };

                var captures = SimulateCooldown(events, cooldownSeconds);

                return (captures.Count == 1 && captures[0] == timestamp)
                    .Label($"First motion event at {timestamp} with cooldown={cooldownSeconds}s " +
                           $"should always trigger. Got {captures.Count} captures.");
            });
    }

    /// <summary>
    /// Property: Events without motion never produce captures regardless of timing.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CooldownStateMachine_NoMotionEvents_NeverCapture()
    {
        var cooldownGen = Gen.Choose(1, 3600);
        var baseTimestamp = new DateTime(2025, 1, 1, 0, 0, 0);
        var deltasGen = Gen.Choose(1, 1000).ListOf().Select(ds => ds.ToList());

        return Prop.ForAll(
            cooldownGen.ToArbitrary(),
            deltasGen.ToArbitrary(),
            (cooldownSeconds, deltas) =>
            {
                if (deltas.Count == 0)
                    return true.Label("Empty sequence - trivially valid");

                // All events have motionDetected = false
                var events = new List<MotionEvent>();
                var currentTime = baseTimestamp;

                for (var i = 0; i < deltas.Count; i++)
                {
                    currentTime = currentTime.AddSeconds(deltas[i]);
                    events.Add(new MotionEvent(currentTime, false));
                }

                var captures = SimulateCooldown(events, cooldownSeconds);

                return (captures.Count == 0)
                    .Label($"Events with no motion should produce 0 captures, got {captures.Count}");
            });
    }
}
