namespace SwiftCam;

/// <summary>
/// Provides access to the current weather conditions for determining
/// whether audio playback should be suppressed.
/// </summary>
public interface IWeatherService
{
    /// <summary>
    /// Gets the most recently fetched weather state.
    /// </summary>
    WeatherState CurrentWeather { get; }
}
