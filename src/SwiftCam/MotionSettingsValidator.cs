using Microsoft.Extensions.Options;

namespace SwiftCam;

/// <summary>
/// Validates <see cref="MotionSettings"/> values are within acceptable ranges.
/// </summary>
public class MotionSettingsValidator : IValidateOptions<MotionSettings>
{
    public ValidateOptionsResult Validate(string? name, MotionSettings options)
    {
        var failures = new List<string>();

        if (options.Threshold < 0.1 || options.Threshold > 100.0)
            failures.Add($"Threshold must be between 0.1 and 100.0, got {options.Threshold}.");

        if (options.CooldownSeconds < 0 || options.CooldownSeconds > 86400)
            failures.Add($"CooldownSeconds must be between 0 and 86400, got {options.CooldownSeconds}.");

        if (string.IsNullOrWhiteSpace(options.CaptureDirectory))
            failures.Add("CaptureDirectory must not be empty.");

        if (options.PixelTolerance < 1 || options.PixelTolerance > 255)
            failures.Add($"PixelTolerance must be between 1 and 255, got {options.PixelTolerance}.");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
