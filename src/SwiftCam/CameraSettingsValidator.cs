using Microsoft.Extensions.Options;

namespace SwiftCam;

/// <summary>
/// Validates <see cref="CameraSettings"/> values are within hardware-supported ranges.
/// </summary>
public class CameraSettingsValidator : IValidateOptions<CameraSettings>
{
    public ValidateOptionsResult Validate(string? name, CameraSettings options)
    {
        var failures = new List<string>();

        if (options.Width < 160 || options.Width > 4056)
            failures.Add($"Width must be between 160 and 4056, got {options.Width}.");

        if (options.Height < 120 || options.Height > 3040)
            failures.Add($"Height must be between 120 and 3040, got {options.Height}.");

        if (options.Framerate < 1 || options.Framerate > 120)
            failures.Add($"Framerate must be between 1 and 120, got {options.Framerate}.");

        if (options.Quality < 1 || options.Quality > 100)
            failures.Add($"Quality must be between 1 and 100, got {options.Quality}.");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
