namespace SwiftCam;

/// <summary>
/// Strongly-typed configuration for audio attraction parameters.
/// Bound from the "Audio" section of appsettings.json.
/// </summary>
public class AudioSettings
{
    public string AudioFilePath { get; set; } = "audio/swift-call.mp3";
    public double Latitude { get; set; } = 51.9;
    public double Longitude { get; set; } = -2.07;
    public int MorningOffsetMinutes { get; set; } = 0;
    public int MorningDurationMinutes { get; set; } = 210;
    public int EveningPreSunsetMinutes { get; set; } = 150;
    public int WeatherPollIntervalMinutes { get; set; } = 15;
    public int WindSpeedThresholdKph { get; set; } = 40;
}
