using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace SwiftCam;

/// <summary>
/// Background service that polls the Open-Meteo API for current weather conditions.
/// Implements IWeatherService to expose the latest weather state to the audio service.
/// </summary>
public class WeatherService : BackgroundService, IWeatherService
{
    /// <summary>
    /// Number of consecutive failures before assuming fair weather.
    /// </summary>
    private const int MaxConsecutiveFailures = 3;

    private static readonly WeatherState FairWeather = new(PrecipitationMm: 0, WindSpeedKph: 0, LastUpdated: null);

    private readonly HttpClient _httpClient;
    private readonly AudioSettings _settings;
    private readonly ILogger<WeatherService> _logger;

    private WeatherState _currentWeather = FairWeather;
    private int _consecutiveFailures;

    public WeatherService(
        HttpClient httpClient,
        IOptions<AudioSettings> options,
        ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public WeatherState CurrentWeather => _currentWeather;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Weather service started. Polling every {Interval} minutes at ({Lat}, {Lon})",
            _settings.WeatherPollIntervalMinutes,
            _settings.Latitude,
            _settings.Longitude);

        while (!stoppingToken.IsCancellationRequested)
        {
            await FetchWeatherAsync(stoppingToken);

            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(_settings.WeatherPollIntervalMinutes),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Weather service shutting down");
    }

    private async Task FetchWeatherAsync(CancellationToken cancellationToken)
    {
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={_settings.Latitude}&longitude={_settings.Longitude}&current=precipitation,wind_speed_10m";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<OpenMeteoResponse>(url, cancellationToken);

            if (response?.Current is null)
            {
                HandleFetchFailure("Invalid response: missing 'current' data");
                return;
            }

            _consecutiveFailures = 0;
            _currentWeather = new WeatherState(
                PrecipitationMm: response.Current.Precipitation,
                WindSpeedKph: response.Current.WindSpeed10m,
                LastUpdated: DateTime.UtcNow);

            _logger.LogInformation(
                "Weather updated: precipitation={Precip}mm, wind={Wind}kph",
                _currentWeather.PrecipitationMm,
                _currentWeather.WindSpeedKph);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down, don't treat as failure
            throw;
        }
        catch (Exception ex)
        {
            HandleFetchFailure($"HTTP request failed: {ex.Message}");
        }
    }

    private void HandleFetchFailure(string reason)
    {
        _consecutiveFailures++;

        if (_consecutiveFailures >= MaxConsecutiveFailures)
        {
            _logger.LogWarning(
                "Weather data unavailable for {Count} consecutive attempts. Assuming fair weather",
                _consecutiveFailures);
            _currentWeather = new WeatherState(
                PrecipitationMm: 0,
                WindSpeedKph: 0,
                LastUpdated: _currentWeather.LastUpdated);
        }
        else
        {
            _logger.LogWarning(
                "Failed to fetch weather data ({Count}/{Max}): {Reason}",
                _consecutiveFailures,
                MaxConsecutiveFailures,
                reason);
        }
    }

    /// <summary>
    /// Open-Meteo API response model.
    /// </summary>
    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("current")]
        public CurrentData? Current { get; set; }
    }

    /// <summary>
    /// Current weather data from Open-Meteo.
    /// </summary>
    private sealed class CurrentData
    {
        [JsonPropertyName("precipitation")]
        public double Precipitation { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public double WindSpeed10m { get; set; }
    }
}
