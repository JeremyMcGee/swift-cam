namespace SwiftCam;

/// <summary>
/// Abstracts solar time computation for testability.
/// </summary>
public interface ISolarCalculator
{
    /// <summary>
    /// Calculates civil twilight, sunrise, and sunset times for a given location and date.
    /// </summary>
    /// <param name="latitude">Latitude in degrees (-90 to 90).</param>
    /// <param name="longitude">Longitude in degrees (-180 to 180).</param>
    /// <param name="date">The date to calculate solar times for.</param>
    /// <returns>A <see cref="SolarTimes"/> record with nullable times for polar edge cases.</returns>
    SolarTimes Calculate(double latitude, double longitude, DateTime date);
}
