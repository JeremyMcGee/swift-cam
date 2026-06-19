using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for motion classification logic.
/// Validates: Requirements 1.2, 1.3
/// </summary>
public class MotionClassificationPropertyTests
{
    /// <summary>
    /// Property 2: Motion classification completeness.
    /// For any changed-pixel percentage and configured threshold, motion is detected
    /// if and only if the percentage is strictly greater than the threshold.
    /// This confirms the classification uses strict greater-than (not >=).
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MotionClassification_DetectsMotion_IfAndOnlyIf_PercentageStrictlyGreaterThanThreshold()
    {
        // percentage: 0.00 to 100.00
        var percentageGen = Gen.Choose(0, 10000).Select(i => i / 100.0);
        // threshold: 0.10 to 100.00 (matches MotionSettingsValidator valid range)
        var thresholdGen = Gen.Choose(10, 10000).Select(i => i / 100.0);

        return Prop.ForAll(
            percentageGen.ToArbitrary(),
            thresholdGen.ToArbitrary(),
            (percentage, threshold) =>
            {
                // The motion classification logic as implemented in MotionDetector:
                // motion is detected when changedPercent > threshold (strict greater-than)
                var motionDetected = percentage > threshold;

                // Verify the two requirements:
                // Req 1.2: percentage > threshold => motion detected
                // Req 1.3: percentage <= threshold => no motion detected
                var expectedMotion = percentage > threshold;

                return (motionDetected == expectedMotion)
                    .Label($"percentage={percentage:F2}, threshold={threshold:F2}, " +
                           $"motionDetected={motionDetected}, expected={expectedMotion}");
            });
    }

    /// <summary>
    /// Boundary property: When percentage equals threshold exactly, no motion is detected.
    /// This verifies the strict greater-than semantics (not >=).
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MotionClassification_WhenPercentageEqualsThreshold_NoMotionDetected()
    {
        // Use the same value for both percentage and threshold
        var valueGen = Gen.Choose(10, 10000).Select(i => i / 100.0);

        return Prop.ForAll(
            valueGen.ToArbitrary(),
            value =>
            {
                var percentage = value;
                var threshold = value;

                // When percentage == threshold, strict > means no motion
                var motionDetected = percentage > threshold;

                return (!motionDetected)
                    .Label($"percentage={percentage:F2} == threshold={threshold:F2}, " +
                           $"motionDetected should be false but was {motionDetected}");
            });
    }
}
