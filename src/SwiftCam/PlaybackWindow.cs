namespace SwiftCam;

/// <summary>
/// Represents a calculated playback time range for a specific day.
/// </summary>
public record PlaybackWindow(DateTime Start, DateTime End);
