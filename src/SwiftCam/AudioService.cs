using Microsoft.Extensions.Options;

namespace SwiftCam;

/// <summary>
/// Background service that orchestrates audio playback scheduling based on solar times,
/// weather conditions, and mplayer process state.
/// </summary>
public class AudioService : BackgroundService
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LoopInterval = TimeSpan.FromSeconds(1);

    private readonly AudioSettings _settings;
    private readonly ISolarCalculator _solarCalculator;
    private readonly IWeatherService _weatherService;
    private readonly IAudioProcessManager _processManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AudioService> _logger;

    private PlaybackWindow? _morningWindow;
    private PlaybackWindow? _eveningWindow;
    private int _consecutiveRetries;
    private DateTime? _lastCrashDetectedAt;
    private DateTime _lastWindowCalculationDate;

    /// <summary>
    /// Gets the current state of the audio service.
    /// </summary>
    public AudioState CurrentState { get; private set; } = AudioState.Idle;

    /// <summary>
    /// Gets the reason string describing the current state (max 200 chars).
    /// </summary>
    public string CurrentReason { get; private set; } = "Outside playback window";

    /// <summary>
    /// Gets the currently active playback window, if any.
    /// </summary>
    public PlaybackWindow? CurrentWindow { get; private set; }

    /// <summary>
    /// Gets the next scheduled playback window.
    /// </summary>
    public PlaybackWindow? NextWindow { get; private set; }

    /// <summary>
    /// Gets today's morning playback window.
    /// </summary>
    public PlaybackWindow? MorningWindow => _morningWindow;

    /// <summary>
    /// Gets today's evening playback window.
    /// </summary>
    public PlaybackWindow? EveningWindow => _eveningWindow;

    public AudioService(
        IOptions<AudioSettings> options,
        ISolarCalculator solarCalculator,
        IWeatherService weatherService,
        IAudioProcessManager processManager,
        TimeProvider timeProvider,
        ILogger<AudioService> logger)
    {
        _settings = options.Value;
        _solarCalculator = solarCalculator;
        _weatherService = weatherService;
        _processManager = processManager;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AudioService starting");

        RecalculateWindows();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                CheckForMidnightRecalculation();
                EvaluateState();

                await Task.Delay(LoopInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        // Graceful shutdown: stop mplayer if running
        await ShutdownAsync();
    }

    private void CheckForMidnightRecalculation()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var today = now.Date;

        if (today != _lastWindowCalculationDate)
        {
            _logger.LogInformation("Recalculating playback windows for {Date:yyyy-MM-dd}", today);
            RecalculateWindows();
        }
    }

    private void RecalculateWindows()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var today = now.Date;

        var solarTimes = _solarCalculator.Calculate(_settings.Latitude, _settings.Longitude, today);
        var (morning, evening) = PlaybackWindowCalculator.Calculate(solarTimes, _settings, today);

        _morningWindow = morning;
        _eveningWindow = evening;
        _lastWindowCalculationDate = today;

        if (morning is null && evening is null)
        {
            _logger.LogWarning(
                "No playback windows for {Date:yyyy-MM-dd} at lat={Lat}, lon={Lon} (polar edge case)",
                today, _settings.Latitude, _settings.Longitude);
        }
        else
        {
            _logger.LogInformation(
                "Playback windows: Morning={Morning}, Evening={Evening}",
                morning is not null ? $"{morning.Start:HH:mm}–{morning.End:HH:mm}" : "none",
                evening is not null ? $"{evening.Start:HH:mm}–{evening.End:HH:mm}" : "none");
        }

        UpdateCurrentAndNextWindows(now);
    }

    private void EvaluateState()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        UpdateCurrentAndNextWindows(now);

        var inWindow = CurrentWindow is not null;
        var weather = _weatherService.CurrentWeather;
        var isSuppressed = IsWeatherSuppressed(weather);
        var isPlaying = _processManager.IsPlaying;

        switch (CurrentState)
        {
            case AudioState.Idle:
                EvaluateIdleState(inWindow, isSuppressed, now);
                break;

            case AudioState.Playing:
                EvaluatePlayingState(inWindow, isSuppressed, isPlaying, now);
                break;

            case AudioState.Suppressed:
                EvaluateSuppressedState(inWindow, isSuppressed, now);
                break;

            case AudioState.Stopped:
                EvaluateStoppedState(inWindow, now);
                break;

            case AudioState.Error:
                EvaluateErrorState(inWindow);
                break;
        }
    }

    private void EvaluateIdleState(bool inWindow, bool isSuppressed, DateTime now)
    {
        if (!inWindow)
        {
            SetState(AudioState.Idle, "Outside playback window");
            return;
        }

        if (isSuppressed)
        {
            SetState(AudioState.Suppressed, GetSuppressionReason());
            return;
        }

        // Entering a window — check file and start playback
        if (!AudioFileExists())
        {
            SetState(AudioState.Error, $"Audio file not found: {_settings.AudioFilePath}");
            return;
        }

        StartPlayback(now);
    }

    private void EvaluatePlayingState(bool inWindow, bool isSuppressed, bool isPlaying, DateTime now)
    {
        // Window ended
        if (!inWindow)
        {
            StopPlayback();
            _logger.LogInformation("Audio playback stopped: window ended");
            SetState(AudioState.Idle, "Outside playback window");
            return;
        }

        // Weather suppression detected
        if (isSuppressed)
        {
            StopPlayback();
            _logger.LogInformation("Audio playback stopped: {Reason}", GetSuppressionReason());
            SetState(AudioState.Suppressed, GetSuppressionReason());
            return;
        }

        // mplayer crash detected (IsPlaying became false unexpectedly)
        if (!isPlaying)
        {
            _consecutiveRetries++;
            _lastCrashDetectedAt = now;

            if (_consecutiveRetries > MaxRetries)
            {
                SetState(AudioState.Error, $"Max retries ({MaxRetries}) exceeded");
                _logger.LogError("Audio playback failed after {MaxRetries} consecutive restart attempts", MaxRetries);
                return;
            }

            SetState(AudioState.Stopped, $"mplayer crashed, retry {_consecutiveRetries}/{MaxRetries} in 3s");
            _logger.LogWarning("mplayer process terminated unexpectedly, retry {Attempt}/{Max}", _consecutiveRetries, MaxRetries);
        }
    }

    private void EvaluateSuppressedState(bool inWindow, bool isSuppressed, DateTime now)
    {
        // Window ended
        if (!inWindow)
        {
            SetState(AudioState.Idle, "Outside playback window");
            return;
        }

        // Weather cleared — resume playback
        if (!isSuppressed)
        {
            if (!AudioFileExists())
            {
                SetState(AudioState.Error, $"Audio file not found: {_settings.AudioFilePath}");
                return;
            }

            _logger.LogInformation("Weather cleared, resuming audio playback");
            StartPlayback(now);
            return;
        }

        // Still suppressed — update reason in case it changed
        SetState(AudioState.Suppressed, GetSuppressionReason());
    }

    private void EvaluateStoppedState(bool inWindow, DateTime now)
    {
        // Window ended while waiting
        if (!inWindow)
        {
            SetState(AudioState.Idle, "Outside playback window");
            _consecutiveRetries = 0;
            return;
        }

        // Check if enough time has elapsed since crash for restart
        if (_lastCrashDetectedAt is not null && (now - _lastCrashDetectedAt.Value) >= RestartDelay)
        {
            if (_consecutiveRetries > MaxRetries)
            {
                SetState(AudioState.Error, $"Max retries ({MaxRetries}) exceeded");
                _logger.LogError("Audio playback failed after {MaxRetries} consecutive restart attempts", MaxRetries);
                return;
            }

            if (!AudioFileExists())
            {
                SetState(AudioState.Error, $"Audio file not found: {_settings.AudioFilePath}");
                return;
            }

            TryRestart(now);
        }
    }

    private void EvaluateErrorState(bool inWindow)
    {
        // Error → Idle: next window begins (reset retries)
        // We detect this by checking if we've transitioned from "not in window" to "in a new window"
        if (!inWindow)
        {
            // Reset when we leave a window so we're clean for the next one
            _consecutiveRetries = 0;
            SetState(AudioState.Idle, "Outside playback window");
        }
    }

    private void StartPlayback(DateTime now)
    {
        try
        {
            _processManager.Start(_settings.AudioFilePath);
            _consecutiveRetries = 0;
            var windowName = GetCurrentWindowName();
            SetState(AudioState.Playing, $"{windowName} session");
            _logger.LogInformation("Audio playback started: {Window}", windowName);
        }
        catch (InvalidOperationException ex)
        {
            SetState(AudioState.Error, $"mplayer not found: {ex.Message}");
            _logger.LogError(ex, "Failed to start mplayer");
        }
    }

    private void TryRestart(DateTime now)
    {
        try
        {
            _processManager.Start(_settings.AudioFilePath);
            var windowName = GetCurrentWindowName();
            SetState(AudioState.Playing, $"{windowName} session (restarted)");
            _logger.LogInformation("Audio playback restarted (attempt {Attempt})", _consecutiveRetries);
        }
        catch (InvalidOperationException ex)
        {
            SetState(AudioState.Error, $"mplayer not found: {ex.Message}");
            _logger.LogError(ex, "Failed to restart mplayer");
        }
    }

    private void StopPlayback()
    {
        try
        {
            _processManager.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping audio playback");
        }
    }

    private async Task ShutdownAsync()
    {
        if (_processManager.IsPlaying)
        {
            _logger.LogInformation("AudioService shutting down, stopping mplayer");
            try
            {
                await _processManager.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping mplayer during shutdown");
            }
        }

        SetState(AudioState.Idle, "Service stopped");
    }

    private void UpdateCurrentAndNextWindows(DateTime now)
    {
        var windows = new List<PlaybackWindow>();
        if (_morningWindow is not null) windows.Add(_morningWindow);
        if (_eveningWindow is not null) windows.Add(_eveningWindow);

        CurrentWindow = windows.FirstOrDefault(w => now >= w.Start && now < w.End);
        NextWindow = windows
            .Where(w => w.Start > now)
            .OrderBy(w => w.Start)
            .FirstOrDefault();
    }

    private bool IsWeatherSuppressed(WeatherState weather)
    {
        return weather.PrecipitationMm > 0 || weather.WindSpeedKph > _settings.WindSpeedThresholdKph;
    }

    private string GetSuppressionReason()
    {
        var weather = _weatherService.CurrentWeather;
        if (weather.PrecipitationMm > 0 && weather.WindSpeedKph > _settings.WindSpeedThresholdKph)
        {
            return "Paused: rain and high wind detected";
        }
        if (weather.PrecipitationMm > 0)
        {
            return "Paused: rain detected";
        }
        return "Paused: high wind detected";
    }

    private string GetCurrentWindowName()
    {
        if (CurrentWindow == _morningWindow) return "Morning";
        if (CurrentWindow == _eveningWindow) return "Evening";
        return "Playback";
    }

    private bool AudioFileExists()
    {
        return File.Exists(_settings.AudioFilePath);
    }

    private void SetState(AudioState state, string reason)
    {
        CurrentState = state;
        // Cap reason at 200 characters per requirement 7.3
        CurrentReason = reason.Length > 200 ? reason[..200] : reason;
    }
}
