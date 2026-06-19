# Architecture

This document describes the internal design of SwiftCam — how the components fit together, how data flows through the system, and the rationale behind key decisions.

## System overview

SwiftCam is a single-process ASP.NET Core application that runs two concurrent pipelines:

1. **Streaming pipeline** — Captures frames from the camera hardware, broadcasts them to connected HTTP clients as an MJPEG stream.
2. **Motion detection pipeline** — Subscribes to the same frame broadcast, compares consecutive frames, and saves a JPEG to disk when motion is detected.

Both pipelines share the same frame source via a publish-subscribe broadcaster, so motion detection runs independently without affecting stream latency.

```
┌─────────────────┐       ┌──────────────────┐       ┌─────────────────────┐
│  CameraService  │──────▶│  FrameBroadcaster │──┬──▶│  MJPEG Stream (HTTP) │
│  (rpicam-vid)   │ frame │  (pub-sub hub)    │  │   └─────────────────────┘
└─────────────────┘       └──────────────────┘  │
                                                 │   ┌─────────────────────┐
                                                 └──▶│  MotionDetector     │
                                                     │  → FrameDifferencer │
                                                     │  → CaptureWriter    │
                                                     └─────────────────────┘
```

## Component details

### CameraService

A `BackgroundService` that spawns `rpicam-vid` (or `libcamera-vid` as fallback) as a child process. It reads raw JPEG frames from the process's stdout by scanning for SOI (`FF D8`) and EOI (`FF D9`) markers, applies a timestamp overlay, and publishes each complete frame to the broadcaster.

If the camera process exits within 5 seconds of starting, the application terminates (camera not detected). If it crashes mid-stream, one restart is attempted before terminating.

### FrameBroadcaster

A thread-safe pub-sub hub. Each subscriber gets an independent bounded channel (capacity 3, drop-oldest policy) so a slow consumer never blocks other subscribers or the camera service. Supports up to 10 concurrent subscriptions.

Key interface:
- `PublishFrame(byte[] jpegData)` — Non-blocking fan-out to all subscribers
- `Subscribe()` — Returns an `IFrameSubscription` for reading frames
- Subscriptions are `IDisposable` — disposing unregisters from the broadcaster

### MjpegStreamWriter

A static utility that writes the MJPEG multipart HTTP response. It subscribes to the broadcaster, writes each frame with the appropriate `multipart/x-mixed-replace` boundary headers, and exits cleanly when the client disconnects.

### MotionDetector

A `BackgroundService` that implements the detection loop:

1. Subscribe to the broadcaster
2. Wait for a frame
3. If no previous frame exists, store it and loop
4. Check if cooldown has elapsed (skip comparison if still in cooldown)
5. Compute changed-pixel percentage via `FrameDifferencer`
6. If percentage > threshold, save via `CaptureWriter` and enter cooldown
7. Update previous frame reference and loop

The cooldown comparison skips frame differencing entirely during the cooldown window. This is intentional — it avoids unnecessary JPEG decoding work while the system is idle.

Uses `TimeProvider` for time operations, making the service fully testable with `FakeTimeProvider`.

### FrameDifferencer

A static utility that computes the percentage of pixels that differ between two JPEG frames:

1. Decode both frames with SkiaSharp (`SKBitmap.Decode`)
2. For each pixel, compute luminance using ITU-R BT.601: `0.299R + 0.587G + 0.114B`
3. If `|prevLuminance - currLuminance| > pixelTolerance`, count it as changed
4. Return `changedPixels / totalPixels * 100.0`

Edge cases:
- Returns 0.0 if either frame fails to decode (avoids false positives from corruption)
- Uses `Math.Min` of dimensions for mismatched frame sizes (compares overlapping region)

### CaptureWriter

A static utility for writing captures to disk:
- `GenerateFilename(DateTime)` — Produces `yyyy-MMM-dd_HH-mm-ss.jpg` (e.g., `2025-Jan-15_14-30-22.jpg`)
- `SaveAsync(byte[], string, DateTime, CancellationToken)` — Creates the directory if missing, writes the file, returns the full path

The `DateTime` is passed in rather than captured internally, making the output deterministic and testable.

### Configuration classes

**CameraSettings** — Width, Height, Framerate, Quality. Validated at startup via `CameraSettingsValidator`.

**MotionSettings** — Threshold, CooldownSeconds, CaptureDirectory, PixelTolerance. Validated at startup via `MotionSettingsValidator`.

Both follow the `IOptions<T>` + `IValidateOptions<T>` + `ValidateOnStart` pattern. Invalid configuration causes the application to fail fast with a clear error message.

## Dependency injection graph

```
WebApplication Host
├── CameraService (HostedService)
│   ├── IOptions<CameraSettings>
│   ├── IFrameBroadcaster
│   ├── IHostApplicationLifetime
│   └── ILogger<CameraService>
├── MotionDetector (HostedService)
│   ├── IOptions<MotionSettings>
│   ├── IFrameBroadcaster
│   ├── ILogger<MotionDetector>
│   └── TimeProvider
├── FrameBroadcaster (Singleton, implements IFrameBroadcaster)
├── TimeProvider.System (Singleton)
├── CameraSettingsValidator (Singleton)
└── MotionSettingsValidator (Singleton)
```

## Data flow

### Frame lifecycle

```
rpicam-vid stdout
    → CameraService (SOI/EOI parsing + timestamp overlay)
    → FrameBroadcaster.PublishFrame()
    → Channel.TryWrite() to each subscriber's bounded channel
    → Subscriber reads via WaitForFrameAsync()
```

### Motion capture flow

```
Frame N-1 (stored)  ─┐
                     ├─ FrameDifferencer.ComputeChangedPercentage()
Frame N (received)  ─┘        │
                              ▼
                    changedPercent > threshold?
                              │
                    yes + not in cooldown
                              │
                              ▼
                    CaptureWriter.SaveAsync()
                    → creates directory if needed
                    → writes yyyy-MMM-dd_HH-mm-ss.jpg
                    → enters cooldown
```

## Design decisions

**Why pixel-level luminance rather than more advanced algorithms?**
This runs on a Raspberry Pi with limited CPU. Per-pixel luminance comparison is simple, predictable, and fast enough at 640×480. More sophisticated approaches (optical flow, background subtraction models) would require significant CPU or a GPU-accelerated library.

**Why a two-level sensitivity model (PixelTolerance + Threshold)?**
`PixelTolerance` filters out sensor noise at the individual pixel level — small fluctuations from camera electronics. `Threshold` determines what percentage of the frame needs to change to count as "motion." This separation lets users tune out noise without reducing sensitivity to actual movement.

**Why skip frame comparison during cooldown?**
Decoding two JPEGs per frame is the most CPU-intensive operation. During cooldown, no capture will happen regardless of motion, so the work is wasted. Skipping it keeps CPU usage minimal when the system is in its idle state.

**Why static classes for FrameDifferencer and CaptureWriter?**
Neither holds state. They're pure functions (input → output) and don't need dependency injection or lifecycle management. This also makes them trivial to test — no mocking infrastructure needed.

**Why TimeProvider instead of DateTime.Now?**
`TimeProvider` (introduced in .NET 8) enables deterministic time in tests via `FakeTimeProvider`, without needing to mock static methods or introduce custom clock abstractions.

**Why bounded channels with drop-oldest?**
If a subscriber (slow HTTP client) can't keep up, we want to serve the latest frame rather than buffering stale ones. Drop-oldest keeps memory bounded and ensures viewers always see near-real-time content.

## Testing strategy

The test suite covers three layers:

**Unit tests** — Validate settings defaults and boundary conditions for both validators.

**Property-based tests** (FsCheck, 100 iterations each):
1. Frame differencing correctness — Generated pixel grids produce expected percentages
2. Motion classification completeness — `percentage > threshold` contract holds for all values
3. Capture file round-trip — Written bytes equal read bytes for arbitrary data
4. Filename format consistency — All DateTime values produce valid filenames matching the expected regex
5. Cooldown state machine — Captures fire only outside cooldown windows for arbitrary event sequences

**Integration tests** — Verify DI wiring (settings bind from config) and service lifecycle (subscribe on start, dispose on stop).

## File layout

```
src/SwiftCam/
├── Program.cs                  Entry point, DI, routes
├── appsettings.json            Configuration
├── CameraService.cs            Camera process management
├── CameraSettings.cs           Camera config POCO
├── CameraSettingsValidator.cs  Camera config validation
├── FrameBroadcaster.cs         Pub-sub frame distribution
├── FrameDifferencer.cs         Pixel luminance comparison
├── CaptureWriter.cs            Disk write utility
├── MotionDetector.cs           Motion detection service
├── MotionSettings.cs           Motion config POCO
├── MotionSettingsValidator.cs  Motion config validation
├── MjpegStreamWriter.cs        HTTP MJPEG response writer
├── MaxClientsExceededException.cs
└── Interfaces/
    ├── ICameraService.cs
    ├── IFrameBroadcaster.cs
    └── IFrameSubscription.cs

tests/SwiftCam.Tests/
├── Unit/
│   ├── CameraServiceConfigTests.cs
│   ├── MotionSettingsTests.cs
│   ├── FrameDifferencerPropertyTests.cs
│   ├── MotionClassificationPropertyTests.cs
│   ├── CaptureWriterRoundTripPropertyTests.cs
│   ├── CaptureWriterFilenamePropertyTests.cs
│   └── CooldownStateMachinePropertyTests.cs
└── Integration/
    ├── MotionDetectorLifecycleTests.cs
    └── MotionSettingsBindingTests.cs
```
