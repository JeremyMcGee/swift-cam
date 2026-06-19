using System.Diagnostics;

namespace SwiftCam;

/// <summary>
/// Background service that manages the libcamera-vid child process,
/// reads JPEG frames from stdout, and publishes them to the broadcaster.
/// </summary>
public class CameraService : BackgroundService, ICameraService
{
    private static readonly string[] ProcessNames = ["rpicam-vid", "libcamera-vid"];
    private const string ProcessArguments = "-t 0 --codec mjpeg --width 640 --height 480 --framerate 15 -q 80 -n -o -";

    /// <summary>
    /// JPEG Start Of Image marker.
    /// </summary>
    private const byte MarkerPrefix = 0xFF;
    private const byte SoiMarker = 0xD8;
    private const byte EoiMarker = 0xD9;

    /// <summary>
    /// If the process exits within this time after starting, it is treated as
    /// "camera not detected" rather than a mid-stream crash.
    /// </summary>
    private static readonly TimeSpan CameraDetectionTimeout = TimeSpan.FromSeconds(5);

    private readonly IFrameBroadcaster _broadcaster;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<CameraService> _logger;

    private Process? _process;
    private bool _isRunning;

    public CameraService(
        IFrameBroadcaster broadcaster,
        IHostApplicationLifetime appLifetime,
        ILogger<CameraService> logger)
    {
        _broadcaster = broadcaster;
        _appLifetime = appLifetime;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var restartAttempted = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                _process = StartCameraProcess();
                _isRunning = true;
                _logger.LogInformation("Camera process started (PID {Pid})", _process.Id);

                await ReadFramesAsync(_process, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown requested
                _logger.LogInformation("Camera service shutting down");
                break;
            }
            catch (Exception ex)
            {
                _isRunning = false;
                var elapsed = DateTime.UtcNow - startTime;
                var stderr = await ReadStderrAsync(_process);

                // If the process exited very quickly, treat as camera not detected
                if (elapsed < CameraDetectionTimeout)
                {
                    _logger.LogError(
                        ex,
                        "Camera not detected. Process exited within {Elapsed:F1}s. Stderr: {Stderr}",
                        elapsed.TotalSeconds,
                        stderr);
                    // Terminate application with non-zero exit code
                    Environment.ExitCode = 1;
                    _appLifetime.StopApplication();
                    return;
                }

                // Mid-stream crash
                _logger.LogError(
                    ex,
                    "Camera process crashed. Stderr: {Stderr}",
                    stderr);

                if (!restartAttempted)
                {
                    restartAttempted = true;
                    _logger.LogWarning("Attempting to restart camera process...");
                    // Small delay before restart
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                // Restart already attempted once, terminate app
                _logger.LogCritical("Camera process restart failed. Terminating application.");
                Environment.ExitCode = 1;
                _appLifetime.StopApplication();
                return;
            }
            finally
            {
                CleanupProcess();
            }
        }

        _isRunning = false;
    }

    public override void Dispose()
    {
        CleanupProcess();
        base.Dispose();
    }

    private Process StartCameraProcess()
    {
        foreach (var processName in ProcessNames)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = processName,
                Arguments = ProcessArguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };

            try
            {
                if (process.Start())
                {
                    _logger.LogInformation("Using camera tool: {ProcessName}", processName);
                    return process;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Binary not found, try next
                process.Dispose();
                _logger.LogDebug("{ProcessName} not found, trying next option", processName);
            }
        }

        throw new InvalidOperationException(
            $"No camera tool found. Tried: {string.Join(", ", ProcessNames)}");
    }

    private async Task ReadFramesAsync(Process process, CancellationToken ct)
    {
        var stdout = process.StandardOutput.BaseStream;
        var buffer = new byte[65536]; // 64KB read buffer
        using var frameBuffer = new MemoryStream();
        var insideFrame = false;
        var hasPendingMarkerPrefix = false;

        while (!ct.IsCancellationRequested)
        {
            var bytesRead = await stdout.ReadAsync(buffer, 0, buffer.Length, ct);

            if (bytesRead == 0)
            {
                // Stream closed — process likely exited
                if (!process.HasExited)
                {
                    process.Kill();
                }

                process.WaitForExit();
                var exitCode = process.ExitCode;
                throw new InvalidOperationException(
                    $"Camera process stdout closed unexpectedly (exit code: {exitCode})");
            }

            for (var i = 0; i < bytesRead; i++)
            {
                var currentByte = buffer[i];

                if (hasPendingMarkerPrefix)
                {
                    hasPendingMarkerPrefix = false;

                    if (currentByte == SoiMarker)
                    {
                        // Start of a new JPEG frame
                        frameBuffer.SetLength(0);
                        frameBuffer.WriteByte(MarkerPrefix);
                        frameBuffer.WriteByte(SoiMarker);
                        insideFrame = true;
                        continue;
                    }

                    if (currentByte == EoiMarker && insideFrame)
                    {
                        // End of JPEG frame
                        frameBuffer.WriteByte(MarkerPrefix);
                        frameBuffer.WriteByte(EoiMarker);
                        insideFrame = false;

                        var frameData = frameBuffer.ToArray();
                        _broadcaster.PublishFrame(frameData);

                        frameBuffer.SetLength(0);
                        continue;
                    }

                    // Not a recognized marker — write the deferred 0xFF and current byte
                    if (insideFrame)
                    {
                        frameBuffer.WriteByte(MarkerPrefix);
                        frameBuffer.WriteByte(currentByte);
                    }

                    continue;
                }

                if (currentByte == MarkerPrefix)
                {
                    // Defer writing 0xFF until we see the next byte
                    hasPendingMarkerPrefix = true;
                    continue;
                }

                if (insideFrame)
                {
                    frameBuffer.WriteByte(currentByte);
                }
            }
        }
    }

    private static async Task<string> ReadStderrAsync(Process? process)
    {
        if (process == null)
            return string.Empty;

        try
        {
            return await process.StandardError.ReadToEndAsync();
        }
        catch
        {
            return "(unable to read stderr)";
        }
    }

    private void CleanupProcess()
    {
        if (_process == null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while cleaning up camera process");
        }

        _process.Dispose();
        _process = null;
        _isRunning = false;
    }
}
