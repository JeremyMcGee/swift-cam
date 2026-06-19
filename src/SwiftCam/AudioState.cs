namespace SwiftCam;

/// <summary>
/// Represents the current state of the audio attraction system.
/// </summary>
public enum AudioState
{
    Idle,       // Outside any playback window
    Playing,    // mplayer is actively playing
    Suppressed, // Inside window but weather prevents playback
    Stopped,    // Inside window, fair weather, but stopped (e.g., between restart attempts)
    Error       // Unrecoverable error (file not found, mplayer not installed, max retries)
}
