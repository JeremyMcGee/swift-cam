using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace SwiftCam.Tests.Integration;

/// <summary>
/// Integration tests for the GET /api/audio-status endpoint.
/// Validates Requirements 7.1 and 7.6.
/// </summary>
public class StatusEndpointIntegrationTests : IClassFixture<StatusEndpointIntegrationTests.AudioStatusWebApplicationFactory>
{
    private readonly HttpClient _client;

    public StatusEndpointIntegrationTests(AudioStatusWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAudioStatus_Returns200Ok()
    {
        // Act
        var response = await _client.GetAsync("/api/audio-status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAudioStatus_ReturnsApplicationJsonContentType()
    {
        // Act
        var response = await _client.GetAsync("/api/audio-status");

        // Assert
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetAudioStatus_ReturnsValidResponseStructure()
    {
        // Act
        var response = await _client.GetAsync("/api/audio-status");
        var body = await response.Content.ReadFromJsonAsync<AudioStatusResponse>();

        // Assert
        Assert.NotNull(body);

        // State must be one of the valid AudioState enum names
        var validStates = new[] { "Idle", "Playing", "Suppressed", "Stopped", "Error" };
        Assert.Contains(body.State, validStates);

        // Reason must be non-null and at most 200 characters
        Assert.NotNull(body.Reason);
        Assert.True(body.Reason.Length <= 200, $"Reason length {body.Reason.Length} exceeds 200 characters");
    }

    /// <summary>
    /// Custom WebApplicationFactory that removes all hosted services but registers
    /// AudioService as a singleton with stub dependencies so the endpoint can resolve it.
    /// </summary>
    public class AudioStatusWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove all hosted services (CameraService, WeatherService, AudioService, MotionDetector)
                services.RemoveAll<IHostedService>();

                // Remove existing audio-related registrations to avoid conflicts
                services.RemoveAll<ICameraService>();
                services.RemoveAll<ISolarCalculator>();
                services.RemoveAll<IWeatherService>();
                services.RemoveAll<IAudioProcessManager>();
                services.RemoveAll<AudioService>();

                // Register stubs for AudioService dependencies
                services.AddSingleton<ICameraService, StubCameraService>();
                services.AddSingleton<ISolarCalculator, StubSolarCalculator>();
                services.AddSingleton<IWeatherService, StubWeatherService>();
                services.AddSingleton<IAudioProcessManager, StubAudioProcessManager>();

                // Register AudioService as a singleton (not as a hosted service)
                // so the endpoint can resolve it without running the background loop
                services.AddSingleton<AudioService>();
            });
        }

        private sealed class StubCameraService : ICameraService
        {
            public bool IsRunning => true;
            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public void Dispose() { }
        }

        private sealed class StubSolarCalculator : ISolarCalculator
        {
            public SolarTimes Calculate(double latitude, double longitude, DateTime date) =>
                new(new TimeOnly(5, 30), new TimeOnly(6, 0), new TimeOnly(20, 0));
        }

        private sealed class StubWeatherService : IWeatherService
        {
            public WeatherState CurrentWeather => new(0, 0, DateTime.UtcNow);
        }

        private sealed class StubAudioProcessManager : IAudioProcessManager
        {
            public bool IsPlaying => false;
            public void Start(string audioFilePath) { }
            public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
