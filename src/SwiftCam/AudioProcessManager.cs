using System.ComponentModel;
using System.Diagnostics;

namespace SwiftCam;

/// <summary>
/// Manages the mplayer child process lifecycle for audio playback.
/// Follows the same process management pattern as CameraService.
/// </summary>
public class AudioProcessManager : IAudioProcessManager
{
    private readonly ILogger<AudioProcessManager> _logger;
    private Process? _process;

    public AudioProcessManager(ILogger<AudioProcessManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsPlaying => _process is not null && !_process.HasExited;

    /// <inheritdoc />
    public void Start(string audioFilePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "mplayer",
            Arguments = $"-loop 0 {audioFilePath}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("Failed to start mplayer process.");
            }

            _process = process;
            _logger.LogInformation("mplayer started (PID {Pid}) playing {File}", process.Id, audioFilePath);
        }
        catch (Win32Exception ex)
        {
            process.Dispose();
            _logger.LogError(ex, "mplayer binary not found. Ensure mplayer is installed and available on PATH.");
            throw new InvalidOperationException(
                "mplayer binary not found. Ensure mplayer is installed and available on PATH.", ex);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                _logger.LogInformation("Sending termination signal to mplayer (PID {Pid})", _process.Id);
                _process.Kill();

                // Wait up to 5 seconds for the process to exit
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

                try
                {
                    await _process.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout elapsed, force-kill
                    _logger.LogWarning("mplayer did not exit within 5 seconds, force-killing");
                    try
                    {
                        _process.Kill(entireProcessTree: true);
                        _process.WaitForExit(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error while force-killing mplayer process");
                    }
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited between our check and kill attempt
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping mplayer process");
        }
        finally
        {
            _process.Dispose();
            _process = null;
            _logger.LogInformation("mplayer process stopped and disposed");
        }
    }
}
