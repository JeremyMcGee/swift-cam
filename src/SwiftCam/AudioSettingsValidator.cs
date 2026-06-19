using Microsoft.Extensions.Options;

namespace SwiftCam;

/// <summary>
/// Validates <see cref="AudioSettings"/> values are within acceptable ranges.
/// </summary>
public class AudioSettingsValidator : IValidateOptions<AudioSettings>
{
    public ValidateOptionsResult Validate(string? name, AudioSettings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.AudioFilePath))
            failures.Add("AudioFilePath must not be empty.");

        if (options.Latitude < -90 || options.Latitude > 90)
            failures.Add($"Latitude must be between -90 and 90, got {options.Latitude}.");

        if (options.Longitude < -180 || options.Longitude > 180)
            failures.Add($"Longitude must be between -180 and 180, got {options.Longitude}.");

        if (options.MorningOffsetMinutes < -60 || options.MorningOffsetMinutes > 240)
            failures.Add($"MorningOffsetMinutes must be between -60 and 240, got {options.MorningOffsetMinutes}.");

        if (options.MorningDurationMinutes < 1 || options.MorningDurationMinutes > 720)
            failures.Add($"MorningDurationMinutes must be between 1 and 720, got {options.MorningDurationMinutes}.");

        if (options.EveningPreSunsetMinutes < 1 || options.EveningPreSunsetMinutes > 480)
            failures.Add($"EveningPreSunsetMinutes must be between 1 and 480, got {options.EveningPreSunsetMinutes}.");

        if (options.WeatherPollIntervalMinutes < 1 || options.WeatherPollIntervalMinutes > 60)
            failures.Add($"WeatherPollIntervalMinutes must be between 1 and 60, got {options.WeatherPollIntervalMinutes}.");

        if (options.WindSpeedThresholdKph < 1 || options.WindSpeedThresholdKph > 120)
            failures.Add($"WindSpeedThresholdKph must be between 1 and 120, got {options.WindSpeedThresholdKph}.");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
