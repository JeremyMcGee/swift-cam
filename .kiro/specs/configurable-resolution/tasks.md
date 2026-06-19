# Implementation Plan: Configurable Resolution

## Overview

Replace the hardcoded `ProcessArguments` constant in `CameraService` with a strongly-typed `CameraSettings` options class. Add validation via `IValidateOptions<CameraSettings>`, wire up configuration binding in `Program.cs`, and log active settings at startup. The implementation language is C# targeting the existing .NET project.

## Tasks

- [x] 1. Create CameraSettings options class
  - [x] 1.1 Create `src/SwiftCam/CameraSettings.cs` with properties Width, Height, Framerate, Quality and defaults (640, 480, 15, 80)
    - Plain POCO class with `int` properties and default values via property initializers
    - _Requirements: 1.1, 1.2_

- [x] 2. Create CameraSettingsValidator
  - [x] 2.1 Create `src/SwiftCam/CameraSettingsValidator.cs` implementing `IValidateOptions<CameraSettings>`
    - Validate Width in [160, 4056], Height in [120, 3040], Framerate in [1, 120], Quality in [1, 100]
    - Return descriptive error messages specifying the allowed range and the actual value
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [ ]* 2.2 Write property test: Validator accepts all in-range settings
    - **Property 2: Validator accepts all in-range settings**
    - Generate random CameraSettings with all values within valid ranges, assert validation returns Success
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4**

  - [ ]* 2.3 Write property test: Validator rejects all out-of-range settings
    - **Property 3: Validator rejects all out-of-range settings**
    - Generate random CameraSettings with at least one value outside valid range, assert validation returns Fail with error messages
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4**

- [x] 3. Modify CameraService to use CameraSettings
  - [x] 3.1 Add `IOptions<CameraSettings>` parameter to CameraService constructor
    - Remove the `ProcessArguments` constant
    - Store `options.Value` in a `_settings` field
    - _Requirements: 4.1_

  - [x] 3.2 Add `BuildProcessArguments()` method to CameraService
    - Build string: `-t 0 --codec mjpeg --width {Width} --height {Height} --framerate {Framerate} -q {Quality} -n -o -`
    - Replace the hardcoded constant usage in `StartCameraProcess()` with a call to `BuildProcessArguments()`
    - _Requirements: 4.1, 4.2_

  - [x] 3.3 Add startup logging of active camera settings
    - Log Width, Height, Framerate, and Quality at Information level when `ExecuteAsync` begins
    - _Requirements: 5.1_

  - [ ]* 3.4 Write property test: Argument string round-trip
    - **Property 1: Argument string round-trip**
    - Generate random valid CameraSettings, call BuildProcessArguments(), parse the integer values back out using regex, assert they equal the input values
    - **Validates: Requirements 4.1, 4.2**

- [x] 4. Wire up configuration in Program.cs
  - [x] 4.1 Register CameraSettings options binding and validation in Program.Main
    - Add `builder.Services.Configure<CameraSettings>(builder.Configuration.GetSection("Camera"))`
    - Add `builder.Services.AddSingleton<IValidateOptions<CameraSettings>, CameraSettingsValidator>()`
    - Add `builder.Services.AddOptionsWithValidateOnStart<CameraSettings>()` for eager validation
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.5_

  - [x] 4.2 Add a sample "Camera" section to appsettings.json (or create appsettings.json if it doesn't exist)
    - Include commented example values showing all four properties
    - _Requirements: 2.1_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ]* 6. Write unit tests for configuration binding and defaults
  - [ ]* 6.1 Write unit test verifying default CameraSettings produces original argument string
    - Assert BuildProcessArguments() with default settings equals `-t 0 --codec mjpeg --width 640 --height 480 --framerate 15 -q 80 -n -o -`
    - _Requirements: 1.2, 1.3, 4.2_

  - [ ]* 6.2 Write unit test verifying configuration binding from in-memory provider
    - Use `ConfigurationBuilder` with in-memory collection to bind a "Camera" section to CameraSettings
    - _Requirements: 2.1_

  - [ ]* 6.3 Write unit test verifying validation failure terminates cleanly
    - Configure invalid settings, verify that OptionsValidationException is thrown on value access
    - _Requirements: 3.5_

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Task Dependency Graph

```json
{
  "waves": [
    { "tasks": ["1"] },
    { "tasks": ["2"] },
    { "tasks": ["3"] },
    { "tasks": ["4"] },
    { "tasks": ["5"] },
    { "tasks": ["6"] },
    { "tasks": ["7"] }
  ]
}
```

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- The project uses FsCheck with xUnit for property-based tests
- Property tests should have a minimum of 100 iterations
- Requirement 5.2 (logging the configuration source) is deferred as it requires deeper integration with .NET's configuration provenance tracking, which is not natively exposed by `IOptions<T>`
