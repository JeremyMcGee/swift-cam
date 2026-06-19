# Requirements Document

## Introduction

Motion-triggered image capture for the SwiftCam birdbox camera. The system detects motion by comparing consecutive video frames using pixel-level differencing and saves a timestamped JPEG capture when significant change is detected. A configurable sensitivity threshold and cooldown period prevent excessive captures while ensuring meaningful activity is recorded.

## Glossary

- **Motion_Detector**: The background service that subscribes to the frame stream, compares consecutive frames, and triggers captures when motion exceeds the configured threshold.
- **Frame_Differencer**: The component responsible for decoding two JPEG frames and computing the percentage of pixels that differ beyond a per-pixel tolerance.
- **Capture_Writer**: The component responsible for writing a JPEG frame to disk with a timestamped filename.
- **Motion_Settings**: The strongly-typed configuration class holding motion detection parameters (threshold, cooldown duration, output path).
- **Cooldown_Period**: The time window after a capture during which no additional captures are triggered, preventing burst saves from continuous motion.
- **Changed_Pixel_Percentage**: The ratio of pixels whose luminance difference exceeds the per-pixel tolerance, expressed as a percentage of total pixels in the frame.

## Requirements

### Requirement 1: Frame Differencing

**User Story:** As the birdbox owner, I want the system to compare consecutive frames to detect motion, so that the camera can identify when a bird enters the box.

#### Acceptance Criteria

1. WHEN two consecutive JPEG frames are received, THE Frame_Differencer SHALL decode both frames and compute the Changed_Pixel_Percentage by comparing pixel luminance values.
2. WHEN the Changed_Pixel_Percentage exceeds the configured threshold, THE Motion_Detector SHALL classify the frame pair as containing motion.
3. WHEN the Changed_Pixel_Percentage is at or below the configured threshold, THE Motion_Detector SHALL classify the frame pair as containing no motion.

### Requirement 2: Configurable Sensitivity

**User Story:** As the birdbox owner, I want to configure the motion detection sensitivity, so that I can tune it to avoid false positives from lighting changes or camera noise.

#### Acceptance Criteria

1. THE Motion_Settings SHALL expose a Threshold property representing the Changed_Pixel_Percentage above which motion is detected, with a default value of 5.0 (percent).
2. THE Motion_Settings SHALL expose a CooldownSeconds property representing the duration in seconds of the Cooldown_Period, with a default value of 300 (5 minutes).
3. THE Motion_Settings SHALL expose a CaptureDirectory property representing the output path for captured images, with a default value of "captures".
4. WHEN the application starts, THE Motion_Detector SHALL read motion detection settings from the "Motion" configuration section using the standard IOptions pattern.

### Requirement 3: Motion-Triggered Capture

**User Story:** As the birdbox owner, I want the system to save a photo when motion is detected, so that I have a record of bird activity.

#### Acceptance Criteria

1. WHEN motion is detected, THE Capture_Writer SHALL save the current frame as a JPEG file to the configured CaptureDirectory.
2. WHEN saving a capture, THE Capture_Writer SHALL name the file using the format `yyyy-MMM-dd_HH-mm-ss.jpg` based on the current local time.
3. WHEN the CaptureDirectory does not exist, THE Capture_Writer SHALL create the directory before writing the file.

### Requirement 4: Cooldown Behaviour

**User Story:** As the birdbox owner, I want a cooldown period after each capture, so that continuous motion does not fill my disk with redundant images.

#### Acceptance Criteria

1. WHEN a capture is saved, THE Motion_Detector SHALL enter the Cooldown_Period for the configured CooldownSeconds duration.
2. WHILE the Motion_Detector is in Cooldown_Period, THE Motion_Detector SHALL skip motion detection and not trigger additional captures.
3. WHEN the Cooldown_Period expires, THE Motion_Detector SHALL resume normal frame comparison and motion detection.

### Requirement 5: Frame Stream Integration

**User Story:** As the birdbox owner, I want motion detection to run independently of the live video stream, so that viewers can watch the stream without affecting detection.

#### Acceptance Criteria

1. WHEN the application starts, THE Motion_Detector SHALL subscribe to the IFrameBroadcaster to receive JPEG frames independently of streaming clients.
2. WHEN the application shuts down, THE Motion_Detector SHALL dispose its frame subscription and stop processing gracefully.
3. THE Motion_Detector SHALL operate as a BackgroundService so that its lifecycle is managed by the .NET host.

### Requirement 6: Logging

**User Story:** As the birdbox owner, I want the system to log motion events and captures, so that I can review activity without checking the captures folder.

#### Acceptance Criteria

1. WHEN motion is detected, THE Motion_Detector SHALL log an informational message indicating motion was detected and the computed Changed_Pixel_Percentage.
2. WHEN a capture is saved, THE Motion_Detector SHALL log an informational message including the full filename of the saved image.
3. WHEN the Motion_Detector enters Cooldown_Period, THE Motion_Detector SHALL log a debug message indicating cooldown has started and its duration.
