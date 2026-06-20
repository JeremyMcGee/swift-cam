# Requirements Document

## Introduction

The Capture Management feature extends the existing SwiftCam capture gallery with two new capabilities: manually triggering a new capture from the live camera feed, and deleting an existing capture image with a confirmation step. These operations complement the read-only gallery by giving users control over what is stored.

## Glossary

- **Capture_API**: The set of HTTP endpoints that serve capture metadata, image files, and manage capture lifecycle
- **Capture_Image**: A JPEG file saved to the Capture_Directory with the filename format yyyy-MMM-dd_HH-mm-ss.jpg
- **Capture_Directory**: The file system directory where captured JPEGs are stored, configured via MotionSettings.CaptureDirectory
- **Gallery_Panel**: The scrollable UI container that displays capture thumbnails
- **Frame_Broadcaster**: The service that distributes live camera frames to subscribers
- **Web_Page**: The HTML page served at "/" that shows the live MJPEG stream, audio status, and the Gallery_Panel
- **Confirmation_Dialog**: A UI prompt that requires explicit user approval before a destructive action is executed

## Requirements

### Requirement 1: Take New Capture API Endpoint

**User Story:** As a user, I want to trigger a manual capture from the live camera feed, so that I can save a snapshot of what the camera currently sees.

#### Acceptance Criteria

1. WHEN a POST request is made to /api/captures, THE Capture_API SHALL subscribe to the Frame_Broadcaster, retrieve the next available frame within 5 seconds, and save it as a JPEG file in the Capture_Directory
2. WHEN a POST request is made to /api/captures and the capture is saved successfully, THE Capture_API SHALL return HTTP status 201 with a JSON body containing a "filename" field set to the name of the saved Capture_Image
3. IF no camera frame is received from the Frame_Broadcaster within 5 seconds, THEN THE Capture_API SHALL return HTTP status 503 with a JSON body containing an "error" field with a message indicating that no camera frame is available
4. THE Capture_API SHALL generate the filename for the new Capture_Image using the current UTC timestamp in the format yyyy-MMM-dd_HH-mm-ss.jpg
5. IF the Capture_Directory is not writable or the file write fails, THEN THE Capture_API SHALL return HTTP status 500 with a JSON body containing an "error" field with a message indicating that the capture could not be saved
6. IF two captures are requested within the same second producing an identical filename, THEN THE Capture_API SHALL append a numeric suffix (e.g., _1, _2) before the file extension to ensure a unique filename

### Requirement 2: Delete Capture API Endpoint

**User Story:** As a user, I want to delete a capture image I no longer need, so that I can manage disk space and remove unwanted snapshots.

#### Acceptance Criteria

1. WHEN a DELETE request is made to /api/captures/{filename}, THE Capture_API SHALL delete the specified Capture_Image from the Capture_Directory and return HTTP status 204 with no response body
2. WHEN a DELETE request is made to /api/captures/{filename} and the file does not exist in the Capture_Directory, THE Capture_API SHALL return HTTP status 404 with a JSON body containing an error message indicating the file was not found
3. IF the requested filename contains path traversal characters (.. or / or \\), THEN THE Capture_API SHALL return HTTP status 400 with a JSON body containing an error message indicating invalid filename
4. IF the requested filename does not end with .jpg (case-insensitive), THEN THE Capture_API SHALL return HTTP status 400 with a JSON body containing an error message indicating an unsupported file extension
5. IF the file exists but the delete operation fails due to a file system error, THEN THE Capture_API SHALL return HTTP status 500 with a JSON body containing an error message indicating the deletion could not be completed

### Requirement 3: Take Capture UI Control

**User Story:** As a user, I want a button in the gallery panel to take a new capture, so that I can save a snapshot without leaving the page.

#### Acceptance Criteria

1. THE Gallery_Panel SHALL display a "Take Capture" button in the gallery-header alongside the existing "Refresh" button
2. WHEN the "Take Capture" button is clicked, THE Gallery_Panel SHALL send a POST request to /api/captures
3. WHEN the capture is saved successfully (HTTP 201 response), THE Gallery_Panel SHALL refresh the thumbnail list by re-fetching GET /api/captures to include the new Capture_Image
4. WHILE a capture request is in progress, THE Gallery_Panel SHALL disable the "Take Capture" button and display "Capturing..." as the button text to indicate a loading state
5. IF the capture request fails (non-2xx HTTP response or network error), THEN THE Gallery_Panel SHALL re-enable the "Take Capture" button and display an error message within the gallery panel for 5 seconds without affecting other page components

### Requirement 4: Delete Capture UI with Confirmation

**User Story:** As a user, I want to delete a capture from the gallery with a confirmation step, so that I do not accidentally remove important snapshots.

#### Acceptance Criteria

1. THE Gallery_Panel SHALL display a delete control on each Thumbnail
2. WHEN the delete control is activated, THE Gallery_Panel SHALL display a Confirmation_Dialog asking the user to confirm deletion of the associated Capture_Image
3. WHEN the user confirms deletion in the Confirmation_Dialog, THE Gallery_Panel SHALL send a DELETE request to /api/captures/{filename}
4. WHEN the deletion is successful, THE Gallery_Panel SHALL remove the deleted Thumbnail from the display without requiring a full page reload
5. WHEN the user cancels the Confirmation_Dialog, THE Gallery_Panel SHALL dismiss the dialog and take no further action on the associated Thumbnail
6. IF the delete request fails, THEN THE Gallery_Panel SHALL display an error message indicating the deletion was unsuccessful, retain the Thumbnail in the display, and automatically dismiss the error message after 5 seconds
7. WHILE a delete request is in progress for a Thumbnail, THE Gallery_Panel SHALL disable the delete control on that Thumbnail and indicate a loading state until the request completes or fails

### Requirement 5: Non-Interference with Existing Features

**User Story:** As a user, I want capture management operations to work independently of the live stream and audio playback, so that taking or deleting captures does not degrade the viewing experience.

#### Acceptance Criteria

1. THE Capture_API SHALL handle POST and DELETE requests without dropping frames or adding latency to MJPEG frame delivery for any active stream subscriber
2. THE Gallery_Panel capture and delete operations SHALL not interrupt or delay the audio status polling interval or prevent the audio status display from updating
3. WHILE a capture or delete operation is in progress, THE Web_Page SHALL continue displaying the live MJPEG stream with no visible freeze or frame gap
4. IF a POST or DELETE request to the Capture_API fails due to a file system error, THEN THE Capture_API SHALL return the appropriate error response without affecting the Frame_Broadcaster or any active MJPEG stream connection
5. THE Capture_API SHALL complete POST and DELETE requests within 5 seconds, even while the maximum number of stream subscribers (10) are connected
