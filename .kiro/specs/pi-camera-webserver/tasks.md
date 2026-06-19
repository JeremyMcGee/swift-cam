# Implementation Plan: Pi Camera Webserver

## Overview

Build a .NET 8 / C# application for Raspberry Pi that serves a live MJPEG video stream from the camera module over HTTP. The application uses ASP.NET Core minimal API with Kestrel, spawns `libcamera-vid` as a child process, and uses a FrameBroadcaster with bounded channels to distribute frames to multiple connected clients.

## Tasks

- [x] 1. Set up project structure and core interfaces
  - [x] 1.1 Create .NET 8 console project with ASP.NET Core dependencies
    - Create solution file and `src/SwiftCam/SwiftCam.csproj` targeting `net8.0`
    - Add `Microsoft.AspNetCore.App` framework reference
    - Create `tests/SwiftCam.Tests/SwiftCam.Tests.csproj` with xUnit, FsCheck.Xunit, and Microsoft.AspNetCore.Mvc.Testing references
    - _Requirements: 1.1, 1.2_

  - [x] 1.2 Define core interfaces (`ICameraService`, `IFrameBroadcaster`, `IFrameSubscription`)
    - Create `ICameraService` interface extending `IHostedService` with `IsRunning` property
    - Create `IFrameBroadcaster` interface with `PublishFrame`, `Subscribe`, and `ClientCount` members
    - Create `IFrameSubscription` interface extending `IDisposable` with `WaitForFrameAsync` method
    - _Requirements: 3.1, 5.1, 5.3_

- [x] 2. Implement FrameBroadcaster
  - [x] 2.1 Implement `FrameBroadcaster` class with bounded channels
    - Use `ConcurrentDictionary<Guid, Channel<byte[]>>` for subscriber management
    - Configure `BoundedChannelOptions` with capacity 3 and `DropOldest` full mode
    - Implement `PublishFrame` to write to all subscriber channels
    - Implement `Subscribe` with max-client check (10), returning `IFrameSubscription`
    - Return HTTP 503 semantics when max clients exceeded (throw or signal rejection)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x]* 2.2 Write property test: Frame broadcast delivers to all subscribers
    - **Property 4: Frame broadcast delivers to all subscribers**
    - Generator: Random subscriber count (1-10), random frame data
    - Assert all subscribers receive the published frame
    - **Validates: Requirements 5.1, 5.2**

  - [x]* 2.3 Write property test: Slow subscriber isolation
    - **Property 5: Slow subscriber isolation**
    - Generator: Random number of fast subscribers (1-9), random frames (count > channel capacity)
    - Assert fast subscribers receive all frames; slow subscriber doesn't block others
    - **Validates: Requirements 5.4**

- [x] 3. Implement CameraService
  - [x] 3.1 Implement `CameraService` as a `BackgroundService`
    - Spawn `libcamera-vid` with arguments: `-t 0 --codec mjpeg --width 640 --height 480 --framerate 15 -q 80 -n -o -`
    - Read stdout in a loop, detecting JPEG frame boundaries via SOI (0xFFD8) and EOI (0xFFD9) markers
    - Push complete frames to `IFrameBroadcaster.PublishFrame`
    - Handle process exit: log error with stderr, attempt one restart, terminate app if restart fails
    - Handle camera not detected: log error, exit with non-zero code within 5 seconds
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x]* 3.2 Write unit tests for CameraService configuration
    - Verify correct `libcamera-vid` argument construction (resolution, framerate, quality)
    - Verify error handling when process fails to start
    - _Requirements: 3.2, 3.3, 3.4_

- [x] 4. Implement MJPEG stream writer and route handlers
  - [x] 4.1 Implement `MjpegStreamWriter.WriteStreamAsync`
    - Set response `Content-Type` to `multipart/x-mixed-replace; boundary=frame`
    - Loop: await frame from subscription, write boundary `--frame\r\n`, Content-Type header, Content-Length header, blank line, JPEG data, trailing `\r\n`
    - On cancellation (client disconnect), dispose subscription and exit cleanly
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x]* 4.2 Write property test: MJPEG frame format integrity
    - **Property 2: MJPEG frame format integrity**
    - Generator: Random byte arrays (1 byte to 500KB)
    - Assert output contains boundary "--frame", Content-Type "image/jpeg", Content-Length matching input length, and original bytes unchanged
    - **Validates: Requirements 4.3**

  - [x] 4.3 Implement route handlers and HTML page
    - GET `/` returns HTML5 page with DOCTYPE, `<title>Raspberry Pi Camera</title>`, and `<img src="/stream" />`
    - GET `/stream` calls `MjpegStreamWriter.WriteStreamAsync` with client-limit check (return 503 if over 10)
    - Non-GET on `/stream` returns 405
    - Fallback catch-all returns 404 with plain-text body
    - Set Content-Type headers: `text/html` for page, `multipart/x-mixed-replace; boundary=frame` for stream
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 4.1, 4.2, 4.6, 5.5_

  - [x]* 4.4 Write property test: Unknown paths return 404
    - **Property 1: Unknown paths return 404**
    - Generator: Random valid URL path strings (excluding "/" and "/stream")
    - Assert response status is 404 with plain-text body
    - **Validates: Requirements 2.5**

  - [x]* 4.5 Write property test: Non-GET methods on /stream return 405
    - **Property 3: Non-GET methods on /stream return 405**
    - Generator: Random HTTP method strings from {POST, PUT, DELETE, PATCH, HEAD, OPTIONS, TRACE}
    - Assert response status is 405 without initiating a stream
    - **Validates: Requirements 4.6**

- [x] 5. Checkpoint - Verify core components
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Wire up Program.cs and application lifecycle
  - [x] 6.1 Implement `Program.Main` with full application wiring
    - Configure Kestrel to listen on `0.0.0.0:8080` (HTTP only)
    - Register `CameraService` as a hosted service and singleton `ICameraService`
    - Register `FrameBroadcaster` as singleton `IFrameBroadcaster`
    - Map all route handlers (GET `/`, GET `/stream`, method filter for 405, fallback 404)
    - Configure `HostOptions.ShutdownTimeout` to 5 seconds
    - Handle SIGTERM/SIGINT gracefully: stop accepting connections, kill `libcamera-vid`, cancel subscriptions
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x]* 6.2 Write unit tests for routing and HTML page
    - Use `WebApplicationFactory` to verify GET `/` returns 200, correct Content-Type, and HTML content
    - Verify GET on unknown paths returns 404
    - Verify non-GET on `/stream` returns 405
    - _Requirements: 2.1, 2.3, 2.4, 2.5, 4.6_

- [x] 7. Implement logging
  - [x] 7.1 Add structured logging throughout the application
    - Configure console logging with `TimestampFormat` for timestamps in each log entry
    - Log startup confirmation with bound address and port
    - Log client connect/disconnect events with client IP address
    - Log camera errors with source and message
    - Log port conflict errors on startup failure
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck with xUnit
- Unit tests validate specific examples and edge cases using WebApplicationFactory
- The application targets Raspberry Pi with `libcamera-vid` available on PATH

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["2.1", "3.1", "4.1"] },
    { "id": 3, "tasks": ["2.2", "2.3", "3.2", "4.2", "4.3"] },
    { "id": 4, "tasks": ["4.4", "4.5", "6.1"] },
    { "id": 5, "tasks": ["6.2", "7.1"] }
  ]
}
```
