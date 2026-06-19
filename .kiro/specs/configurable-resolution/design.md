# Design Document: Configurable Resolution

## Overview

This feature replaces the hardcoded `ProcessArguments` constant in `CameraService` with a strongly-typed `CameraSettings` options class bound through .NET's `IOptions<T>` pattern. Settings are validated at startup using `IValidateOptions<CameraSettings>`, and the process argument string is built dynamically from the validated configuration. The design fits cleanly into the existing DI and `BackgroundService` infrastructure with minimal changes to the codebase.

## Architecture

```mermaid
graph TD
    subgraph "Configuration Sources"
        A[appsettings.json]
        B[Environment Variables]
        C[Command-line Arguments]
    end

    subgraph "Startup Pipeline"
        A --> D[IConfiguration]
        B --> D
        C --> D
        D -->|Bind "Camera" section| E[IOptions&lt;CameraSettings&gt;]
        E -->|Validate on first access| F[CameraSettingsValidator]
        F -->|Pass| G[CameraService starts]
        F -->|Fail| H[Log error + Exit 1]
    end

    subgraph "Runtime"
        G -->|Build arguments from settings| I[libcamera-vid process]
    end
```

## Components and Interfaces

### Component 1: CameraSettings

**Purpose**: Strongly-typed POCO representing the camera configuration values with sensible defaults.

```csharp
public class CameraSettings
{
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 480;
    public int Framerate { get; set; } = 15;
    public int Quality { get; set; } = 80;
}
```

**Responsibilities**:
- Hold the four configurable camera parameters
- Provide backward-compatible defaults matching the current hardcoded values

### Component 2: CameraSettingsValidator

**Purpose**: Validates all camera settings at startup using the `IValidateOptions<T>` pattern.

```csharp
public class CameraSettingsValidator : IValidateOptions<CameraSettings>
{
    public ValidateOptionsResult Validate(string? name, CameraSettings options)
    {
        var failures = new List<string>();

        if (options.Width < 160 || options.Width > 4056)
            failures.Add($"Width must be between 160 and 4056, got {options.Width}.");

        if (options.Height < 120 || options.Height > 3040)
            failures.Add($"Height must be between 120 and 3040, got {options.Height}.");

        if (options.Framerate < 1 || options.Framerate > 120)
            failures.Add($"Framerate must be between 1 and 120, got {options.Framerate}.");

        if (options.Quality < 1 || options.Quality > 100)
            failures.Add($"Quality must be between 1 and 100, got {options.Quality}.");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
```

**Responsibilities**:
- Enforce valid ranges for each parameter
- Return descriptive error messages specifying the allowed range and the invalid value

### Component 3: CameraService (modified)

**Purpose**: Accept `IOptions<CameraSettings>` via DI, build the process arguments dynamically, and log active configuration at startup.

```csharp
public class CameraService : BackgroundService, ICameraService
{
    private readonly CameraSettings _settings;
    // ... existing fields ...

    public CameraService(
        IOptions<CameraSettings> options,
        IFrameBroadcaster broadcaster,
        IHostApplicationLifetime appLifetime,
        ILogger<CameraService> logger)
    {
        _settings = options.Value; // triggers validation
        // ... existing assignments ...
    }

    private string BuildProcessArguments()
    {
        return $"-t 0 --codec mjpeg --width {_settings.Width} --height {_settings.Height} " +
               $"--framerate {_settings.Framerate} -q {_settings.Quality} -n -o -";
    }
}
```

**Responsibilities**:
- Resolve validated settings from DI (validation fires on `options.Value`)
- Build the process arguments string from settings values
- Log active settings at startup

### Component 4: Program.cs Registration

**Purpose**: Wire up the options binding, validation, and eager validation at startup.

```csharp
// In Program.Main, after builder creation:
builder.Services.Configure<CameraSettings>(
    builder.Configuration.GetSection("Camera"));

builder.Services.AddSingleton<IValidateOptions<CameraSettings>, CameraSettingsValidator>();

// Trigger eager validation so failures surface before the camera process starts
builder.Services.AddOptionsWithValidateOnStart<CameraSettings>();
```

## Data Models

### CameraSettings

```csharp
public class CameraSettings
{
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 480;
    public int Framerate { get; set; } = 15;
    public int Quality { get; set; } = 80;
}
```

**Validation Rules**:
- Width: 160 ≤ value ≤ 4056
- Height: 120 ≤ value ≤ 3040
- Framerate: 1 ≤ value ≤ 120
- Quality: 1 ≤ value ≤ 100

### Configuration Binding (appsettings.json)

```json
{
  "Camera": {
    "Width": 1920,
    "Height": 1080,
    "Framerate": 30,
    "Quality": 85
  }
}
```

### Environment Variable Mapping

| Setting   | Environment Variable    |
|-----------|------------------------|
| Width     | Camera__Width           |
| Height    | Camera__Height          |
| Framerate | Camera__Framerate       |
| Quality   | Camera__Quality         |

### Command-line Argument Mapping

| Setting   | Argument              |
|-----------|-----------------------|
| Width     | --Camera:Width=1920   |
| Height    | --Camera:Height=1080  |
| Framerate | --Camera:Framerate=30 |
| Quality   | --Camera:Quality=85   |

## Key Functions with Formal Specifications

### Function: BuildProcessArguments()

```csharp
private string BuildProcessArguments()
```

**Preconditions:**
- `_settings` is non-null and has been validated (all values within allowed ranges)

**Postconditions:**
- Returns a string in the exact format: `-t 0 --codec mjpeg --width {W} --height {H} --framerate {F} -q {Q} -n -o -`
- The embedded values W, H, F, Q are the integer string representations of the settings
- No side effects

### Function: CameraSettingsValidator.Validate()

```csharp
public ValidateOptionsResult Validate(string? name, CameraSettings options)
```

**Preconditions:**
- `options` is non-null

**Postconditions:**
- Returns `ValidateOptionsResult.Success` if and only if all four properties are within their valid ranges
- Returns `ValidateOptionsResult.Fail(failures)` where `failures` contains one message per invalid property
- No mutations to the input

## Error Handling

### Validation Failure at Startup

**Condition**: One or more Camera_Settings values are outside allowed ranges
**Response**: `OptionsValidationException` thrown when `IOptions<CameraSettings>.Value` is first accessed. The application host logs the validation errors and terminates with a non-zero exit code.
**Recovery**: User corrects the configuration source and restarts the application.

### Non-integer Configuration Value

**Condition**: A configuration source provides a non-integer string for a camera property (e.g., `"Width": "abc"`)
**Response**: The .NET configuration binder will throw a `FormatException` or leave the default value. With `ValidateOnStart`, the binding error surfaces before the camera process starts.
**Recovery**: User corrects the malformed value.

## Testing Strategy

### Unit Testing Approach

- Test `CameraSettingsValidator.Validate()` with boundary values and mid-range values
- Test `BuildProcessArguments()` produces correct format with various settings
- Test that default `CameraSettings` values match the original hardcoded values (640, 480, 15, 80)
- Test that validation rejects out-of-range values with descriptive messages

### Property-Based Testing Approach

**Property Test Library**: FsCheck (via `FsCheck.Xunit` NuGet package), already established in the project's testing conventions.

Property-based tests will validate the argument builder and validator using randomly generated settings values.

### Integration Testing Approach

- Test that `Configure<CameraSettings>` correctly binds from an in-memory configuration provider
- Test that `ValidateOnStart` surfaces validation errors before the hosted service runs

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Argument string round-trip

*For any* valid CameraSettings (Width in [160, 4056], Height in [120, 3040], Framerate in [1, 120], Quality in [1, 100]), parsing the width, height, framerate, and quality integer values back out of the string produced by `BuildProcessArguments()` shall yield the original settings values.

**Validates: Requirements 4.1, 4.2**

### Property 2: Validator accepts all in-range settings

*For any* CameraSettings where Width is in [160, 4056], Height is in [120, 3040], Framerate is in [1, 120], and Quality is in [1, 100], the validator shall return `ValidateOptionsResult.Success`.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

### Property 3: Validator rejects all out-of-range settings

*For any* CameraSettings where at least one property is outside its valid range, the validator shall return a failed `ValidateOptionsResult` containing at least one error message that specifies the allowed range.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**
