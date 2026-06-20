using Microsoft.Extensions.Options;

namespace SwiftCam;

/// <summary>
/// Extension methods for mapping all HTTP routes.
/// </summary>
internal static class RouteRegistration
{
    internal static WebApplication MapSwiftCamRoutes(this WebApplication app)
    {
        // GET / — serve the HTML video page
        app.MapGet("/", () => Results.Content(HtmlPageContent.Html, "text/html"));

        // GET /style.css — serve the embedded stylesheet
        app.MapGet("/style.css", () => Results.Content(HtmlPageContent.Css, "text/css"));

        // GET /app.js — serve the embedded JavaScript
        app.MapGet("/app.js", () => Results.Content(HtmlPageContent.Js, "application/javascript"));

        // /stream — handle GET for MJPEG streaming, reject other methods with 405
        app.Map("/stream", async (HttpContext context, IFrameBroadcaster broadcaster, ILogger<Program> logger) =>
        {
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            try
            {
                await MjpegStreamWriter.WriteStreamAsync(
                    context,
                    broadcaster,
                    logger,
                    context.RequestAborted);
            }
            catch (MaxClientsExceededException)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Service Unavailable: Maximum number of concurrent clients exceeded.");
            }
        });

        // GET /api/audio-status — return current audio playback status as JSON
        app.MapGet("/api/audio-status", (AudioService audioService) =>
        {
            var reason = audioService.CurrentReason;
            if (reason.Length > 200)
            {
                reason = reason[..200];
            }

            var response = new AudioStatusResponse(
                State: audioService.CurrentState.ToString(),
                Reason: reason,
                CurrentWindowStart: audioService.CurrentWindow?.Start.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                CurrentWindowEnd: audioService.CurrentWindow?.End.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                NextWindowStart: audioService.NextWindow?.Start.ToString("yyyy-MM-ddTHH:mm:ssZ"));

            return Results.Ok(response);
        });

        // GET /api/captures — return list of capture filenames as JSON array
        app.MapGet("/api/captures", (IOptions<MotionSettings> motionSettings) =>
        {
            var filenames = CaptureListService.GetCaptureFilenames(motionSettings.Value.CaptureDirectory);
            return Results.Json(filenames);
        });

        // GET /api/captures/{filename} — serve a capture image file
        app.MapGet("/api/captures/{filename}", (string filename, IOptions<MotionSettings> motionSettings) =>
        {
            if (!CaptureFileService.IsValidFilename(filename))
                return Results.BadRequest();

            var path = CaptureFileService.ResolveCaptureFile(filename, motionSettings.Value.CaptureDirectory);
            if (path is null)
                return Results.NotFound();

            return Results.File(path, "image/jpeg");
        });

        // POST /api/captures — capture a single frame and save to disk
        app.MapPost("/api/captures", async (IFrameBroadcaster broadcaster, IOptions<MotionSettings> motionSettings, TimeProvider timeProvider, CancellationToken ct) =>
        {
            try
            {
                var filename = await CaptureService.CaptureFrameAsync(
                    broadcaster,
                    motionSettings.Value.CaptureDirectory,
                    timeProvider,
                    TimeSpan.FromSeconds(5),
                    ct);

                return Results.Json(new { filename }, statusCode: StatusCodes.Status201Created);
            }
            catch (TimeoutException)
            {
                return Results.Json(
                    new { error = "No camera frame available \u2014 the camera may not be running" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (MaxClientsExceededException)
            {
                return Results.Json(
                    new { error = "No camera frame available \u2014 maximum stream subscribers reached" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (IOException)
            {
                return Results.Json(
                    new { error = "Capture could not be saved due to a file system error" },
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        // DELETE /api/captures/{filename} — delete a capture file
        app.MapDelete("/api/captures/{filename}", (string filename, IOptions<MotionSettings> motionSettings) =>
        {
            try
            {
                CaptureDeleteService.DeleteCapture(filename, motionSettings.Value.CaptureDirectory);
                return Results.NoContent();
            }
            catch (ArgumentException ex) when (ex.Message.Contains("path traversal"))
            {
                return Results.Json(
                    new { error = "Invalid filename: path traversal characters are not allowed" },
                    statusCode: StatusCodes.Status400BadRequest);
            }
            catch (ArgumentException)
            {
                return Results.Json(
                    new { error = "Invalid filename: only .jpg files are supported" },
                    statusCode: StatusCodes.Status400BadRequest);
            }
            catch (FileNotFoundException)
            {
                return Results.Json(
                    new { error = "Capture not found: the specified file does not exist" },
                    statusCode: StatusCodes.Status404NotFound);
            }
            catch (IOException)
            {
                return Results.Json(
                    new { error = "Deletion could not be completed due to a file system error" },
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        // Fallback catch-all — return 404 for all other paths
        app.MapFallback((HttpContext context) =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/plain";
            return context.Response.WriteAsync("Not Found");
        });

        return app;
    }
}
