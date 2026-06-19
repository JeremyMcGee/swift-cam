namespace SwiftCam;

/// <summary>
/// Manages the camera capture process and reports its running state.
/// </summary>
public interface ICameraService : IHostedService, IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the camera capture process is currently running.
    /// </summary>
    bool IsRunning { get; }
}
