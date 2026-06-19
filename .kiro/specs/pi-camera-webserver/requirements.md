# Requirements Document

## Introduction

This document defines the requirements for a .NET/C# application that runs on a Raspberry Pi and serves a live video stream from the standard Raspberry Pi camera module over HTTP on port 8080. The application hosts an HTML page that displays the video feed, allowing any browser on the local network to view the camera output without requiring SSL.

## Glossary

- **Web_Server**: The HTTP server component of the application that listens on port 8080 and serves content to clients
- **Camera_Module**: The standard Raspberry Pi camera module connected via CSI interface (e.g., v2 or HQ camera)
- **Stream_Handler**: The component responsible for capturing frames from the Camera_Module and encoding them for transmission
- **Video_Page**: The HTML page served by the Web_Server that displays the live video stream
- **MJPEG_Stream**: A Motion JPEG stream where each frame is sent as a discrete JPEG image over HTTP using multipart content boundaries
- **Client**: A web browser or HTTP client that connects to the Web_Server to view the video feed

## Requirements

### Requirement 1: Web Server Initialization

**User Story:** As a user, I want the application to start an HTTP server on port 8080, so that I can access the camera stream from any device on the network.

#### Acceptance Criteria

1. WHEN the application starts, THE Web_Server SHALL bind to port 8080 on all network interfaces (0.0.0.0)
2. THE Web_Server SHALL use plain HTTP without SSL/TLS encryption
3. IF port 8080 is already in use, THEN THE Web_Server SHALL log an error message indicating the port conflict and terminate gracefully with a non-zero exit code
4. WHEN the application receives a termination signal (SIGTERM or SIGINT), THE Web_Server SHALL stop accepting new connections and shut down gracefully within 5 seconds
5. WHEN the Web_Server binds successfully, THE Web_Server SHALL log a startup confirmation message including the bound address and port

### Requirement 2: HTML Video Page

**User Story:** As a user, I want to open a browser and see the live camera feed on a simple HTML page, so that I can monitor the camera output without installing additional software.

#### Acceptance Criteria

1. WHEN a Client sends a GET request to the root path ("/"), THE Web_Server SHALL respond with an HTTP 200 status code and an HTML5 page containing a DOCTYPE declaration and the Video_Page content
2. THE Video_Page SHALL include an `<img>` element with its `src` attribute set to the "/stream" path
3. THE Video_Page SHALL include a `<title>` element containing the text "Raspberry Pi Camera"
4. THE Web_Server SHALL serve the Video_Page with a Content-Type header of "text/html"
5. IF a Client sends a GET request to a path other than "/" or "/stream", THEN THE Web_Server SHALL respond with an HTTP 404 status code and a plain-text body indicating the requested resource was not found

### Requirement 3: Camera Capture

**User Story:** As a user, I want the application to capture video from the Raspberry Pi camera module, so that live frames are available for streaming.

#### Acceptance Criteria

1. WHEN the application starts, THE Stream_Handler SHALL initialize the Camera_Module using the `libcamera` toolchain available on Raspberry Pi OS
2. THE Stream_Handler SHALL capture frames from the Camera_Module at a resolution of 640x480 pixels and at a minimum rate of 15 frames per second
3. THE Stream_Handler SHALL encode each captured frame as a JPEG image with a quality level between 70 and 85 (on a scale of 0 to 100)
4. IF the Camera_Module is not detected or fails to initialize, THEN THE Stream_Handler SHALL log an error message indicating the cause of failure and terminate the application with a non-zero exit code within 5 seconds of the failed initialization attempt

### Requirement 4: MJPEG Video Streaming

**User Story:** As a user, I want the video to stream continuously in my browser, so that I can observe real-time activity from the camera.

#### Acceptance Criteria

1. WHEN a Client sends a GET request to the "/stream" path, THE Web_Server SHALL respond with HTTP status 200 and a multipart MJPEG_Stream
2. THE Web_Server SHALL set the Content-Type header of the stream response to "multipart/x-mixed-replace; boundary=frame"
3. THE Stream_Handler SHALL send each JPEG frame as a separate part within the multipart response, preceded by the boundary marker, a Content-Type header of "image/jpeg", and a Content-Length header indicating the size of the JPEG data in bytes
4. WHILE a Client is connected to the "/stream" endpoint, THE Stream_Handler SHALL send new frames to that Client at the capture rate defined by the Camera_Module (minimum 15 frames per second)
5. WHEN a Client disconnects from the "/stream" endpoint, THE Stream_Handler SHALL stop sending frames to that Client and release the associated resources within 1 second
6. IF a Client sends a non-GET request to the "/stream" path, THEN THE Web_Server SHALL respond with HTTP status 405 and not initiate a stream

### Requirement 5: Multiple Client Support

**User Story:** As a user, I want multiple devices to view the camera stream simultaneously, so that more than one person can monitor the feed at the same time.

#### Acceptance Criteria

1. WHILE multiple Clients are connected to the "/stream" endpoint, THE Stream_Handler SHALL deliver frames to each connected Client independently, such that one Client's connection state or network speed does not affect frame delivery to other Clients, supporting at least 5 simultaneous Client connections
2. WHEN a new Client connects, THE Web_Server SHALL begin serving frames to the new Client without dropping frames to or disconnecting existing Client connections
3. THE Stream_Handler SHALL use a single Camera_Module capture session shared across all connected Clients
4. IF a connected Client is consuming frames slower than the capture rate, THEN THE Stream_Handler SHALL skip frames for that Client rather than blocking frame delivery to other Clients
5. IF the number of connected Clients exceeds 10, THEN THE Web_Server SHALL reject the new connection with an HTTP error response indicating the server is at capacity

### Requirement 6: Logging and Diagnostics

**User Story:** As a developer, I want the application to log key events, so that I can diagnose issues during operation.

#### Acceptance Criteria

1. WHEN the Web_Server starts successfully, THE Web_Server SHALL log the listening address and port to the console
2. WHEN a Client connects to the "/stream" endpoint, THE Web_Server SHALL log the connection event including the Client IP address
3. WHEN a Client disconnects from the "/stream" endpoint, THE Web_Server SHALL log the disconnection event including the Client IP address
4. IF an error occurs during frame capture, THEN THE Stream_Handler SHALL log the error source and error message to the console without terminating the stream for other connected Clients
5. THE Web_Server SHALL include a timestamp in each log entry
