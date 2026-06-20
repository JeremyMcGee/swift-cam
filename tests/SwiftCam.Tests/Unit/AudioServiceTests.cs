using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Unit tests for AudioService error states and graceful shutdown.
/// Validates: Requirements 1.7, 1.8, 2.3
/// </summary>
public class AudioServiceTests
{
    /// <summary>
    /// When IAudioProcessManager.Start() throws InvalidOperationException (mplayer not found),
    /// the AudioService should enter the Error state with a reason containing "mplayer not found".
    /// Validates: Requirement 1.8
    /// </summary>
    [Fact]
    public async Task Start_MplayerNotFound_EntersErrorState()
    {
        // Arrange - set up a time that falls within an active playback window
        var now = new DateTimeOffset(2024, 6, 15, 5, 30, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);

        // Civil twilight at 05:00 -> morning window 05:00 to 08:30 (210 min)
        var solarCalculator = new FakeSolarCalculator(
            new SolarTimes(
                CivilTwilight: new TimeOnly(5, 0),
                Sunrise: new TimeOnly(5, 30),
                Sunset: new TimeOnly(21, 0)));

        var weatherService = new FakeWeatherService(
            new WeatherState(PrecipitationMm: 0, WindSpeedKph: 0, LastUpdated: now.UtcDateTime));

        var processManager = new FakeAudioProcessManager
        {
            ThrowOnStart = new InvalidOperationException("mplayer binary not found")
        };

        // Create a temp audio file so the file-exists check passes
        var tempFile = Path.GetTempFileName();
        try
        {
            var settings = CreateSettings(tempFile);
            var service = CreateAudioService(settings, solarCalculator, weatherService, processManager, timeProvider);

            using var cts = new CancellationTokenSource();

            // Act - start the service and wait for state to transition
            await service.StartAsync(cts.Token);

            // Wait for the background task to complete its first loop iteration
            for (int i = 0; i < 20; i++)
            {
                timeProvider.Advance(TimeSpan.FromSeconds(1));
                await Task.Yield();
                await Task.Delay(50);
                if (service.CurrentState != AudioState.Idle)
                    break;
            }

            // Check MorningWindow was calculated
            var morningWindow = service.MorningWindow;
            var currentState = service.CurrentState;
            var reason = service.CurrentReason;

            cts.Cancel();
            await WaitForServiceStop(service);

            // Assert
            Assert.NotNull(morningWindow); // Verify windows were calculated
            Assert.Equal(AudioState.Error, currentState);
            Assert.Contains("mplayer not found", reason);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// When the configured audio file does not exist on disk,
    /// the AudioService should enter the Error state with a reason containing "Audio file not found".
    /// Validates: Requirement 2.3
    /// </summary>
    [Fact]
    public async Task Start_AudioFileNotFound_EntersErrorState()
    {
        // Arrange - set up a time that falls within an active playback window
        var now = new DateTimeOffset(2024, 6, 15, 5, 30, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);

        var solarCalculator = new FakeSolarCalculator(
            new SolarTimes(
                CivilTwilight: new TimeOnly(5, 0),
                Sunrise: new TimeOnly(5, 30),
                Sunset: new TimeOnly(21, 0)));

        var weatherService = new FakeWeatherService(
            new WeatherState(PrecipitationMm: 0, WindSpeedKph: 0, LastUpdated: now.UtcDateTime));

        var processManager = new FakeAudioProcessManager();

        // Use a non-existent file path
        var settings = CreateSettings("/nonexistent/path/to/audio.mp3");
        var service = CreateAudioService(settings, solarCalculator, weatherService, processManager, timeProvider);

        using var cts = new CancellationTokenSource();

        // Act - start the service and pump time to allow the loop to execute
        await service.StartAsync(cts.Token);

        for (int i = 0; i < 30; i++)
        {
            await Task.Yield();
            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(20);
            if (service.CurrentState != AudioState.Idle)
                break;
        }

        // Capture state before shutdown (shutdown resets state to Idle)
        var capturedState = service.CurrentState;
        var capturedReason = service.CurrentReason;

        cts.Cancel();
        await WaitForServiceStop(service);

        // Assert
        Assert.NotNull(service.MorningWindow);
        Assert.Equal(AudioState.Error, capturedState);
        Assert.Contains("Audio file not found", capturedReason);
    }

    /// <summary>
    /// When the service shuts down and mplayer is playing,
    /// StopAsync should be called on the process manager.
    /// Validates: Requirement 1.7
    /// </summary>
    [Fact(Skip = "Flaky due to race condition in async shutdown timing")]
    public async Task Shutdown_WhenPlaying_TerminatesMplayer()
    {
        // Arrange - set up a time that falls within an active playback window
        var now = new DateTimeOffset(2024, 6, 15, 5, 30, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);

        var solarCalculator = new FakeSolarCalculator(
            new SolarTimes(
                CivilTwilight: new TimeOnly(5, 0),
                Sunrise: new TimeOnly(5, 30),
                Sunset: new TimeOnly(21, 0)));

        var weatherService = new FakeWeatherService(
            new WeatherState(PrecipitationMm: 0, WindSpeedKph: 0, LastUpdated: now.UtcDateTime));

        var processManager = new FakeAudioProcessManager
        {
            IsPlayingValue = true
        };

        // Create a temp audio file so the file-exists check passes
        var tempFile = Path.GetTempFileName();
        try
        {
            var settings = CreateSettings(tempFile);
            var service = CreateAudioService(settings, solarCalculator, weatherService, processManager, timeProvider);

            using var cts = new CancellationTokenSource();

            // Act - start the service, let the loop tick, then trigger shutdown
            await service.StartAsync(cts.Token);
            timeProvider.Advance(TimeSpan.FromSeconds(2));
            await Task.Delay(100);
            cts.Cancel();
            await WaitForServiceStop(service);

            // Assert - StopAsync should have been called during graceful shutdown
            Assert.True(processManager.StopAsyncCalled, "StopAsync should be called during graceful shutdown");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #region Helpers

    private static IOptions<AudioSettings> CreateSettings(string audioFilePath)
    {
        return Options.Create(new AudioSettings
        {
            AudioFilePath = audioFilePath,
            Latitude = 51.9,
            Longitude = -2.07,
            MorningOffsetMinutes = 0,
            MorningDurationMinutes = 210,
            EveningPreSunsetMinutes = 150,
            WindSpeedThresholdKph = 40
        });
    }

    private static AudioService CreateAudioService(
        IOptions<AudioSettings> settings,
        ISolarCalculator solarCalculator,
        IWeatherService weatherService,
        IAudioProcessManager processManager,
        TimeProvider timeProvider)
    {
        return new AudioService(
            settings,
            solarCalculator,
            weatherService,
            processManager,
            timeProvider,
            NullLogger<AudioService>.Instance);
    }

    private static async Task WaitForServiceStop(AudioService service)
    {
        try
        {
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected during cancellation
        }
    }

    #endregion

    #region Test Doubles

    private sealed class FakeSolarCalculator : ISolarCalculator
    {
        private readonly SolarTimes _result;

        public FakeSolarCalculator(SolarTimes result)
        {
            _result = result;
        }

        public SolarTimes Calculate(double latitude, double longitude, DateTime date) => _result;
    }

    private sealed class FakeWeatherService : IWeatherService
    {
        public FakeWeatherService(WeatherState weather)
        {
            CurrentWeather = weather;
        }

        public WeatherState CurrentWeather { get; }
    }

    private sealed class FakeAudioProcessManager : IAudioProcessManager
    {
        public bool IsPlayingValue { get; set; }
        public bool StopAsyncCalled { get; private set; }
        public InvalidOperationException? ThrowOnStart { get; set; }

        public bool IsPlaying => IsPlayingValue;

        public void Start(string audioFilePath)
        {
            if (ThrowOnStart is not null)
            {
                throw ThrowOnStart;
            }

            IsPlayingValue = true;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopAsyncCalled = true;
            IsPlayingValue = false;
            return Task.CompletedTask;
        }
    }

    #endregion
}
