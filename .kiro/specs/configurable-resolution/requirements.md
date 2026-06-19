# Requirements Document

## Introduction

This document defines the requirements for making the SwiftCam camera parameters configurable at runtime. Currently, the resolution (640x480), framerate (15 fps), and JPEG quality (80) are hardcoded in the camera process arguments. This feature allows users to specify these values via standard .NET configuration sources (appsettings.json, environment variables, or command-line arguments) without modifying source code, while preserving backward-compatible defaults.

## Glossary

- **Camera_Settings**: A configuration object containing the camera capture parameters (width, height, framerate, and JPEG quality)
- **Configuration_Provider**: The .NET configuration system that aggregates values from appsettings.json, environment variables, and command-line arguments
- **Camera_Service**: The background service that manages the libcamera-vid child process and reads JPEG frames from its stdout
- **Settings_Validator**: The component responsible for ensuring configured camera parameter values fall within acceptable ranges

## Requirements

### Requirement 1: Configuration Model

**User Story:** As a developer, I want camera parameters defined in a strongly-typed options class, so that the application can bind configuration values safely and provide IntelliSense support.

#### Acceptance Criteria

1. THE Camera_Settings SHALL expose integer properties for Width, Height, Framerate, and Quality
2. THE Camera_Settings SHALL define default values of 640 for Width, 480 for Height, 15 for Framerate, and 80 for Quality
3. WHEN no configuration values are provided by any Configuration_Provider, THE Camera_Service SHALL use the default Camera_Settings values to preserve backward compatibility

### Requirement 2: Configuration Sources

**User Story:** As a user, I want to configure the camera resolution via appsettings.json, environment variables, or command-line arguments, so that I can change camera parameters without recompiling the application.

#### Acceptance Criteria

1. WHEN an appsettings.json file contains a "Camera" section with Width, Height, Framerate, or Quality keys, THE Configuration_Provider SHALL bind those values to the Camera_Settings
2. WHEN environment variables prefixed with "CAMERA__" (e.g., CAMERA__WIDTH, CAMERA__HEIGHT) are set, THE Configuration_Provider SHALL bind those values to the Camera_Settings
3. WHEN command-line arguments in the format --Camera:Width, --Camera:Height, --Camera:Framerate, or --Camera:Quality are provided, THE Configuration_Provider SHALL bind those values to the Camera_Settings
4. WHEN multiple Configuration_Providers supply the same setting, THE Configuration_Provider SHALL apply the standard .NET precedence order: command-line arguments override environment variables, which override appsettings.json values

### Requirement 3: Settings Validation

**User Story:** As a user, I want the application to validate my camera settings at startup, so that I receive a clear error message instead of a cryptic camera failure if I provide invalid values.

#### Acceptance Criteria

1. WHEN the configured Width is less than 160 or greater than 4056, THEN THE Settings_Validator SHALL reject the configuration and report an error specifying the allowed range
2. WHEN the configured Height is less than 120 or greater than 3040, THEN THE Settings_Validator SHALL reject the configuration and report an error specifying the allowed range
3. WHEN the configured Framerate is less than 1 or greater than 120, THEN THE Settings_Validator SHALL reject the configuration and report an error specifying the allowed range
4. WHEN the configured Quality is less than 1 or greater than 100, THEN THE Settings_Validator SHALL reject the configuration and report an error specifying the allowed range
5. IF any Camera_Settings value fails validation, THEN THE Camera_Service SHALL log the validation error and terminate the application with a non-zero exit code before starting the camera process

### Requirement 4: Process Arguments Construction

**User Story:** As a developer, I want the camera process arguments built dynamically from configuration, so that the libcamera-vid process receives the user-specified parameters.

#### Acceptance Criteria

1. WHEN the Camera_Service starts the libcamera-vid process, THE Camera_Service SHALL construct the process arguments string using the validated Camera_Settings values for width, height, framerate, and quality
2. THE Camera_Service SHALL format the process arguments as: "-t 0 --codec mjpeg --width {Width} --height {Height} --framerate {Framerate} -q {Quality} -n -o -"
3. WHEN Camera_Settings values change between application restarts, THE Camera_Service SHALL use the updated values in the process arguments

### Requirement 5: Startup Logging of Active Configuration

**User Story:** As a user, I want the application to log the active camera settings at startup, so that I can confirm which resolution and parameters are in effect.

#### Acceptance Criteria

1. WHEN the Camera_Service starts successfully, THE Camera_Service SHALL log the active Width, Height, Framerate, and Quality values to the console
2. THE Camera_Service SHALL log the configuration source that provided each non-default value (e.g., "appsettings.json", "environment variable", or "command-line argument")
