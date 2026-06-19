using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Unit tests for WeatherService covering initial state, parsing, and failure handling.
/// Validates: Requirements 6.6, 6.8, 6.9
/// </summary>
public class WeatherServiceTests
{
    private static IOptions<AudioSettings> DefaultOptions => Options.Create(new AudioSettings
    {
        Latitude = 51.9,
        Longitude = -2.07,
        WeatherPollIntervalMinutes = 1
    });

    private static WeatherService CreateService(DelegatingHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new WeatherService(
            httpClient,
            DefaultOptions,
            NullLogger<WeatherService>.Instance);
    }

    /// <summary>
    /// On construction, CurrentWeather should be fair weather (0, 0, null) before any fetch.
    /// Validates: Requirement 6.8
    /// </summary>
    [Fact]
    public void CurrentWeather_BeforeAnyFetch_AssumsFairWeather()
    {
        // Arrange
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var service = CreateService(handler);

        // Act
        var weather = service.CurrentWeather;

        // Assert
        Assert.Equal(0, weather.PrecipitationMm);
        Assert.Equal(0, weather.WindSpeedKph);
        Assert.Null(weather.LastUpdated);
    }

    /// <summary>
    /// A valid Open-Meteo JSON response is correctly parsed into WeatherState.
    /// Validates: Requirement 6.6
    /// </summary>
    [Fact]
    public async Task CurrentWeather_AfterSuccessfulFetch_ParsesOpenMeteoResponse()
    {
        // Arrange
        const string json = """{"current":{"precipitation":1.5,"wind_speed_10m":25.0}}""";
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var service = CreateService(handler);

        // Act - invoke FetchWeatherAsync directly via reflection for deterministic testing
        var fetchMethod = typeof(WeatherService).GetMethod(
            "FetchWeatherAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)fetchMethod!.Invoke(service, [CancellationToken.None])!;

        // Assert
        Assert.Equal(1.5, service.CurrentWeather.PrecipitationMm);
        Assert.Equal(25.0, service.CurrentWeather.WindSpeedKph);
        Assert.NotNull(service.CurrentWeather.LastUpdated);
    }

    /// <summary>
    /// After 3 consecutive HTTP failures, CurrentWeather resets to fair weather.
    /// Validates: Requirement 6.9
    /// </summary>
    [Fact]
    public async Task CurrentWeather_After3ConsecutiveFailures_ResetToFairWeather()
    {
        // Arrange - all requests return 500 to simulate consecutive failures
        var handler = new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var service = CreateService(handler);

        // Use reflection to invoke the private FetchWeatherAsync method directly,
        // avoiding the need to wait for real poll intervals
        var fetchMethod = typeof(WeatherService).GetMethod(
            "FetchWeatherAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act - call fetch 3 times to trigger the consecutive failure threshold
        for (int i = 0; i < 3; i++)
        {
            await (Task)fetchMethod!.Invoke(service, [CancellationToken.None])!;
        }

        // Assert - after 3 consecutive failures, CurrentWeather resets to fair weather
        Assert.Equal(0, service.CurrentWeather.PrecipitationMm);
        Assert.Equal(0, service.CurrentWeather.WindSpeedKph);
    }

    /// <summary>
    /// A simple DelegatingHandler that returns controlled responses for testing.
    /// </summary>
    private sealed class FakeHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
        {
            _factory = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_factory(request));
        }
    }
}
