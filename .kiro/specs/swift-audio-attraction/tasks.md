# Implementation Plan: Swift Audio Attraction

## Overview

Implement automated audio playback to attract swifts to the birdbox. The system plays looped audio via mplayer during two daily time windows (morning and evening) calculated from solar events, suppresses playback during adverse weather, and exposes status via an HTTP endpoint and the existing web page.

## Tasks

- [x] 1. Create AudioSettings and AudioSettingsValidator
  - [x] 1.1 Create `src/SwiftCam/AudioSettings.cs` with properties: AudioFilePath (string, default "audio/swift-call.mp3"), Latitude (double, default 51.9), Longitude (double, default -2.07), MorningOffsetMinutes (int, default 0), MorningDurationMinutes (int, default 210), EveningPreSunsetMinutes (int, default 150), WeatherPollIntervalMinutes (int, default 15), WindSpeedThresholdKph (int, default 40)
    - _Requirements: 2.1, 2.2, 3.3, 3.4, 4.3, 5.1, 5.2, 6.2, 6.5_

  - [x] 1.2 Create `src/SwiftCam/AudioSettingsValidator.cs` implementing `IValidateOptions<AudioSettings>` with range checks: AudioFilePath non-empty, Latitude -90 to 90, Longitude -180 to 180, MorningOffsetMinutes -60 to 240, MorningDurationMinutes 1 to 720, EveningPreSunsetMinutes 1 to 480, WeatherPollIntervalMinutes 1 to 60, WindSpeedThresholdKph 1 to 120. Error messages follow format from design document.
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 5.4, 5.5_

  - [x] 1.3 Write property test for settings validation (Property 7)
    - **Property 7: Settings validation rejects invalid values**
    - Generate random AudioSettings instances with at least one field outside its valid range, assert validator returns failure.
    - **Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 5.4, 5.5**

  - [x] 1.4 Write unit tests for AudioSettings defaults and AudioSettingsValidator boundary values
    - Verify all default property values match specification
    - Test exact boundary values (min-1, min, max, max+1) for each numeric property
    - _Requirements: 2.1, 2.2, 3.3, 3.4, 4.3, 5.1, 5.2, 6.2, 6.5, 9.1–9.6_

- [x] 2. Implement solar calculation and playback window logic
  - [x] 2.1 Create `src/SwiftCam/Interfaces/ISolarCalculator.cs` with `SolarTimes Calculate(double latitude, double longitude, DateTime date)` and create `src/SwiftCam/SolarTimes.cs` record with nullable `TimeOnly?` properties for CivilTwilight, Sunrise, and Sunset
    - _Requirements: 5.1, 5.2, 3.1, 4.1_

  - [x] 2.2 Create `src/SwiftCam/SolarCalculatorWrapper.cs` implementing `ISolarCalculator`, wrapping the SolarCalculator NuGet package. Return null times for polar edge cases where no solar event occurs.
    - Add NuGet reference to SolarCalculator package
    - _Requirements: 5.3, 5.6, 3.5, 4.5_

  - [x] 2.3 Create `src/SwiftCam/PlaybackWindow.cs` record with Start and End DateTime properties
    - _Requirements: 3.1, 3.2, 4.1, 4.2_

  - [x] 2.4 Create a static method (or helper class) to calculate morning and evening playback windows from SolarTimes, AudioSettings. Morning: start = CivilTwilight + MorningOffsetMinutes, end = start + MorningDurationMinutes. Evening: start = Sunset - EveningPreSunsetMinutes, end = Sunset. Apply overlap prevention: if evening start < morning end, set evening start = morning end. Return null windows when solar times are null.
    - _Requirements: 3.1, 3.2, 4.1, 4.2, 4.4, 3.5, 4.5_

  - [x] 2.5 Write property test for morning window calculation (Property 2)
    - **Property 2: Morning window calculation**
    - Generate random valid (latitude, longitude, date, offset, duration) where civil twilight is determinable. Assert start = civil_twilight + offset and end = start + duration.
    - **Validates: Requirements 3.1, 3.2**

  - [x] 2.6 Write property test for evening window calculation (Property 3)
    - **Property 3: Evening window calculation**
    - Generate random valid (sunset time, EveningPreSunsetMinutes). Assert start = sunset - preSunsetMinutes and end = sunset.
    - **Validates: Requirements 4.1, 4.2**

  - [x] 2.7 Write property test for window overlap prevention (Property 4)
    - **Property 4: Window overlap prevention**
    - Generate random overlapping morning/evening window pairs. Assert adjusted evening start equals morning end, evening end unchanged.
    - **Validates: Requirements 4.4**

  - [x] 2.8 Write unit tests for polar edge cases (null solar times) and SolarCalculatorWrapper
    - Test that extreme latitudes (e.g., 70°N in summer/winter) return null windows
    - _Requirements: 5.6, 3.5, 4.5_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement weather service
  - [x] 4.1 Create `src/SwiftCam/Interfaces/IWeatherService.cs` with `WeatherState CurrentWeather { get; }` property and create `src/SwiftCam/WeatherState.cs` record with PrecipitationMm (double), WindSpeedKph (double), LastUpdated (DateTime?)
    - _Requirements: 6.1, 6.3, 6.4_

  - [x] 4.2 Create `src/SwiftCam/WeatherService.cs` as a BackgroundService implementing IWeatherService. Poll Open-Meteo API at configured interval. Store latest WeatherState. On fetch failure retain previous state and increment consecutive failure counter. After 3+ consecutive failures assume fair weather (PrecipitationMm=0, WindSpeedKph=0). On first start with no data assume fair weather.
    - Open-Meteo URL: `https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=precipitation,wind_speed_10m`
    - Use IHttpClientFactory pattern with injected HttpClient
    - _Requirements: 6.1, 6.2, 6.6, 6.8, 6.9_

  - [x] 4.3 Write property test for weather suppression classification (Property 6)
    - **Property 6: Weather suppression classification**
    - Generate random (precipitation, windSpeed, threshold) triples. Assert suppression == (precipitation > 0 OR windSpeed > threshold).
    - **Validates: Requirements 6.3, 6.4, 6.7**

  - [x] 4.4 Write unit tests for WeatherService: fair weather assumed when no data, fair weather after 3 consecutive failures, correct parsing of Open-Meteo response
    - Use DelegatingHandler mock for HTTP responses
    - _Requirements: 6.6, 6.8, 6.9_

- [x] 5. Implement audio process manager
  - [x] 5.1 Create `src/SwiftCam/Interfaces/IAudioProcessManager.cs` with IsPlaying property, Start(string audioFilePath) method, and StopAsync(CancellationToken) method
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 5.2 Create `src/SwiftCam/AudioProcessManager.cs` implementing IAudioProcessManager. Start spawns `mplayer -loop 0 <file>` with stdout/stderr redirected. StopAsync sends termination signal, waits up to 5 seconds, then force-kills. IsPlaying returns whether process is alive. Handle case where mplayer binary is not found (throw or return indication).
    - _Requirements: 1.1, 1.2, 1.3, 1.7, 1.8_

- [x] 6. Implement AudioService state machine
  - [x] 6.1 Create `src/SwiftCam/AudioState.cs` enum with values: Idle, Playing, Suppressed, Stopped, Error
    - _Requirements: 7.2_

  - [x] 6.2 Create `src/SwiftCam/AudioService.cs` as a BackgroundService. Implement the main scheduling loop: on startup and at midnight recalculate today's playback windows via ISolarCalculator. Every second evaluate current state based on time, weather (from IWeatherService), and process state (from IAudioProcessManager). Implement state machine transitions: Idle→Playing (enter window + fair weather), Playing→Suppressed (weather suppression), Playing→Idle (window ends), Playing→Stopped (mplayer crash, retries remaining), Playing→Error (max retries/file not found/mplayer not found), Suppressed→Playing (weather clears in window), Suppressed→Idle (window ends), Stopped→Playing (restart after 3s delay), Stopped→Error (5 retries exceeded), Error→Idle (next window begins). Track consecutive restart failures, reset at window boundary. Expose current state, reason, and window schedule for the status endpoint.
    - Inject: IOptions<AudioSettings>, ISolarCalculator, IWeatherService, IAudioProcessManager, TimeProvider, ILogger<AudioService>
    - Check audio file existence at each window start
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 2.3, 3.1, 3.2, 3.5, 4.1, 4.2, 4.4, 4.5, 5.3, 5.6, 6.3, 6.4, 6.7_

  - [x] 6.3 Write property test for playback decision correctness (Property 1)
    - **Property 1: Playback decision correctness**
    - Generate random (currentTime, playbackWindows, weatherState, retryCount) tuples. Assert play decision is true iff: time is within a window AND precipitation == 0 AND windSpeed <= threshold AND retries < 5.
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 6.3, 6.4, 6.7**

  - [x] 6.4 Write property test for retry state machine (Property 5)
    - **Property 5: Retry state machine**
    - Generate random crash event sequences (length 1-10) within a single window. Assert restart attempted for crashes 1-5, Error state after 5th failure.
    - **Validates: Requirements 1.5, 1.6**

  - [x] 6.5 Write unit tests for AudioService: mplayer not found enters Error state, audio file not found enters Error state, graceful shutdown terminates mplayer
    - _Requirements: 1.7, 1.8, 2.3_

- [x] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Implement status endpoint and web page update
  - [x] 8.1 Create `src/SwiftCam/AudioStatusResponse.cs` record with State (string), Reason (string), CurrentWindowStart (string?), CurrentWindowEnd (string?), NextWindowStart (string?) properties
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 8.2 Add a minimal API endpoint `MapGet("/api/audio-status", ...)` in Program.cs that reads current state from AudioService and returns AudioStatusResponse as JSON with HTTP 200 and application/json content-type. Include current/next window times in ISO 8601 format. Ensure reason string is capped at 200 characters.
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 8.3 Update the existing HTML page in Program.cs to add an audio status panel `<div>` alongside the camera stream. Add JavaScript that polls `/api/audio-status` every 5 seconds via fetch(), displays state and reason, shows "Status unavailable" on failure, and resumes normal display when connectivity returns. Ensure MJPEG stream remains visible and continues rendering.
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [x] 8.4 Write property test for status response well-formedness (Property 8)
    - **Property 8: Status response well-formedness**
    - Generate random AudioState + PlaybackWindow combinations. Assert state ∈ {"Playing", "Stopped", "Suppressed", "Idle", "Error"}, reason length ≤ 200 chars, window times in valid ISO 8601 format.
    - **Validates: Requirements 7.2, 7.3, 7.4, 7.5**

  - [x] 8.5 Write integration tests for status endpoint: GET /api/audio-status returns 200 + JSON, correct content-type header, valid response structure
    - Use WebApplicationFactory
    - _Requirements: 7.1, 7.6_

- [x] 9. Wire up DI registration and configuration
  - [x] 9.1 Register all audio services in Program.cs: bind AudioSettings from "Audio" config section, register AudioSettingsValidator with IValidateOptions, add ValidateOnStart, register ISolarCalculator/SolarCalculatorWrapper as singleton, register IAudioProcessManager/AudioProcessManager as singleton, register IWeatherService/WeatherService as singleton, register WeatherService as hosted service, register AudioService as hosted service, add HttpClient for WeatherService.
    - _Requirements: 9.7, 1.1, 6.1_

  - [x] 9.2 Add `"Audio"` section to `src/SwiftCam/appsettings.json` with all default values matching the design specification
    - _Requirements: 2.1, 2.2, 3.3, 3.4, 4.3, 5.1, 5.2, 6.2, 6.5_

  - [x] 9.3 Write integration tests for DI wiring: settings bind from config, all services resolve correctly, validation rejects invalid config at startup
    - _Requirements: 9.7_

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- FsCheck.Xunit is already in the test project; no new test dependencies needed.
- The project targets .NET 10; use TimeProvider for testability (already registered in Program.cs).
- Property tests should use `[Property(MaxTest = 100)]` with tag comments: `// Feature: swift-audio-attraction, Property N: <text>`.
- SolarCalculator NuGet package needs to be added as a project dependency.
- Tasks marked with `*` are optional and can be skipped for faster MVP.
- Each task references specific requirements for traceability.
- Checkpoints ensure incremental validation.
- The AudioService follows the same BackgroundService pattern as CameraService for consistency.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "2.3", "4.1", "5.1", "6.1"] },
    { "id": 1, "tasks": ["1.2", "2.2", "2.4", "4.2", "5.2", "8.1"] },
    { "id": 2, "tasks": ["1.3", "1.4", "2.5", "2.6", "2.7", "2.8", "4.3", "4.4"] },
    { "id": 3, "tasks": ["6.2"] },
    { "id": 4, "tasks": ["6.3", "6.4", "6.5", "8.2"] },
    { "id": 5, "tasks": ["8.3", "8.4", "8.5", "9.1", "9.2"] },
    { "id": 6, "tasks": ["9.3"] }
  ]
}
```
