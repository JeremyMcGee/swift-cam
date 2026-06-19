using Microsoft.Extensions.Options;
using Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Unit tests for MotionSettings default values and MotionSettingsValidator boundary checks.
/// Validates: Requirements 2.1, 2.2, 2.3
/// </summary>
public class MotionSettingsTests
{
    #region Default Values

    /// <summary>
    /// Verifies Threshold defaults to 5.0.
    /// Validates: Requirement 2.1
    /// </summary>
    [Fact]
    public void Defaults_Threshold_Is5()
    {
        var settings = new MotionSettings();

        Assert.Equal(5.0, settings.Threshold);
    }

    /// <summary>
    /// Verifies CooldownSeconds defaults to 300.
    /// Validates: Requirement 2.2
    /// </summary>
    [Fact]
    public void Defaults_CooldownSeconds_Is300()
    {
        var settings = new MotionSettings();

        Assert.Equal(300, settings.CooldownSeconds);
    }

    /// <summary>
    /// Verifies CaptureDirectory defaults to "captures".
    /// Validates: Requirement 2.3
    /// </summary>
    [Fact]
    public void Defaults_CaptureDirectory_IsCaptures()
    {
        var settings = new MotionSettings();

        Assert.Equal("captures", settings.CaptureDirectory);
    }

    /// <summary>
    /// Verifies PixelTolerance defaults to 30.
    /// </summary>
    [Fact]
    public void Defaults_PixelTolerance_Is30()
    {
        var settings = new MotionSettings();

        Assert.Equal(30, settings.PixelTolerance);
    }

    #endregion

    #region Validator – Valid Boundary Values

    /// <summary>
    /// Verifies validator accepts Threshold at lower boundary (0.1).
    /// </summary>
    [Fact]
    public void Validator_Threshold_AtLowerBoundary_Succeeds()
    {
        var settings = ValidSettings();
        settings.Threshold = 0.1;

        var result = Validate(settings);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Verifies validator accepts Threshold at upper boundary (100.0).
    /// </summary>
    [Fact]
    public void Validator_Threshold_AtUpperBoundary_Succeeds()
    {
        var settings = ValidSettings();
        settings.Threshold = 100.0;

        var result = Validate(settings);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Verifies validator accepts CooldownSeconds at lower boundary (0).
    /// </summary>
    [Fact]
    public void Validator_CooldownSeconds_AtLowerBoundary_Succeeds()
    {
        var settings = ValidSettings();
        settings.CooldownSeconds = 0;

        var result = Validate(settings);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Verifies validator accepts CooldownSeconds at upper boundary (86400).
    /// </summary>
    [Fact]
    public void Validator_CooldownSeconds_AtUpperBoundary_Succeeds()
    {
        var settings = ValidSettings();
        settings.CooldownSeconds = 86400;

        var result = Validate(settings);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Verifies validator accepts a non-empty CaptureDirectory.
    /// </summary>
    [Fact]
    public void Validator_CaptureDirectory_NonEmpty_Succeeds()
    {
        var settings = ValidSettings();
        settings.CaptureDirectory = "my-captures";

        var result = Validate(settings);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Verifies validator accepts PixelTolerance at lower boundary (1).
    /// </summary>
    [Fact]
    public void Validator_PixelTolerance_AtLowerBoundary_Succeeds()
    {
        var settings = ValidSettings();
        settings.PixelTolerance = 1;

        var result = Validate(settings);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Verifies validator accepts PixelTolerance at upper boundary (255).
    /// </summary>
    [Fact]
    public void Validator_PixelTolerance_AtUpperBoundary_Succeeds()
    {
        var settings = ValidSettings();
        settings.PixelTolerance = 255;

        var result = Validate(settings);

        Assert.True(result.Succeeded);
    }

    #endregion

    #region Validator – Invalid Boundary Values

    /// <summary>
    /// Verifies validator rejects Threshold below 0.1.
    /// </summary>
    [Fact]
    public void Validator_Threshold_BelowLowerBoundary_Fails()
    {
        var settings = ValidSettings();
        settings.Threshold = 0.09;

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains("Threshold", result.FailureMessage);
    }

    /// <summary>
    /// Verifies validator rejects Threshold above 100.0.
    /// </summary>
    [Fact]
    public void Validator_Threshold_AboveUpperBoundary_Fails()
    {
        var settings = ValidSettings();
        settings.Threshold = 100.1;

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains("Threshold", result.FailureMessage);
    }

    /// <summary>
    /// Verifies validator rejects CooldownSeconds below 0 (-1).
    /// </summary>
    [Fact]
    public void Validator_CooldownSeconds_BelowLowerBoundary_Fails()
    {
        var settings = ValidSettings();
        settings.CooldownSeconds = -1;

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains("CooldownSeconds", result.FailureMessage);
    }

    /// <summary>
    /// Verifies validator rejects CooldownSeconds above 86400.
    /// </summary>
    [Fact]
    public void Validator_CooldownSeconds_AboveUpperBoundary_Fails()
    {
        var settings = ValidSettings();
        settings.CooldownSeconds = 86401;

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains("CooldownSeconds", result.FailureMessage);
    }

    /// <summary>
    /// Verifies validator rejects null CaptureDirectory.
    /// </summary>
    [Fact]
    public void Validator_CaptureDirectory_Null_Fails()
    {
        var settings = ValidSettings();
        settings.CaptureDirectory = null!;

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains("CaptureDirectory", result.FailureMessage);
    }

    /// <summary>
    /// Verifies validator rejects empty CaptureDirectory.
    /// </summary>
    [Fact]
    public void Validator_CaptureDirectory_Empty_Fails()
    {
        var settings = ValidSettings();
        settings.CaptureDirectory = "";

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains("CaptureDirectory", result.FailureMessage);
    }

    /// <summary>
    /// Verifies validator rejects whitespace-only CaptureDirectory.
    /// </summary>
    [Fact]
    public void Validator_CaptureDirectory_Whitespace_Fails()
    {
        var settings = ValidSettings();
        settings.CaptureDirectory = "   ";

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains("CaptureDirectory", result.FailureMessage);
    }

    /// <summary>
    /// Verifies validator rejects PixelTolerance below 1 (0).
    /// </summary>
    [Fact]
    public void Validator_PixelTolerance_BelowLowerBoundary_Fails()
    {
        var settings = ValidSettings();
        settings.PixelTolerance = 0;

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains("PixelTolerance", result.FailureMessage);
    }

    /// <summary>
    /// Verifies validator rejects PixelTolerance above 255 (256).
    /// </summary>
    [Fact]
    public void Validator_PixelTolerance_AboveUpperBoundary_Fails()
    {
        var settings = ValidSettings();
        settings.PixelTolerance = 256;

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains("PixelTolerance", result.FailureMessage);
    }

    #endregion

    #region Helpers

    private static ValidateOptionsResult Validate(MotionSettings settings)
    {
        var validator = new MotionSettingsValidator();
        return validator.Validate(null, settings);
    }

    private static MotionSettings ValidSettings() => new()
    {
        Threshold = 5.0,
        CooldownSeconds = 300,
        CaptureDirectory = "captures",
        PixelTolerance = 30
    };

    #endregion
}
