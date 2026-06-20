using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace SwiftCam.Tests.Integration;

/// <summary>
/// Integration tests for DI wiring of audio services.
/// Validates Requirement 9.7: application prevents Audio_Service from starting
/// on validation failure, and all services resolve correctly from the container.
/// </summary>
public class AudioServiceIntegrationTests : IClassFixture<AudioServiceIntegrationTests.AudioDiTestFactory>
{
    private readonly AudioDiTestFactory _factory;

    public AudioServiceIntegrationTests(AudioDiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void AudioSettings_BindFromConfig_MatchesAppsettingsDefaults()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AudioSettings>>();

        // Act
        var settings = options.Value;

        // Assert
        Assert.Equal("audio/3mingentleduets-1-1-1-1.mp3", settings.AudioFilePath);
        Assert.Equal(51.9, settings.Latitude);
        Assert.Equal(-2.07, settings.Longitude);
        Assert.Equal(0, settings.MorningOffsetMinutes);
        Assert.Equal(210, settings.MorningDurationMinutes);
        Assert.Equal(150, settings.EveningPreSunsetMinutes);
        Assert.Equal(15, settings.WeatherPollIntervalMinutes);
        Assert.Equal(40, settings.WindSpeedThresholdKph);
    }

    [Fact]
    public void AllAudioServices_ResolveCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        // Assert - all audio-related services resolve without throwing
        Assert.NotNull(provider.GetRequiredService<IOptions<AudioSettings>>());
        Assert.NotNull(provider.GetRequiredService<ISolarCalculator>());
        Assert.NotNull(provider.GetRequiredService<IAudioProcessManager>());
        Assert.NotNull(provider.GetRequiredService<IWeatherService>());
        Assert.NotNull(provider.GetRequiredService<AudioService>());
    }

    [Fact]
    public void ValidationRejectsInvalidConfig_ThrowsOptionsValidationException()
    {
        // Arrange - build a host with invalid Audio config (Latitude = 999)
        // ValidateOnStart causes the exception during host startup (accessing factory.Services)
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Audio:Latitude"] = "999",
                    });
                });
            });

        // Act & Assert - the host should fail to start due to validation
        var ex = Assert.Throws<OptionsValidationException>(() =>
        {
            _ = factory.Services;
        });

        Assert.Contains("Latitude", ex.Message);
    }

    /// <summary>
    /// Custom WebApplicationFactory that removes all hosted services to avoid
    /// spawning real camera, audio, and motion processes during tests.
    /// </summary>
    public class AudioDiTestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove all hosted services (CameraService, WeatherService, AudioService, MotionDetector)
                services.RemoveAll<IHostedService>();

                // Re-register AudioService as a singleton (not hosted) so DI resolution tests work
                services.RemoveAll<AudioService>();
                services.AddSingleton<AudioService>();
            });
        }
    }
}
