# Implementation Plan: Capture Management

## Overview

Extend the SwiftCam capture gallery with a manual capture endpoint (`POST /api/captures`) and a delete endpoint (`DELETE /api/captures/{filename}`), plus corresponding UI controls in the gallery panel. Implementation reuses existing services (FrameBroadcaster, CaptureWriter, CaptureFileService) and follows established patterns: static service classes for logic, thin route handlers in Program.cs, inline JavaScript for frontend.

## Tasks

- [x] 1. Implement CaptureService static class
  - [x] 1.1 Create `src/SwiftCam/CaptureService.cs` with `CaptureFrameAsync` method that subscribes to IFrameBroadcaster, waits for next frame with a configurable timeout via CancellationTokenSource, saves frame using CaptureWriter, and disposes the subscription in a finally block. Throw TimeoutException if no frame arrives, IOException if write fails.
    - Accept parameters: IFrameBroadcaster broadcaster, string captureDirectory, TimeProvider timeProvider, TimeSpan timeout, CancellationToken ct
    - Return the filename (not full path) of the saved capture
    - _Requirements: 1.1, 1.2, 1.3, 1.5_

  - [x] 1.2 Add `GenerateUniqueFilename` static method to CaptureService that takes a DateTime timestamp and capture directory, generates the base filename via `CaptureWriter.GenerateFilename`, and appends _1, _2, etc. if the file already exists until a unique name is found.
    - _Requirements: 1.4, 1.6_

  - [x]* 1.3 Write property test for capture save round-trip (Property 1)
    - **Property 1: Capture save round-trip**
    - Generate random byte arrays representing JPEG frame data, mock IFrameBroadcaster to return the bytes, call CaptureFrameAsync with a temp directory, read the saved file back and assert byte-for-byte equality with the original frame data.
    - **Validates: Requirements 1.1**

  - [x]* 1.4 Write property test for filename deduplication uniqueness (Property 2)
    - **Property 2: Filename deduplication uniqueness**
    - Generate random DateTime timestamps and random sets of pre-existing filenames (0–10 collisions). Call GenerateUniqueFilename and assert the returned filename does not match any existing file and follows the format `yyyy-MMM-dd_HH-mm-ss_N.jpg` where N is the lowest positive integer producing a unique name.
    - **Validates: Requirements 1.6**

- [x] 2. Implement CaptureDeleteService static class
  - [x] 2.1 Create `src/SwiftCam/CaptureDeleteService.cs` with `DeleteCapture(string filename, string captureDirectory)` method. Validate filename using `CaptureFileService.IsValidFilename` — throw ArgumentException with appropriate message if invalid (distinguishing path traversal from wrong extension). Resolve full path, throw FileNotFoundException if file doesn't exist, call File.Delete, throw IOException if delete fails.
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x]* 2.2 Write unit tests for CaptureDeleteService
    - Test successful deletion returns normally
    - Test throws FileNotFoundException for non-existent file
    - Test throws ArgumentException for path traversal characters (.. / \)
    - Test throws ArgumentException for non-.jpg extension
    - Test throws IOException when file system error occurs on delete
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Register API endpoints in Program.cs
  - [x] 4.1 Add `app.MapPost("/api/captures", ...)` route handler in Program.cs. Inject IFrameBroadcaster, IOptions<MotionSettings>, and TimeProvider. Call CaptureService.CaptureFrameAsync with 5-second timeout. Return 201 with JSON `{ "filename": "..." }` on success. Catch TimeoutException → 503 with error message. Catch MaxClientsExceededException → 503 with error message. Catch IOException → 500 with error message.
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 5.1, 5.4, 5.5_

  - [x] 4.2 Add `app.MapDelete("/api/captures/{filename}", ...)` route handler in Program.cs. Inject IOptions<MotionSettings>. Call CaptureDeleteService.DeleteCapture. Return 204 on success. Catch ArgumentException → 400 with appropriate error message (check message to distinguish traversal vs extension). Catch FileNotFoundException → 404 with error message. Catch IOException → 500 with error message.
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 5.1, 5.4_

  - [x]* 4.3 Write unit tests for API endpoints
    - Test POST /api/captures returns 201 with filename on success
    - Test POST /api/captures returns 503 when no frame within timeout
    - Test POST /api/captures returns 500 when file write fails
    - Test DELETE returns 204 when file exists and is deleted
    - Test DELETE returns 404 when file does not exist
    - Test DELETE returns 400 for path traversal filenames
    - Test DELETE returns 400 for non-.jpg filenames
    - Test DELETE returns 500 when file system error occurs
    - Use WebApplicationFactory with mocked IFrameBroadcaster
    - _Requirements: 1.2, 1.3, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Update gallery panel UI with capture and delete controls
  - [x] 6.1 Update the `HtmlPage` constant in Program.cs to add a "Take Capture" button in the gallery-header div alongside the existing "Refresh" button. Add JavaScript function `takeCapture()` that: sends POST to /api/captures, disables button and shows "Capturing..." during request, on 201 success refreshes thumbnail list via GET /api/captures, on failure shows error message in gallery panel for 5 seconds then auto-dismisses, re-enables button on completion.
    - Track `isCaptureLoading` state to prevent double-clicks
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 5.2, 5.3_

  - [x] 6.2 Update the `HtmlPage` constant in Program.cs to add a delete control (button/icon) on each thumbnail in the gallery. Add a confirmation dialog that shows when delete is activated, asking user to confirm deletion of the specific filename. On confirm: send DELETE /api/captures/{filename}, remove thumbnail from DOM on success (204), show error for 5 seconds on failure. On cancel: dismiss dialog, no action. Disable delete control and show loading state while request is in progress.
    - Track `pendingDelete` (filename awaiting confirmation) and `deletingFiles` (Set of filenames with in-flight requests)
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 5.2, 5.3_

  - [x]* 6.3 Write unit tests for UI elements
    - Test HTML contains "Take Capture" button in gallery-header
    - Test HTML contains delete controls on thumbnails
    - Test HTML contains confirmation dialog structure
    - Use WebApplicationFactory to fetch the page and inspect HTML content
    - _Requirements: 3.1, 4.1, 4.2_

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- FsCheck.Xunit is already in the test project; no new dependencies needed for property tests.
- Properties 3 and 4 from the design (filename validation rejects path traversal and non-jpg extensions) are already covered by existing `CaptureFileValidationPropertyTests` — no new tests needed.
- The project targets .NET 10; use TimeProvider for testable timestamp generation (already registered as singleton).
- Property tests should use `[Property(MaxTest = 100)]` with tag comments: `// Feature: capture-management, Property N: <description>`.
- CaptureWriter.GenerateFilename and CaptureFileService.IsValidFilename are existing utilities reused without modification.
- Tasks marked with `*` are optional and can be skipped for faster MVP.
- Each task references specific requirements for traceability.
- Checkpoints ensure incremental validation.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "2.2"] },
    { "id": 2, "tasks": ["1.3", "1.4", "4.1", "4.2"] },
    { "id": 3, "tasks": ["4.3"] },
    { "id": 4, "tasks": ["6.1", "6.2"] },
    { "id": 5, "tasks": ["6.3"] }
  ]
}
```
