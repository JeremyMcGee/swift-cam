namespace SwiftCam;

/// <summary>
/// Manages the mplayer child process lifecycle for audio playback.
/// </summary>
public interface IAudioProcessManager
{
    /// <summary>
    /// Gets a value indicating whether the audio process is currently running.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Starts the audio playback process with the specified audio file.
    /// </summary>
    /// <param name="audioFilePath">The path to the audio file to play.</param>
    void Start(string audioFilePath);

    /// <summary>
    /// Stops the audio playback process gracefully, force-killing if it does not terminate within the timeout.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the process to stop.</param>
    /// <returns>A task that completes when the process has been stopped.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}
