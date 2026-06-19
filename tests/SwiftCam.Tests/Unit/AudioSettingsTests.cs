using Microsoft.Extensions.Options;
using Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Unit tests for AudioSettings default values and AudioSettingsValidator boundary checks.
/// Validates: Requirements 2.1, 2.2, 3.3, 3.4, 4.3, 5.1, 5.2, 6.2, 6.5, 9.1–9.6
/// </summary>
public class AudioSettingsTests
{
    #region Default Values

    /// <summary>
    /// Verifies AudioFilePath defaults to "audio/swift-call.mp3".
    /// Validates: Requirement 2.2
    /// </summary>
    [Fact]
    public void Defaults_AudioFilePath_IsSwiftCallMp3()
    {
        var settings = new AudioSettings();

        Assert.Equal("audio/swift-call.mp3", settings.AudioFilePath);
    }

    /// <summary>
    /// Verifies Latitude defaults to 51.9.
    /// Validates: Requirement 5.2
    /// </summary>
    [Fact]
    public void Defaults_Latitude_Is51Point9()
    {
        var settings = new AudioSettings();

        Assert.Equal(51.9, settings.Latitude);
    }

    /// <summary>
    /// Verifies Longitude defaults to -2.07.
    /// Validates: Requirement 5.2
    /// </summary>
    [Fact]
    public void Defaults_Longitude_IsNeg2Point07()
    {
        var settings = new AudioSettings();

        Assert.Equal(-2.07, settings.Longitude);
    }

    /// <summary>
    /// Verifies MorningOffsetMinutes defaults to 0.
    /// Validates: Requirement 3.4
    /// </summary>
    [Fact]
    public void Defaults_MorningOffsetMinutes_Is0()
    {
        var settings = new AudioSettings();

        Assert.Equal(0, settings.MorningOffsetMinutes);
    }

    /// <summary>
    /// Verifies MorningDurationMinutes defaults to 210.
    /// Validates: Requirement 3.3
    /// </summary>
    [Fact]
    public void Defaults_MorningDurationMinutes_Is210()
    {
        var settings = new AudioSettings();

        Assert.Equal(210, settings.MorningDurationMinutes);
    }

    /// <summary>
    /// Verifies EveningPreSunsetMinutes defaults to 150.
    /// Validates: Requirement 4.3
    /// </summary>
    [Fact]
    public void Defaults_EveningPreSunsetMinutes_Is150()
    {
        var settings = new AudioSettings();

        Assert.Equal(150, settings.EveningPreSunsetMinutes);
    }

    /// <summary>
    /// Verifies WeatherPollIntervalMinutes defaults to 15.
    /// Validates: Requirement 6.2
    /// </summary>
    [Fact]
    public void Defaults_WeatherPollIntervalMinutes_Is15()
    {
        var settings = new AudioSettings();

        Assert.Equal(15, settings.WeatherPollIntervalMinutes);
    }

    /// <summary>
    /// Verifies WindSpeedThresholdKph defaults to 40.
    /// Validates: Requirement 6.5
    /// </summary>
    [Fact]
    public void Defaults_WindSpeedThresholdKph_Is40()
    {
        var settings = new AudioSettings();

        Assert.Equal(40, settings.WindSpeedThresholdKph);
    }

    #endregion

    #region Validator – Latitude Boundaries

    /// <summary>
    /// Validates Latitude boundary values: below min fails, at min/max passes, above max fails.
    /// Validates: Requirements 5.1, 9.6
    /// </summary>
    [Theory]
    [InlineData(-91, false)]
    [InlineData(-90, true)]
    [InlineData(90, true)]
    [InlineData(91, false)]
    public void Validator_Latitude_BoundaryValues(double latitude, bool shouldPass)
    {
        var settings = ValidSettings();
        settings.Latitude = latitude;

        var result = Validate(settings);

        Assert.Equal(shouldPass, result.Succeeded);
        if (!shouldPass)
            Assert.Contains("Latitude", result.FailureMessage);
    }

    #endregion

    #region Validator – Longitude Boundaries

    /// <summary>
    /// Validates Longitude boundary values: below min fails, at min/max passes, above max fails.
    /// Validates: Requirements 5.1, 9.6
    /// </summary>
    [Theory]
    [InlineData(-181, false)]
    [InlineData(-180, true)]
    [InlineData(180, true)]
    [InlineData(181, false)]
    public void Validator_Longitude_BoundaryValues(double longitude, bool shouldPass)
    {
        var settings = ValidSettings();
        settings.Longitude = longitude;

        var result = Validate(settings);

        Assert.Equal(shouldPass, result.Succeeded);
        if (!shouldPass)
            Assert.Contains("Longitude", result.FailureMessage);
    }

    #endregion

    #region Validator – MorningOffsetMinutes Boundaries

    /// <summary>
    /// Validates MorningOffsetMinutes boundary values: below -60 fails, -60 to 240 passes, above 240 fails.
    /// Validates: Requirements 3.4, 9.1
    /// </summary>
    [Theory]
    [InlineData(-61, false)]
    [InlineData(-60, true)]
    [InlineData(240, true)]
    [InlineData(241, false)]
    public void Validator_MorningOffsetMinutes_BoundaryValues(int value, bool shouldPass)
    {
        var settings = ValidSettings();
        settings.MorningOffsetMinutes = value;

        var result = Validate(settings);

        Assert.Equal(shouldPass, result.Succeeded);
        if (!shouldPass)
            Assert.Contains("MorningOffsetMinutes", result.FailureMessage);
    }

    #endregion

    #region Validator – MorningDurationMinutes Boundaries

    /// <summary>
    /// Validates MorningDurationMinutes boundary values: 0 fails, 1 to 720 passes, 721 fails.
    /// Validates: Requirements 3.3, 9.1
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(720, true)]
    [InlineData(721, false)]
    public void Validator_MorningDurationMinutes_BoundaryValues(int value, bool shouldPass)
    {
        var settings = ValidSettings();
        settings.MorningDurationMinutes = value;

        var result = Validate(settings);

        Assert.Equal(shouldPass, result.Succeeded);
        if (!shouldPass)
            Assert.Contains("MorningDurationMinutes", result.FailureMessage);
    }

    #endregion

    #region Validator – EveningPreSunsetMinutes Boundaries

    /// <summary>
    /// Validates EveningPreSunsetMinutes boundary values: 0 fails, 1 to 480 passes, 481 fails.
    /// Validates: Requirements 4.3, 9.2
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(480, true)]
    [InlineData(481, false)]
    public void Validator_EveningPreSunsetMinutes_BoundaryValues(int value, bool shouldPass)
    {
        var settings = ValidSettings();
        settings.EveningPreSunsetMinutes = value;

        var result = Validate(settings);

        Assert.Equal(shouldPass, result.Succeeded);
        if (!shouldPass)
            Assert.Contains("EveningPreSunsetMinutes", result.FailureMessage);
    }

    #endregion

    #region Validator – WeatherPollIntervalMinutes Boundaries

    /// <summary>
    /// Validates WeatherPollIntervalMinutes boundary values: 0 fails, 1 to 60 passes, 61 fails.
    /// Validates: Requirements 6.2, 9.3
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void Validator_WeatherPollIntervalMinutes_BoundaryValues(int value, bool shouldPass)
    {
        var settings = ValidSettings();
        settings.WeatherPollIntervalMinutes = value;

        var result = Validate(settings);

        Assert.Equal(shouldPass, result.Succeeded);
        if (!shouldPass)
            Assert.Contains("WeatherPollIntervalMinutes", result.FailureMessage);
    }

    #endregion

    #region Validator – WindSpeedThresholdKph Boundaries

    /// <summary>
    /// Validates WindSpeedThresholdKph boundary values: 0 fails, 1 to 120 passes, 121 fails.
    /// Validates: Requirements 6.5, 9.4
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public void Validator_WindSpeedThresholdKph_BoundaryValues(int value, bool shouldPass)
    {
        var settings = ValidSettings();
        settings.WindSpeedThresholdKph = value;

        var result = Validate(settings);

        Assert.Equal(shouldPass, result.Succeeded);
        if (!shouldPass)
            Assert.Contains("WindSpeedThresholdKph", result.FailureMessage);
    }

    #endregion

    #region Validator – AudioFilePath Boundaries

    /// <summary>
    /// Validates AudioFilePath: empty/whitespace fails, non-empty passes.
    /// Validates: Requirements 2.1, 9.5
    /// </summary>
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("audio/swift-call.mp3", true)]
    public void Validator_AudioFilePath_BoundaryValues(string value, bool shouldPass)
    {
        var settings = ValidSettings();
        settings.AudioFilePath = value;

        var result = Validate(settings);

        Assert.Equal(shouldPass, result.Succeeded);
        if (!shouldPass)
            Assert.Contains("AudioFilePath", result.FailureMessage);
    }

    /// <summary>
    /// Validates AudioFilePath null fails.
    /// Validates: Requirement 9.5
    /// </summary>
    [Fact]
    public void Validator_AudioFilePath_Null_Fails()
    {
        var settings = ValidSettings();
        settings.AudioFilePath = null!;

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains("AudioFilePath", result.FailureMessage);
    }

    #endregion

    #region Helpers

    private static ValidateOptionsResult Validate(AudioSettings settings)
    {
        var validator = new AudioSettingsValidator();
        return validator.Validate(null, settings);
    }

    private static AudioSettings ValidSettings() => new()
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

    #endregion
}
