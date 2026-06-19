# Implementation Plan

## Overview

Implement motion-triggered image capture for the SwiftCam birdbox camera. The system detects motion by comparing consecutive video frames using pixel-level luminance differencing, then saves a timestamped JPEG when significant change is detected.

## Tasks

- [x] 1. Create `src/SwiftCam/MotionSettings.cs` with properties: Threshold (double, default 5.0), CooldownSeconds (int, default 300), CaptureDirectory (string, default "captures"), PixelTolerance (int, default 30)
- [x] 2. Create `src/SwiftCam/MotionSettingsValidator.cs` implementing `IValidateOptions<MotionSettings>` with range checks: Threshold 0.1–100.0, CooldownSeconds 0–86400, CaptureDirectory non-empty, PixelTolerance 1–255
- [x] 3. Create `src/SwiftCam/FrameDifferencer.cs` as a static class with `ComputeChangedPercentage(byte[] previousFrame, byte[] currentFrame, int pixelTolerance)` that decodes JPEGs via SkiaSharp, computes per-pixel luminance diff using BT.601 formula, and returns changed percentage. Handle edge cases: return 0.0 if either frame fails to decode; use Math.Min of dimensions for mismatched sizes.
- [x] 4. Create `src/SwiftCam/CaptureWriter.cs` as a static class with `GenerateFilename(DateTime timestamp)` returning `yyyy-MMM-dd_HH-mm-ss.jpg` format and `SaveAsync(byte[] jpegData, string captureDirectory, DateTime timestamp, CancellationToken ct)` that creates directory if missing and writes the file.
- [x] 5. Create `src/SwiftCam/MotionDetector.cs` extending `BackgroundService` with constructor accepting `IOptions<MotionSettings>`, `IFrameBroadcaster`, `ILogger<MotionDetector>`, and `TimeProvider`. Implement `ExecuteAsync` to subscribe to broadcaster, compare consecutive frames via FrameDifferencer, trigger CaptureWriter.SaveAsync when motion exceeds threshold and not in cooldown. Implement cooldown logic tracking `_lastCaptureTime`. Add logging at Information level for motion detected and capture saved, Debug level for cooldown start.
- [x] 6. Register services in `Program.cs`: bind MotionSettings from "Motion" config section, register MotionSettingsValidator, add ValidateOnStart, register MotionDetector as hosted service, register TimeProvider.System as singleton.
- [x] 7. Add `"Motion"` section to `appsettings.json` with default values: Threshold 5.0, CooldownSeconds 300, CaptureDirectory "captures", PixelTolerance 30.
- [x] 8. Add unit tests in `tests/SwiftCam.Tests/Unit/MotionSettingsTests.cs` verifying default values and validator accepts/rejects boundary values.
- [x] 9. Write property test (Property 1): generate random pixel grids as synthetic bitmaps, compute expected percentage manually, assert `FrameDifferencer.ComputeChangedPercentage` matches.
  - [x] 9.1 Write property test for frame differencing correctness
- [x] 10. Write property test (Property 2): generate random (percentage, threshold) pairs, assert motion classification result equals `percentage > threshold`.
  - [x] 10.1 Write property test for motion classification completeness
- [x] 11. Write property test (Property 3): generate random byte arrays, call CaptureWriter.SaveAsync to a temp directory, read file back, assert byte-for-byte equality.
  - [x] 11.1 Write property test for capture file round-trip
- [x] 12. Write property test (Property 4): generate random DateTime values, call CaptureWriter.GenerateFilename, assert output matches regex `^\d{4}-[A-Z][a-z]{2}-\d{2}_\d{2}-\d{2}-\d{2}\.jpg$` and date components correspond to input.
  - [x] 12.1 Write property test for filename format consistency
- [x] 13. Write property test (Property 5): generate random sequences of (timestamp, motionDetected) events with a configured cooldown, simulate the cooldown state machine, assert captures fire only outside cooldown windows.
  - [x] 13.1 Write property test for cooldown state machine
- [x] 14. Write integration test verifying MotionDetector subscribes to IFrameBroadcaster on start and disposes subscription on stop.
- [x] 15. Write integration test verifying MotionSettings binds correctly from in-memory configuration provider.

## Task Dependency Graph

```json
{
  "waves": [
    [1, 3, 4],
    [2, 5, 8],
    [6, 9, 10, 11, 12, 13, 14],
    [7, 15]
  ]
}
```

## Notes

- FsCheck.Xunit is already in the test project; no new dependencies needed for property tests.
- SkiaSharp is already a project dependency; used in FrameDifferencer for JPEG decode.
- TimeProvider is available in .NET 8+ (project targets net10.0); use FakeTimeProvider from Microsoft.Extensions.TimeProvider.Testing for tests.
- Property tests should run with minimum 100 iterations (FsCheck default is 100).
