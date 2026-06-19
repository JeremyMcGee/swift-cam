# Requirements Document

## Introduction

The Swift Audio Attraction feature adds automated audio playback to the SwiftCam birdbox camera system to attract swifts. The system plays a looped audio recording during two daily time windows (morning and evening) calculated from civil twilight and sunset times for the configured location. Playback is suppressed during rain or high wind conditions, as swifts do not fly in those conditions. A status panel on the existing web page displays the current audio state and reason.

## Glossary

- **Audio_Service**: The BackgroundService responsible for managing audio playback scheduling, weather checks, and mplayer process control.
- **Playback_Window**: A time period during which audio playback is permitted, defined by a start time and end time calculated from solar events.
- **Civil_Twilight**: The moment when the sun is 6 degrees below the horizon before sunrise, marking the start of useful daylight.
- **Weather_Service**: The component responsible for fetching current weather conditions from the Open-Meteo API.
- **Status_Endpoint**: The HTTP endpoint that returns the current audio playback state and reason as JSON.
- **Web_Page**: The existing HTML page served at the root path (/) that displays the camera stream and status information.
- **Audio_Settings**: The strongly-typed configuration class holding all configurable parameters for audio attraction.
- **Suppression_Condition**: A weather condition (rain or high wind) that causes audio playback to be paused.

## Requirements

### Requirement 1: Audio Playback via mplayer

**User Story:** As a swift conservation volunteer, I want the system to play an audio recording on loop via the attached speaker, so that swifts are attracted to the birdbox.

#### Acceptance Criteria

1. WHEN a Playback_Window is active and no Suppression_Condition exists, THE Audio_Service SHALL start an mplayer process with loop mode enabled playing the configured audio file.
2. WHEN the Playback_Window ends, THE Audio_Service SHALL send a termination signal to the mplayer process and, if the process has not exited within 5 seconds, force-kill it.
3. WHEN a Suppression_Condition is detected during an active Playback_Window, THE Audio_Service SHALL send a termination signal to the mplayer process and, if the process has not exited within 5 seconds, force-kill it.
4. WHEN a Suppression_Condition clears during an active Playback_Window, THE Audio_Service SHALL restart the mplayer process.
5. IF the mplayer process terminates unexpectedly during an active Playback_Window, THEN THE Audio_Service SHALL wait 3 seconds and restart the mplayer process, up to a maximum of 5 consecutive restart attempts within a single Playback_Window.
6. IF the mplayer process has failed 5 consecutive restart attempts within a single Playback_Window, THEN THE Audio_Service SHALL log an error and remain idle until the next Playback_Window begins.
7. WHEN the application shuts down, THE Audio_Service SHALL send a termination signal to the mplayer process and, if the process has not exited within 5 seconds, force-kill it.
8. IF the mplayer binary is not found on the system, THEN THE Audio_Service SHALL log an error and remain idle.

### Requirement 2: Configurable Audio File Path

**User Story:** As a system administrator, I want to configure which audio file is played, so that I can update the recording without modifying code.

#### Acceptance Criteria

1. THE Audio_Settings SHALL include an AudioFilePath property specifying the path to the audio file.
2. THE Audio_Settings SHALL default AudioFilePath to "audio/swift-call.mp3".
3. IF the configured AudioFilePath does not exist on disk, THEN THE Audio_Service SHALL log an error at startup and remain idle until a valid file is present.

### Requirement 3: Morning Playback Window

**User Story:** As a swift conservation volunteer, I want audio to play during the morning period when swifts are most active, so that the sound attracts them at the optimal time.

#### Acceptance Criteria

1. THE Audio_Service SHALL calculate the morning Playback_Window start time as Civil_Twilight plus MorningOffsetMinutes for the configured location.
2. THE Audio_Service SHALL calculate the morning Playback_Window end time as the morning Playback_Window start time plus MorningDurationMinutes.
3. THE Audio_Settings SHALL include a MorningDurationMinutes property with a default value of 210 (3.5 hours) and a maximum value of 720.
4. THE Audio_Settings SHALL include a MorningOffsetMinutes property (offset from Civil_Twilight, where positive values delay the start) with a default value of 0 and a permitted range of -60 to 240.
5. IF Civil_Twilight cannot be determined for the configured location and date (e.g., polar latitudes with no twilight event), THEN THE Audio_Service SHALL skip the morning Playback_Window for that day and log a warning.

### Requirement 4: Evening Playback Window

**User Story:** As a swift conservation volunteer, I want audio to play during the evening period before sunset, so that returning swifts are attracted to the birdbox.

#### Acceptance Criteria

1. THE Audio_Service SHALL calculate the evening Playback_Window start time as sunset minus the configured EveningPreSunsetMinutes for the configured location.
2. THE Audio_Service SHALL calculate the evening Playback_Window end time as sunset for the configured location.
3. THE Audio_Settings SHALL include an EveningPreSunsetMinutes property with a default value of 150 (2.5 hours) and a maximum configurable value of 480 (8 hours).
4. IF the calculated evening Playback_Window start time is earlier than the morning Playback_Window end time, THEN THE Audio_Service SHALL adjust the evening Playback_Window start time to equal the morning Playback_Window end time.
5. IF no sunset time can be determined for the configured location on a given day, THEN THE Audio_Service SHALL skip the evening Playback_Window for that day and log a warning.

### Requirement 5: Location-Based Solar Time Calculation

**User Story:** As a system administrator, I want to configure the location used for sunrise and sunset calculations, so that playback windows are accurate for the birdbox site.

#### Acceptance Criteria

1. THE Audio_Settings SHALL include Latitude and Longitude properties for the configured location.
2. THE Audio_Settings SHALL default Latitude to 51.9 and Longitude to -2.07 (Cheltenham, UK).
3. WHEN the Audio_Service starts and at midnight (00:00) local time each day, THE Audio_Service SHALL recalculate Playback_Window times for that day based on the configured Latitude and Longitude.
4. IF the configured Latitude is outside the range -90 to 90, THEN THE settings validator SHALL report a validation failure.
5. IF the configured Longitude is outside the range -180 to 180, THEN THE settings validator SHALL report a validation failure.
6. IF the solar calculation yields no sunrise or sunset for the configured location and date (e.g., polar day or polar night), THEN THE Audio_Service SHALL skip playback for that day and log a warning.

### Requirement 6: Weather-Based Playback Suppression

**User Story:** As a swift conservation volunteer, I want playback to stop during rain or high wind, so that audio is not wasted when swifts cannot fly.

#### Acceptance Criteria

1. THE Weather_Service SHALL fetch current weather conditions from the Open-Meteo API at the interval specified by WeatherPollIntervalMinutes.
2. THE Audio_Settings SHALL include a WeatherPollIntervalMinutes property with a default value of 15 and a valid range of 1 to 60 inclusive.
3. WHEN the weather data indicates rain (precipitation greater than 0 mm), THE Audio_Service SHALL stop any currently playing track and prevent new tracks from starting until the condition clears.
4. WHEN the weather data indicates wind speed exceeding the configured WindSpeedThresholdKph, THE Audio_Service SHALL stop any currently playing track and prevent new tracks from starting until the condition clears.
5. THE Audio_Settings SHALL include a WindSpeedThresholdKph property with a default value of 40 and a valid range of 1 to 120 inclusive.
6. IF the Weather_Service fails to fetch weather data, THEN THE Audio_Service SHALL continue with the last known weather state and log a warning.
7. WHEN a subsequent weather fetch indicates precipitation equals 0 mm and wind speed is at or below the configured WindSpeedThresholdKph, THE Audio_Service SHALL allow playback to resume according to the normal schedule.
8. IF no successful weather fetch has occurred since application startup, THEN THE Audio_Service SHALL allow playback (assume fair weather) until the first successful fetch provides actual conditions.
9. IF the Weather_Service fails to fetch weather data for 3 or more consecutive attempts, THEN THE Audio_Service SHALL assume fair weather conditions and log a warning indicating prolonged weather data unavailability.

### Requirement 7: Status Endpoint

**User Story:** As a user viewing the web interface, I want to see the current audio playback status, so that I know whether the system is working correctly.

#### Acceptance Criteria

1. THE application SHALL expose a GET /api/audio-status endpoint returning a JSON response with HTTP status code 200 and a Content-Type header of application/json.
2. THE Status_Endpoint SHALL return the current state as one of: "Playing", "Stopped", "Suppressed", "Idle", or "Error".
3. THE Status_Endpoint SHALL return a reason string describing the current state (e.g., "Morning session", "Outside playback window", "Paused: rain detected", "Paused: high wind", "Audio file not found") with a maximum length of 200 characters.
4. IF a Playback_Window is currently active or is the next scheduled window within the same day, THEN THE Status_Endpoint SHALL return that Playback_Window's start and end times in ISO 8601 format (yyyy-MM-ddTHH:mm:ssZ).
5. THE Status_Endpoint SHALL return the next scheduled Playback_Window start time in ISO 8601 format, or null if no future window is scheduled.
6. THE Status_Endpoint SHALL respond within 2 seconds of receiving the request.

### Requirement 8: Web Page Status Display

**User Story:** As a user viewing the web interface, I want to see the audio status on the same page as the camera stream, so that I have a complete view of the system state.

#### Acceptance Criteria

1. THE Web_Page SHALL include a status panel displaying the current audio playback state and reason as returned by the Status_Endpoint.
2. THE Web_Page SHALL poll the Status_Endpoint every 5 seconds to update the displayed status.
3. THE Web_Page SHALL display the status panel alongside the camera stream such that the MJPEG stream element remains visible and continues rendering frames.
4. WHEN the Status_Endpoint is unreachable (request fails or no response within 5 seconds), THE Web_Page SHALL display "Status unavailable" in the status panel.
5. WHEN the Status_Endpoint becomes reachable after a previous failure, THE Web_Page SHALL resume displaying the current audio playback state and reason.

### Requirement 9: Configuration Validation

**User Story:** As a system administrator, I want the system to validate audio configuration at startup, so that misconfigurations are caught early.

#### Acceptance Criteria

1. IF MorningDurationMinutes is less than 1 or greater than 1440, THEN THE settings validator SHALL report a validation failure indicating the provided value and the acceptable range.
2. IF EveningPreSunsetMinutes is less than 1 or greater than 1440, THEN THE settings validator SHALL report a validation failure indicating the provided value and the acceptable range.
3. IF WeatherPollIntervalMinutes is less than 1 or greater than 60, THEN THE settings validator SHALL report a validation failure indicating the provided value and the acceptable range.
4. IF WindSpeedThresholdKph is less than 1 or greater than 120, THEN THE settings validator SHALL report a validation failure indicating the provided value and the acceptable range.
5. IF AudioFilePath is empty or whitespace, THEN THE settings validator SHALL report a validation failure indicating that the audio file path must not be empty.
6. IF Latitude is outside the range -90 to 90 or Longitude is outside the range -180 to 180, THEN THE settings validator SHALL report a validation failure indicating the provided value and the acceptable range.
7. IF one or more validation failures are reported, THEN THE application SHALL prevent the Audio_Service from starting and log all validation failure messages at error level.
