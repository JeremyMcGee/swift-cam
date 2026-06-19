namespace SwiftCam;

/// <summary>
/// Strongly-typed configuration for motion detection parameters.
/// </summary>
public class MotionSettings
{
    public double Threshold { get; set; } = 5.0;
    public int CooldownSeconds { get; set; } = 300;
    public string CaptureDirectory { get; set; } = "captures";
    public int PixelTolerance { get; set; } = 30;
}
