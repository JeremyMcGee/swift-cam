namespace SwiftCam;

/// <summary>
/// Strongly-typed configuration for camera capture parameters.
/// </summary>
public class CameraSettings
{
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 480;
    public int Framerate { get; set; } = 15;
    public int Quality { get; set; } = 80;
}
