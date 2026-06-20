# SwiftCam

A lightweight MJPEG streaming server with motion-triggered image capture and automated audio attraction, designed for Raspberry Pi birdbox cameras.

## What it does

SwiftCam captures video from a Raspberry Pi camera module and serves it as a live MJPEG stream over HTTP. In parallel, it watches for motion by comparing consecutive frames and saves a timestamped JPEG whenever significant change is detected. It also plays looped audio through an attached speaker during scheduled time windows to attract swifts to the birdbox — ideal for swift conservation without manual intervention.

## Features

- **Live MJPEG streaming** — View your camera feed in any browser at `http://<pi-ip>:8080`
- **Greyscale output** — Frames are converted to greyscale before streaming, eliminating the purple cast from NoIR camera modules
- **Motion detection** — Pixel-level luminance differencing detects movement between frames
- **Automatic capture** — Saves a JPEG when motion exceeds a configurable threshold
- **Cooldown period** — Prevents burst captures during sustained motion
- **Audio attraction** — Plays looped swift call audio during morning and evening windows calculated from solar events
- **Weather-aware** — Automatically suppresses audio during rain or high wind (swifts don't fly in those conditions)
- **Capture gallery** — Browse motion-captured images in a scrollable thumbnail grid below the stream
- **Manual capture** — Take a snapshot from the live feed with a single click
- **Capture deletion** — Delete unwanted captures from the gallery with a confirmation step
- **Status API** — `GET /api/audio-status` returns current playback state as JSON
- **Capture API** — `GET /api/captures` lists captures; `GET /api/captures/{filename}` serves images; `POST /api/captures` takes a manual capture; `DELETE /api/captures/{filename}` removes a capture
- **Web status panel** — Audio state displayed alongside the camera stream
- **Configurable sensitivity** — Tune threshold, pixel tolerance, and cooldown via `appsettings.json`
- **Multi-client support** — Up to 10 simultaneous stream viewers, independent of motion detection

## Requirements

- Raspberry Pi with a camera module (standard or NoIR)
- `rpicam-vid` or `libcamera-vid` available on the system
- `mplayer` installed (for audio playback)
- .NET 10 SDK

## Quick start

```bash
# Build
dotnet build src/SwiftCam/SwiftCam.csproj

# Run
dotnet run --project src/SwiftCam
```

Open `http://<pi-ip>:8080` in a browser to view the stream. Captured images are saved to the `captures/` directory by default and can be browsed in the gallery panel below the stream.

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
  },
  "Audio": {
    "AudioFilePath": "audio/swift-call.mp3",
    "Latitude": 51.9,
    "Longitude": -2.07,
    "MorningOffsetMinutes": 0,
    "MorningDurationMinutes": 210,
    "EveningPreSunsetMinutes": 150,
    "WeatherPollIntervalMinutes": 15,
    "WindSpeedThresholdKph": 40
  }
}
```

### Camera settings

| Setting | Description | Default |
|---------|-------------|---------|
| `Camera:Width` | Capture width in pixels | 640 |
| `Camera:Height` | Capture height in pixels | 480 |
| `Camera:Framerate` | Frames per second | 15 |
| `Camera:Quality` | JPEG quality (1–100) | 80 |

### Motion settings

| Setting | Description | Default |
|---------|-------------|---------|
| `Motion:Threshold` | Percentage of changed pixels to trigger capture (0.1–100) | 5.0 |
| `Motion:CooldownSeconds` | Seconds between captures (0–86400) | 300 |
| `Motion:CaptureDirectory` | Output folder for captured images | captures |
| `Motion:PixelTolerance` | Per-pixel luminance difference to count as "changed" (1–255) | 30 |

### Audio settings

| Setting | Description | Default | Range |
|---------|-------------|---------|-------|
| `Audio:AudioFilePath` | Path to the audio file to play | audio/swift-call.mp3 | Non-empty |
| `Audio:Latitude` | Location latitude for solar calculations | 51.9 | -90 to 90 |
| `Audio:Longitude` | Location longitude for solar calculations | -2.07 | -180 to 180 |
| `Audio:MorningOffsetMinutes` | Offset from civil twilight for morning start | 0 | -60 to 240 |
| `Audio:MorningDurationMinutes` | Length of morning playback window | 210 | 1 to 720 |
| `Audio:EveningPreSunsetMinutes` | Minutes before sunset to start evening playback | 150 | 1 to 480 |
| `Audio:WeatherPollIntervalMinutes` | How often to check weather conditions | 15 | 1 to 60 |
| `Audio:WindSpeedThresholdKph` | Wind speed above which playback is suppressed | 40 | 1 to 120 |

## Audio attraction

The audio system plays a looped recording through an attached speaker to attract swifts to the birdbox. Playback is scheduled around two daily windows:

- **Morning window** — Starts at civil twilight + offset, runs for the configured duration
- **Evening window** — Starts before sunset, ends at sunset

Playback is automatically suppressed during rain (precipitation > 0mm) or high wind (above threshold). The system polls the [Open-Meteo API](https://open-meteo.com/) for weather conditions.

The audio state machine handles crashes gracefully — if mplayer terminates unexpectedly, it retries up to 5 times before entering an error state until the next window.

### Status endpoint

`GET /api/audio-status` returns JSON:

```json
{
  "state": "Playing",
  "reason": "Morning session",
  "currentWindowStart": "2025-06-15T04:30:00Z",
  "currentWindowEnd": "2025-06-15T08:00:00Z",
  "nextWindowStart": "2025-06-15T18:30:00Z"
}
```

## Capture gallery

The web page includes a gallery panel below the audio status section that displays thumbnails of all captured images (both motion-triggered and manual). The gallery:

- Loads automatically on page open
- Displays thumbnails in a responsive CSS grid
- Parses timestamps from filenames and shows them below each thumbnail
- Clicking a thumbnail opens the full-resolution image in a new tab
- Includes a "Take Capture" button to manually snapshot the live feed
- Includes a delete button (×) on each thumbnail with a confirmation dialog
- Shows loading states during capture and delete operations
- Displays auto-dismissing error messages (5 seconds) on failure

### Capture endpoints

`GET /api/captures` — Returns a JSON array of `.jpg` filenames sorted most-recent-first:

```json
["2025-Jun-15_14-30-22.jpg", "2025-Jun-15_09-12-05.jpg"]
```

`GET /api/captures/{filename}` — Serves a capture image with `Content-Type: image/jpeg`. Returns 400 for invalid filenames (path traversal, wrong extension) and 404 for missing files.

`POST /api/captures` — Captures a frame from the live camera feed and saves it to disk. Returns 201 with the filename on success, 503 if no frame is available within 5 seconds, or 500 on file system errors.

```json
{"filename": "2025-Jun-15_14-30-22.jpg"}
```

`DELETE /api/captures/{filename}` — Deletes a capture image. Returns 204 on success, 400 for invalid filenames, 404 if not found, or 500 on file system errors.

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
