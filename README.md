# SwiftCam

A lightweight MJPEG streaming server with motion-triggered image capture, designed for Raspberry Pi birdbox cameras.

## What it does

SwiftCam captures video from a Raspberry Pi camera module and serves it as a live MJPEG stream over HTTP. In parallel, it watches for motion by comparing consecutive frames and saves a timestamped JPEG whenever significant change is detected — ideal for recording bird activity without manual intervention.

## Features

- **Live MJPEG streaming** — View your camera feed in any browser at `http://<pi-ip>:8080`
- **Motion detection** — Pixel-level luminance differencing detects movement between frames
- **Automatic capture** — Saves a JPEG when motion exceeds a configurable threshold
- **Cooldown period** — Prevents burst captures during sustained motion
- **Configurable sensitivity** — Tune threshold, pixel tolerance, and cooldown via `appsettings.json`
- **Multi-client support** — Up to 10 simultaneous stream viewers, independent of motion detection

## Requirements

- Raspberry Pi with a camera module (or `rpicam-vid`/`libcamera-vid` available)
- .NET 10 SDK

## Quick start

```bash
# Build
dotnet build src/SwiftCam/SwiftCam.csproj

# Run
dotnet run --project src/SwiftCam
```

Open `http://<pi-ip>:8080` in a browser to view the stream. Captured images are saved to the `captures/` directory by default.

## Configuration

All settings live in `src/SwiftCam/appsettings.json`:

```json
{
  "Camera": {
    "Width": 640,
    "Height": 480,
    "Framerate": 15,
    "Quality": 80
  },
  "Motion": {
    "Threshold": 5.0,
    "CooldownSeconds": 300,
    "CaptureDirectory": "captures",
    "PixelTolerance": 30
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `Camera:Width` | Capture width in pixels | 640 |
| `Camera:Height` | Capture height in pixels | 480 |
| `Camera:Framerate` | Frames per second | 15 |
| `Camera:Quality` | JPEG quality (1–100) | 80 |
| `Motion:Threshold` | Percentage of changed pixels to trigger capture (0.1–100) | 5.0 |
| `Motion:CooldownSeconds` | Seconds between captures (0–86400) | 300 |
| `Motion:CaptureDirectory` | Output folder for captured images | captures |
| `Motion:PixelTolerance` | Per-pixel luminance difference to count as "changed" (1–255) | 30 |

## Running tests

```bash
dotnet test tests/SwiftCam.Tests
```

The test suite includes unit tests, property-based tests (FsCheck), and integration tests.

## Project structure

```
src/SwiftCam/           Application source
tests/SwiftCam.Tests/   Unit, property, and integration tests
ARCHITECTURE.md         Detailed design documentation
```

## License

MIT
