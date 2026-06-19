using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SwiftCam;

/// <summary>
/// Background service that subscribes to the frame broadcast, compares consecutive
/// frames using pixel-level luminance differencing, and saves a JPEG capture to disk
/// when the changed-pixel percentage exceeds the configured threshold.
/// A cooldown mechanism prevents burst captures during sustained motion.
/// </summary>
public class MotionDetector : BackgroundService
{
    private readonly IFrameBroadcaster _broadcaster;
    private readonly MotionSettings _settings;
    private readonly ILogger<MotionDetector> _logger;
    private readonly TimeProvider _timeProvider;

    private DateTime _lastCaptureTime = DateTime.MinValue;

    public MotionDetector(
        IOptions<MotionSettings> options,
        IFrameBroadcaster broadcaster,
        ILogger<MotionDetector> logger,
        TimeProvider timeProvider)
    {
        _settings = options.Value;
        _broadcaster = broadcaster;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var subscription = _broadcaster.Subscribe();
        byte[]? previousFrame = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            var currentFrame = await subscription.WaitForFrameAsync(stoppingToken);

            if (previousFrame is null)
            {
                previousFrame = currentFrame;
                continue;
            }

            var now = _timeProvider.GetLocalNow().DateTime;
            var elapsed = (now - _lastCaptureTime).TotalSeconds;

            if (elapsed < _settings.CooldownSeconds)
            {
                previousFrame = currentFrame;
                continue;
            }

            var changedPercent = FrameDifferencer.ComputeChangedPercentage(
                previousFrame, currentFrame, _settings.PixelTolerance);

            if (changedPercent > _settings.Threshold)
            {
                _logger.LogInformation(
                    "Motion detected: {Percent:F1}% pixels changed",
                    changedPercent);

                var path = await CaptureWriter.SaveAsync(
                    currentFrame,
                    _settings.CaptureDirectory,
                    now,
                    stoppingToken);

                _logger.LogInformation("Capture saved: {Path}", path);

                _lastCaptureTime = now;
                _logger.LogDebug(
                    "Cooldown started: {Seconds}s",
                    _settings.CooldownSeconds);
            }

            previousFrame = currentFrame;
        }
    }
}
