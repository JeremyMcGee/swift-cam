# Design Document: Motion-Triggered Capture

## Overview

This feature adds motion detection to the SwiftCam birdbox camera. A `MotionDetector` background service subscribes to the existing `IFrameBroadcaster`, compares consecutive frames using pixel-level luminance differencing via SkiaSharp, and saves a JPEG capture to disk when the changed-pixel percentage exceeds a configurable threshold. A cooldown mechanism prevents burst captures during sustained motion. Configuration follows the same `IOptions<T>` + `IValidateOptions<T>` pattern already established by `CameraSettings`.

## Architecture

```mermaid
graph TD
    subgraph "Frame Pipeline"
        A[CameraService] -->|PublishFrame| B[IFrameBroadcaster]
        B -->|Subscribe| C[MotionDetector]
        B -->|Subscribe| D[MJPEG Streaming Clients]
    end

    subgraph "Motion Detection"
        C -->|Receive frame| E[FrameDifferencer]
        E -->|Changed_Pixel_Percentage| F{Threshold exceeded?}
        F -->|Yes + not in cooldown| G[CaptureWriter]
        F -->|No or in cooldown| H[Discard / wait for next frame]
        G -->|Save JPEG| I[Disk: captures/]
    end

    subgraph "Configuration"
        J[appsettings.json "Motion" section] --> K[IOptions&lt;MotionSettings&gt;]
        K --> L[MotionSettingsValidator]
        L -->|Valid| C
    end
```

## Components and Interfaces

### Component 1: MotionSettings

**Purpose**: Strongly-typed POCO for motion detection configuration with sensible defaults.

```csharp
public class MotionSettings
{
    public double Threshold { get; set; } = 5.0;
    public int CooldownSeconds { get; set; } = 300;
    public string CaptureDirectory { get; set; } = "captures";
    public int PixelTolerance { get; set; } = 30;
}
```

**Responsibilities**:
- Hold the four configurable motion detection parameters
- Provide sensible defaults for birdbox usage

**Design Rationale**: `PixelTolerance` (per-pixel luminance difference below which a pixel is considered "unchanged") is separated from `Threshold` (the overall percentage of changed pixels needed to trigger). This two-level approach lets users tune out sensor noise (PixelTolerance) independently from the motion sensitivity (Threshold).

### Component 2: MotionSettingsValidator

**Purpose**: Validates motion settings at startup using the `IValidateOptions<T>` pattern.

```csharp
public class MotionSettingsValidator : IValidateOptions<MotionSettings>
{
    public ValidateOptionsResult Validate(string? name, MotionSettings options)
    {
        var failures = new List<string>();

        if (options.Threshold < 0.1 || options.Threshold > 100.0)
            failures.Add($"Threshold must be between 0.1 and 100.0, got {options.Threshold}.");

        if (options.CooldownSeconds < 0 || options.CooldownSeconds > 86400)
            failures.Add($"CooldownSeconds must be between 0 and 86400, got {options.CooldownSeconds}.");

        if (string.IsNullOrWhiteSpace(options.CaptureDirectory))
            failures.Add("CaptureDirectory must not be empty.");

        if (options.PixelTolerance < 1 || options.PixelTolerance > 255)
            failures.Add($"PixelTolerance must be between 1 and 255, got {options.PixelTolerance}.");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
```

### Component 3: FrameDifferencer (static utility)

**Purpose**: Decodes two JPEG frames using SkiaSharp, converts to grayscale luminance, and computes the percentage of pixels whose absolute luminance difference exceeds the per-pixel tolerance.

```csharp
public static class FrameDifferencer
{
    public static double ComputeChangedPercentage(
        byte[] previousFrame,
        byte[] currentFrame,
        int pixelTolerance)
    {
        using var prev = SKBitmap.Decode(previousFrame);
        using var curr = SKBitmap.Decode(currentFrame);

        if (prev is null || curr is null)
            return 0.0;

        var width = Math.Min(prev.Width, curr.Width);
        var height = Math.Min(prev.Height, curr.Height);
        var totalPixels = width * height;

        if (totalPixels == 0)
            return 0.0;

        var changedPixels = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var prevLum = GetLuminance(prev.GetPixel(x, y));
                var currLum = GetLuminance(curr.GetPixel(x, y));

                if (Math.Abs(prevLum - currLum) > pixelTolerance)
                    changedPixels++;
            }
        }

        return (double)changedPixels / totalPixels * 100.0;
    }

    private static int GetLuminance(SKColor color)
    {
        // ITU-R BT.601 luminance formula
        return (int)(0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue);
    }
}
```

**Design Rationale**:
- Static class (like `TimestampOverlay`) since it holds no state
- Uses ITU-R BT.601 luminance (standard for video) rather than simple averaging
- Takes `Math.Min` of dimensions so mismatched frame sizes don't crash — just compares the overlapping region
- Returns 0.0 when frames can't be decoded (defensive — avoids false positives)

### Component 4: CaptureWriter (static utility)

**Purpose**: Writes a JPEG frame to disk with a timestamped filename, creating the directory if needed.

```csharp
public static class CaptureWriter
{
    public static string GenerateFilename(DateTime timestamp)
    {
        return timestamp.ToString("yyyy-MMM-dd_HH-mm-ss") + ".jpg";
    }

    public static async Task<string> SaveAsync(
        byte[] jpegData,
        string captureDirectory,
        DateTime timestamp,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(captureDirectory);

        var filename = GenerateFilename(timestamp);
        var fullPath = Path.Combine(captureDirectory, filename);

        await File.WriteAllBytesAsync(fullPath, jpegData, ct);

        return fullPath;
    }
}
```

**Design Rationale**:
- Static class since it only performs file I/O with no internal state
- `GenerateFilename` is separated for testability
- `DateTime` is passed in (not captured internally) for deterministic testing
- `Directory.CreateDirectory` is idempotent — safe to call every time

### Component 5: MotionDetector (BackgroundService)

**Purpose**: Subscribes to frame broadcast, runs the detection loop comparing consecutive frames, triggers captures when motion exceeds threshold, and enforces cooldown.

```csharp
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
```

**Design Rationale**:
- Uses `TimeProvider` (new in .NET 8) for testability — can inject `FakeTimeProvider` in tests
- Comparison is skipped during cooldown (performance optimisation: avoids decoding frames unnecessarily)
- `previousFrame` is always updated so the next comparison after cooldown uses a recent frame

## Data Models

### MotionSettings

| Property | Type | Default | Valid Range | Description |
|----------|------|---------|-------------|-------------|
| Threshold | double | 5.0 | 0.1–100.0 | Percentage of changed pixels to trigger motion |
| CooldownSeconds | int | 300 | 0–86400 | Seconds between captures |
| CaptureDirectory | string | "captures" | non-empty | Path for saved captures |
| PixelTolerance | int | 30 | 1–255 | Per-pixel luminance difference to count as "changed" |

### Configuration Binding (appsettings.json)

```json
{
  "Motion": {
    "Threshold": 5.0,
    "CooldownSeconds": 300,
    "CaptureDirectory": "captures",
    "PixelTolerance": 30
  }
}
```

### Capture Filename Format

Pattern: `yyyy-MMM-dd_HH-mm-ss.jpg`

Examples:
- `2025-Jan-15_14-30-22.jpg`
- `2025-Jul-04_08-00-01.jpg`

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Frame differencing correctness

*For any* two pixel arrays of equal dimensions and any pixel tolerance value, the `ComputeChangedPercentage` function shall return the ratio of pixels whose absolute luminance difference exceeds the tolerance, expressed as a percentage between 0.0 and 100.0 inclusive.

**Validates: Requirements 1.1**

### Property 2: Motion classification completeness

*For any* changed-pixel percentage and configured threshold, motion is detected if and only if the percentage is strictly greater than the threshold.

**Validates: Requirements 1.2, 1.3**

### Property 3: Capture file round-trip

*For any* valid byte array representing JPEG data and any valid directory path, calling `CaptureWriter.SaveAsync` shall produce a file on disk whose contents are byte-for-byte identical to the input data.

**Validates: Requirements 3.1**

### Property 4: Filename format consistency

*For any* valid DateTime value, `CaptureWriter.GenerateFilename` shall produce a string matching the regex pattern `^\d{4}-[A-Z][a-z]{2}-\d{2}_\d{2}-\d{2}-\d{2}\.jpg$` and the date/time components in the filename shall correspond to the input DateTime.

**Validates: Requirements 3.2**

### Property 5: Cooldown state machine

*For any* sequence of motion events with timestamps and a configured cooldown duration, a capture shall be triggered only when the elapsed time since the previous capture exceeds the cooldown duration.

**Validates: Requirements 4.1, 4.2, 4.3**

## Error Handling

### Frame Decode Failure

**Condition**: SkiaSharp fails to decode a JPEG frame (corrupted data)
**Response**: `FrameDifferencer` returns 0.0 (no motion), effectively skipping that frame pair
**Rationale**: A single corrupted frame should not trigger false positives or crash the service

### Disk Write Failure

**Condition**: `File.WriteAllBytesAsync` throws (disk full, permissions, invalid path)
**Response**: Exception propagates up to the `ExecuteAsync` loop. The BackgroundService logs the error but continues processing subsequent frames. The capture is lost but detection continues.
**Recovery**: Operator reviews logs and resolves disk/permission issue

### Subscription Channel Closed

**Condition**: `WaitForFrameAsync` throws `ChannelClosedException` (camera stopped)
**Response**: The `OperationCanceledException` path in `ExecuteAsync` handles graceful shutdown. If the camera restarts, the MotionDetector will need to be restarted as well (managed by the host).

### Settings Validation Failure

**Condition**: One or more MotionSettings values are outside valid ranges
**Response**: `OptionsValidationException` thrown at startup via `ValidateOnStart`. Application logs the error and terminates with non-zero exit code.
**Recovery**: User corrects `appsettings.json` and restarts.

## Testing Strategy

### Unit Tests (xUnit)

- **MotionSettings defaults**: Verify Threshold=5.0, CooldownSeconds=300, CaptureDirectory="captures", PixelTolerance=30
- **MotionSettingsValidator**: Boundary values (valid edge, just-out-of-range), empty CaptureDirectory
- **CaptureWriter.GenerateFilename**: Specific DateTime values produce expected strings
- **CaptureWriter.SaveAsync**: Creates directory when missing; file contents match input
- **FrameDifferencer with identical frames**: Returns 0.0
- **FrameDifferencer with fully different frames**: Returns ~100.0
- **Logging verification**: Mock logger confirms motion/capture/cooldown messages

### Property-Based Tests (FsCheck.Xunit)

**Library**: FsCheck via `FsCheck.Xunit` (already in test project)
**Configuration**: Minimum 100 iterations per property

Each property test is tagged with a comment referencing its design property:

- **Feature: motion-capture, Property 1**: Generate random grayscale pixel grids, compute expected percentage manually, assert `FrameDifferencer` matches
- **Feature: motion-capture, Property 2**: Generate random (percentage, threshold) pairs, assert detection result equals `percentage > threshold`
- **Feature: motion-capture, Property 3**: Generate random byte arrays, write to temp directory, read back, assert equality
- **Feature: motion-capture, Property 4**: Generate random DateTime values, assert filename matches format regex and components round-trip
- **Feature: motion-capture, Property 5**: Generate random event sequences with timestamps, simulate cooldown logic, assert captures fire only outside cooldown windows

### Integration Tests

- **DI wiring**: Verify `MotionSettings` binds from in-memory configuration
- **BackgroundService lifecycle**: Start/stop `MotionDetector` with a mock broadcaster, verify subscription and disposal
- **End-to-end detection**: Feed synthetic frames through a mock broadcaster, verify capture file appears on disk
