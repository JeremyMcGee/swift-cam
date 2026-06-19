// Feature: swift-audio-attraction, Property 7: Settings validation rejects invalid values
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Options;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for AudioSettingsValidator.
/// Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 5.4, 5.5
/// </summary>
public class AudioSettingsValidatorPropertyTests
{
    private readonly AudioSettingsValidator _validator = new();

    /// <summary>
    /// Creates a valid AudioSettings instance that passes all validation rules.
    /// </summary>
    private static AudioSettings CreateValidSettings() => new()
    {
        AudioFilePath = "audio/swift-call.mp3",
        Latitude = 51.9,
        Longitude = -2.07,
        MorningOffsetMinutes = 0,
        MorningDurationMinutes = 210,
        EveningPreSunsetMinutes = 150,
        WeatherPollIntervalMinutes = 15,
        WindSpeedThresholdKph = 40
    };

    /// <summary>
    /// Property 7: Settings validation rejects invalid values.
    /// For any AudioSettings instance where at least one field is outside its valid range,
    /// the validator SHALL return a failure result.
    /// **Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Validator_RejectsSettings_WithAtLeastOneInvalidField()
    {
        // Generate an index 0-7 indicating which field(s) to invalidate,
        // plus a random value used to produce an out-of-range value.
        var fieldIndexGen = Gen.Choose(0, 7);
        var invalidValueGen = Gen.Choose(1, 10000);
        var signGen = Gen.Elements(true, false);

        var invalidSettingsGen = from fieldIndex in fieldIndexGen
                                 from magnitude in invalidValueGen
                                 from negative in signGen
                                 select MakeInvalid(fieldIndex, magnitude, negative);

        return Prop.ForAll(
            invalidSettingsGen.ToArbitrary(),
            settings =>
            {
                var result = _validator.Validate(null, settings);

                return (result.Failed)
                    .Label($"Expected validation failure for settings: " +
                           $"AudioFilePath='{settings.AudioFilePath}', " +
                           $"Lat={settings.Latitude}, Lon={settings.Longitude}, " +
                           $"MorningOffset={settings.MorningOffsetMinutes}, " +
                           $"MorningDuration={settings.MorningDurationMinutes}, " +
                           $"EveningPreSunset={settings.EveningPreSunsetMinutes}, " +
                           $"WeatherPoll={settings.WeatherPollIntervalMinutes}, " +
                           $"WindThreshold={settings.WindSpeedThresholdKph}");
            });
    }

    /// <summary>
    /// Creates an AudioSettings with one field made invalid based on the field index.
    /// </summary>
    private static AudioSettings MakeInvalid(int fieldIndex, int magnitude, bool negative)
    {
        var settings = CreateValidSettings();

        switch (fieldIndex)
        {
            case 0: // AudioFilePath - empty or whitespace
                settings.AudioFilePath = magnitude % 3 == 0 ? "" : magnitude % 3 == 1 ? "   " : null!;
                break;
            case 1: // Latitude outside [-90, 90]
                settings.Latitude = negative ? -90.0 - magnitude : 90.0 + magnitude;
                break;
            case 2: // Longitude outside [-180, 180]
                settings.Longitude = negative ? -180.0 - magnitude : 180.0 + magnitude;
                break;
            case 3: // MorningOffsetMinutes outside [-60, 240]
                settings.MorningOffsetMinutes = negative ? -60 - magnitude : 240 + magnitude;
                break;
            case 4: // MorningDurationMinutes outside [1, 720]
                settings.MorningDurationMinutes = negative ? 1 - magnitude : 720 + magnitude;
                break;
            case 5: // EveningPreSunsetMinutes outside [1, 480]
                settings.EveningPreSunsetMinutes = negative ? 1 - magnitude : 480 + magnitude;
                break;
            case 6: // WeatherPollIntervalMinutes outside [1, 60]
                settings.WeatherPollIntervalMinutes = negative ? 1 - magnitude : 60 + magnitude;
                break;
            case 7: // WindSpeedThresholdKph outside [1, 120]
                settings.WindSpeedThresholdKph = negative ? 1 - magnitude : 120 + magnitude;
                break;
        }

        return settings;
    }

    /// <summary>
    /// Complementary property: valid settings are accepted by the validator.
    /// Ensures the generator strategy is correct by confirming the valid base case passes.
    /// **Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Validator_AcceptsSettings_WithAllFieldsInValidRange()
    {
        // Generate random but valid settings
        var validSettingsGen =
            from audioPath in Gen.Elements("audio/call.mp3", "sounds/swift.wav", "test.mp3")
            from lat in Gen.Choose(-9000, 9000).Select(i => i / 100.0)
            from lon in Gen.Choose(-18000, 18000).Select(i => i / 100.0)
            from morningOffset in Gen.Choose(-60, 240)
            from morningDuration in Gen.Choose(1, 720)
            from eveningPreSunset in Gen.Choose(1, 480)
            from weatherPoll in Gen.Choose(1, 60)
            from windThreshold in Gen.Choose(1, 120)
            select new AudioSettings
            {
                AudioFilePath = audioPath,
                Latitude = lat,
                Longitude = lon,
                MorningOffsetMinutes = morningOffset,
                MorningDurationMinutes = morningDuration,
                EveningPreSunsetMinutes = eveningPreSunset,
                WeatherPollIntervalMinutes = weatherPoll,
                WindSpeedThresholdKph = windThreshold
            };

        return Prop.ForAll(
            validSettingsGen.ToArbitrary(),
            settings =>
            {
                var result = _validator.Validate(null, settings);

                return (result.Succeeded)
                    .Label($"Expected validation success for settings: " +
                           $"AudioFilePath='{settings.AudioFilePath}', " +
                           $"Lat={settings.Latitude}, Lon={settings.Longitude}, " +
                           $"MorningOffset={settings.MorningOffsetMinutes}, " +
                           $"MorningDuration={settings.MorningDurationMinutes}, " +
                           $"EveningPreSunset={settings.EveningPreSunsetMinutes}, " +
                           $"WeatherPoll={settings.WeatherPollIntervalMinutes}, " +
                           $"WindThreshold={settings.WindSpeedThresholdKph}");
            });
    }
}
