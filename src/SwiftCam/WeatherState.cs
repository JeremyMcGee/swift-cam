namespace SwiftCam;

/// <summary>
/// Represents the current weather conditions relevant to swift flight activity.
/// </summary>
/// <param name="PrecipitationMm">Current precipitation in millimeters.</param>
/// <param name="WindSpeedKph">Current wind speed in kilometers per hour.</param>
/// <param name="LastUpdated">The time when weather data was last successfully fetched, or null if no data has been retrieved.</param>
public record WeatherState(
    double PrecipitationMm,
    double WindSpeedKph,
    DateTime? LastUpdated);
