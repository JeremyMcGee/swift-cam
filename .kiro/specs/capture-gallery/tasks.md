# Implementation Plan: Capture Gallery

## Overview

Add a browsable gallery of motion-captured images to the SwiftCam web page. This involves two backend API endpoints (`/api/captures` and `/api/captures/{filename}`) for listing and serving captures, plus a frontend gallery panel with thumbnails displayed in a scrollable grid below the audio status panel.

## Tasks

- [x] 1. Implement capture listing and file serving logic
  - [x] 1.1 Create `src/SwiftCam/CaptureListService.cs` as a static class with `GetCaptureFilenames(string captureDirectory)` that returns `.jpg` filenames sorted descending (most recent first), returns empty array if directory doesn't exist or has no `.jpg` files, catches `DirectoryNotFoundException` and `IOException`
    - Filter to `.jpg` extension case-insensitively
    - Sort filenames in descending alphabetical order
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 1.2 Create `src/SwiftCam/CaptureFileService.cs` as a static class with `IsValidFilename(string filename)` and `ResolveCaptureFile(string filename, string captureDirectory)` methods
    - `IsValidFilename`: reject empty/whitespace, reject filenames containing `..`, `/`, or `\`, reject non-`.jpg` extension, case-insensitive extension check
    - `ResolveCaptureFile`: validate via `IsValidFilename`, combine with directory, return full path if file exists, return null if not found, throw `ArgumentException` for invalid filenames
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x]* 1.3 Write property test for capture listing sort order
    - **Property 1: Capture listing is sorted most-recent-first**
    - Generate random sets of valid capture filenames, write to temp directory, call `GetCaptureFilenames`, verify result is sorted descending
    - **Validates: Requirements 1.1**

  - [x]* 1.4 Write property test for .jpg-only filtering
    - **Property 2: Capture listing includes only .jpg files**
    - Generate directories with mixed file extensions (`.jpg`, `.png`, `.txt`, `.jpeg`, `.JPG`), call `GetCaptureFilenames`, verify only `.jpg` files returned
    - **Validates: Requirements 1.4**

  - [x]* 1.5 Write property test for filename validation
    - **Property 3: Invalid filenames are always rejected**
    - Generate filenames with path traversal chars and/or non-`.jpg` extensions, verify `IsValidFilename` returns false. Generate valid filenames and verify returns true
    - **Validates: Requirements 2.3, 2.4**

  - [x]* 1.6 Write property test for filename timestamp round-trip
    - **Property 4: Filename timestamp parsing round-trip**
    - Generate random `DateTime` values, produce filename via `CaptureWriter.GenerateFilename`, parse timestamp back, verify year/month/day/hour/minute/second match
    - **Validates: Requirements 4.4**

- [x] 2. Register API endpoints
  - [x] 2.1 Add `GET /api/captures` route in `Program.cs` `MapRoutes` method that calls `CaptureListService.GetCaptureFilenames` with `MotionSettings.CaptureDirectory` and returns JSON array with status 200
    - Inject `IOptions<MotionSettings>` to get the capture directory path
    - Return `Results.Json(filenames)` with status 200
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 2.2 Add `GET /api/captures/{filename}` route in `Program.cs` `MapRoutes` method that validates filename via `CaptureFileService.IsValidFilename`, resolves file via `CaptureFileService.ResolveCaptureFile`, returns file with `Content-Type: image/jpeg` or appropriate error status
    - Return 400 if `IsValidFilename` returns false
    - Return 404 if `ResolveCaptureFile` returns null
    - Return `Results.File(path, "image/jpeg")` on success
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x]* 2.3 Write unit tests for API endpoints
    - Test `GET /api/captures` returns 200 with empty array when no captures
    - Test `GET /api/captures/{filename}` returns 404 for missing file
    - Test `GET /api/captures/{filename}` returns 400 for path traversal (`../etc/passwd`)
    - Test `GET /api/captures/{filename}` returns 400 for wrong extension (`test.png`)
    - Test `GET /api/captures/{filename}` returns 200 with correct content-type for valid file
    - _Requirements: 1.5, 2.1, 2.2, 2.3, 2.4_

- [x] 3. Checkpoint - Ensure backend compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement gallery frontend UI
  - [x] 4.1 Add Gallery Panel HTML/CSS to the page served from `Program.cs`, below the audio status panel
    - Add `<div id="capture-gallery">` with heading "Captures", a refresh button, and a scrollable grid container
    - Set max-height with vertical overflow scroll, max-width matching the stream (1280px)
    - Style thumbnails in a CSS grid layout
    - Display "No captures yet" message when gallery is empty
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 4.2 Add Gallery Panel JavaScript as an IIFE that fetches `/api/captures`, renders thumbnails, handles refresh, and displays timestamps
    - On page load, fetch capture list and render thumbnails
    - Each thumbnail links to `/api/captures/{filename}` opening in a new tab
    - Parse filename format `yyyy-MMM-dd_HH-mm-ss.jpg` to display human-readable timestamp below each thumbnail
    - Refresh button fetches latest list and re-renders grid
    - Show loading state on refresh button while fetching
    - Display "Failed to load captures" on fetch error without affecting stream or audio status
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3_

- [x] 5. Checkpoint - Ensure full build succeeds and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Integration tests
  - [x]* 6.1 Write integration tests using WebApplicationFactory
    - Test `GET /api/captures` returns `Content-Type: application/json` and status 200
    - Test `GET /api/captures/{filename}` returns `Content-Type: image/jpeg` for a valid file
    - Test gallery endpoints don't interfere with `/stream` or `/api/audio-status`
    - Test application starts correctly with new routes registered
    - _Requirements: 1.5, 2.1, 6.1, 6.2_

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- FsCheck.Xunit is already in the test project; use `[Property(MaxTest = 100)]` for property tests
- Reuse `MotionSettings.CaptureDirectory` — no new configuration needed
- `CaptureWriter.GenerateFilename` already exists for the filename format; reuse in property test 4
- Frontend follows the existing inline HTML/CSS/JS pattern in `Program.cs`
- Tasks marked with `*` are optional and can be skipped for faster MVP
- Temp directories for filesystem tests should be cleaned up in `Dispose`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4", "1.5", "1.6", "2.1", "2.2"] },
    { "id": 2, "tasks": ["2.3", "4.1"] },
    { "id": 3, "tasks": ["4.2"] },
    { "id": 4, "tasks": ["6.1"] }
  ]
}
```
