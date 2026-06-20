# Requirements Document

## Introduction

The Capture Gallery feature adds a scrollable browsing panel to the SwiftCam web page that displays motion-captured images. It appears below the audio status indicator and allows the user to browse through JPEG captures stored on disk, view thumbnails sorted by most recent first, and open full-size images. The feature includes backend API endpoints for listing and serving captures, and a frontend UI component built with vanilla JavaScript.

## Glossary

- **Gallery_Panel**: The scrollable UI container displayed below the audio status panel that holds capture thumbnails
- **Capture_API**: The set of HTTP endpoints that serve capture metadata and image files
- **Capture_Image**: A JPEG file saved by the motion detection system with the filename format yyyy-MMM-dd_HH-mm-ss.jpg
- **Thumbnail**: A small preview of a Capture_Image displayed in the Gallery_Panel for browsing
- **Capture_Directory**: The file system directory where motion-captured JPEGs are stored, configured via MotionSettings.CaptureDirectory (default: "captures")
- **Web_Page**: The HTML page served at "/" that shows the live MJPEG stream, audio status, and the Gallery_Panel

## Requirements

### Requirement 1: List Captures API Endpoint

**User Story:** As a user, I want an API endpoint that returns a list of captured images sorted by most recent first, so that the gallery can display them in chronological order.

#### Acceptance Criteria

1. WHEN a GET request is made to /api/captures, THE Capture_API SHALL return a JSON array of capture filenames from the Capture_Directory sorted by most recent first
2. WHEN the Capture_Directory contains no files, THE Capture_API SHALL return an empty JSON array
3. WHEN the Capture_Directory does not exist, THE Capture_API SHALL return an empty JSON array
4. THE Capture_API SHALL only include files with a .jpg extension in the response
5. WHEN a GET request is made to /api/captures, THE Capture_API SHALL return the response with Content-Type application/json and HTTP status 200

### Requirement 2: Serve Capture Image Endpoint

**User Story:** As a user, I want to view individual captured images at full size, so that I can inspect motion events in detail.

#### Acceptance Criteria

1. WHEN a GET request is made to /api/captures/{filename}, THE Capture_API SHALL return the JPEG file contents with Content-Type image/jpeg and HTTP status 200
2. WHEN a GET request is made to /api/captures/{filename} and the file does not exist in the Capture_Directory, THE Capture_API SHALL return HTTP status 404
3. IF the requested filename contains path traversal characters (.. or /), THEN THE Capture_API SHALL return HTTP status 400
4. THE Capture_API SHALL only serve files with a .jpg extension from the Capture_Directory

### Requirement 3: Gallery Panel UI Layout

**User Story:** As a user, I want a scrollable gallery panel below the audio status indicator, so that I can browse captured images without disrupting the live stream view.

#### Acceptance Criteria

1. THE Web_Page SHALL display the Gallery_Panel below the audio status panel
2. THE Gallery_Panel SHALL have a fixed maximum height and scroll vertically when content overflows
3. THE Gallery_Panel SHALL have the same maximum width as the live stream image (1280px)
4. THE Gallery_Panel SHALL display a heading label of "Captures"
5. WHEN no captures are available, THE Gallery_Panel SHALL display the text "No captures yet"

### Requirement 4: Thumbnail Display

**User Story:** As a user, I want to see thumbnails of captured images in a grid layout, so that I can quickly scan through motion events.

#### Acceptance Criteria

1. THE Gallery_Panel SHALL display Capture_Images as Thumbnails in a grid layout
2. THE Gallery_Panel SHALL display Thumbnails in order from most recent to oldest (matching the API sort order)
3. WHEN a Thumbnail is clicked, THE Web_Page SHALL open the full-size Capture_Image in a new browser tab
4. THE Gallery_Panel SHALL display the capture timestamp below each Thumbnail, derived from the filename

### Requirement 5: Gallery Refresh

**User Story:** As a user, I want to refresh the gallery to see newly captured images, so that I can view recent motion events without reloading the entire page.

#### Acceptance Criteria

1. THE Gallery_Panel SHALL include a refresh button that fetches the latest capture list from the Capture_API
2. WHEN the refresh button is clicked, THE Gallery_Panel SHALL update the displayed Thumbnails with the current API response
3. WHILE the gallery is refreshing, THE Gallery_Panel SHALL indicate a loading state on the refresh button
4. THE Gallery_Panel SHALL perform an initial fetch of the capture list when the page loads

### Requirement 6: Non-Interference with Existing Features

**User Story:** As a user, I want the gallery to operate independently of the live stream and audio status, so that browsing captures does not degrade the viewing experience.

#### Acceptance Criteria

1. THE Gallery_Panel SHALL not interfere with the MJPEG stream connection or frame delivery
2. THE Gallery_Panel SHALL not interfere with the audio status polling interval or display
3. WHEN the Capture_API request fails, THE Gallery_Panel SHALL display an error message without affecting other page components
