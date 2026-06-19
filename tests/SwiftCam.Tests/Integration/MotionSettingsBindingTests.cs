using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace SwiftCam.Tests.Integration;

public class MotionSettingsBindingTests
{
    [Fact]
    public void MotionSettings_BindsCustomValuesFromConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Motion:Threshold"] = "10.5",
                ["Motion:CooldownSeconds"] = "600",
                ["Motion:CaptureDirectory"] = "/tmp/caps",
                ["Motion:PixelTolerance"] = "50"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<MotionSettings>(configuration.GetSection("Motion"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<MotionSettings>>();
        var settings = options.Value;

        // Assert
        Assert.Equal(10.5, settings.Threshold);
        Assert.Equal(600, settings.CooldownSeconds);
        Assert.Equal("/tmp/caps", settings.CaptureDirectory);
        Assert.Equal(50, settings.PixelTolerance);
    }

    [Fact]
    public void MotionSettings_UsesDefaultsWhenNoMotionSectionExists()
    {
        // Arrange - configuration with no "Motion" section
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.Configure<MotionSettings>(configuration.GetSection("Motion"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<MotionSettings>>();
        var settings = options.Value;

        // Assert - all defaults from MotionSettings POCO
        Assert.Equal(5.0, settings.Threshold);
        Assert.Equal(300, settings.CooldownSeconds);
        Assert.Equal("captures", settings.CaptureDirectory);
        Assert.Equal(30, settings.PixelTolerance);
    }
}
