using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SwiftCam;

public class Program
{
    private const string HtmlPage = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <title>Raspberry Pi Camera</title>
            <style>
                body {
                    margin: 0;
                    background: #000;
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    min-height: 100vh;
                    font-family: sans-serif;
                }
                img {
                    width: 100%;
                    max-width: 1280px;
                    height: auto;
                }
                #audio-status-panel {
                    width: 100%;
                    max-width: 1280px;
                    background: #1a1a1a;
                    color: #e0e0e0;
                    padding: 12px 16px;
                    box-sizing: border-box;
                    border-top: 1px solid #333;
                    font-size: 14px;
                    line-height: 1.4;
                }
                #audio-status-panel .label {
                    color: #999;
                    margin-right: 6px;
                }
                #audio-status-panel .value {
                    color: #fff;
                }
                #audio-status-panel .unavailable {
                    color: #f44;
                }
                #capture-gallery {
                    width: 100%;
                    max-width: 1280px;
                    max-height: 400px;
                    overflow-y: scroll;
                    background: #1a1a1a;
                    color: #e0e0e0;
                    padding: 12px 16px;
                    box-sizing: border-box;
                    border-top: 1px solid #333;
                    font-size: 14px;
                }
                #capture-gallery .gallery-header {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    margin-bottom: 12px;
                }
                #capture-gallery .gallery-header h2 {
                    margin: 0;
                    font-size: 16px;
                    color: #fff;
                }
                #capture-gallery .gallery-header button {
                    background: #333;
                    color: #e0e0e0;
                    border: 1px solid #555;
                    padding: 4px 12px;
                    cursor: pointer;
                    border-radius: 4px;
                    font-size: 13px;
                }
                #capture-gallery .gallery-header button:hover {
                    background: #444;
                }
                #capture-gallery .gallery-header button:disabled {
                    opacity: 0.5;
                    cursor: not-allowed;
                }
                #capture-gallery .gallery-header .header-buttons {
                    display: flex;
                    gap: 8px;
                }
                #capture-gallery .gallery-error {
                    background: #4a1c1c;
                    color: #f88;
                    border: 1px solid #622;
                    border-radius: 4px;
                    padding: 8px 12px;
                    margin-bottom: 12px;
                    font-size: 13px;
                }
                #capture-gallery .gallery-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
                    gap: 12px;
                }
                #capture-gallery .gallery-grid .thumb {
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    position: relative;
                }
                #capture-gallery .gallery-grid .thumb img {
                    width: 100%;
                    height: auto;
                    border-radius: 4px;
                    cursor: pointer;
                }
                #capture-gallery .gallery-grid .thumb .timestamp {
                    margin-top: 4px;
                    font-size: 11px;
                    color: #999;
                    text-align: center;
                }
                #capture-gallery .gallery-grid .thumb .delete-btn {
                    position: absolute;
                    top: 4px;
                    right: 4px;
                    background: rgba(0,0,0,0.7);
                    color: #f88;
                    border: 1px solid #622;
                    border-radius: 50%;
                    width: 24px;
                    height: 24px;
                    font-size: 14px;
                    line-height: 22px;
                    text-align: center;
                    cursor: pointer;
                    padding: 0;
                }
                #capture-gallery .gallery-grid .thumb .delete-btn:hover {
                    background: rgba(80,0,0,0.9);
                    color: #fff;
                }
                #capture-gallery .gallery-grid .thumb .delete-btn:disabled {
                    opacity: 0.5;
                    cursor: not-allowed;
                }
                #capture-gallery .gallery-empty {
                    color: #999;
                    font-style: italic;
                }
                #delete-confirm-dialog {
                    display: none;
                    position: fixed;
                    top: 0;
                    left: 0;
                    width: 100%;
                    height: 100%;
                    background: rgba(0,0,0,0.6);
                    z-index: 1000;
                    align-items: center;
                    justify-content: center;
                }
                #delete-confirm-dialog.visible {
                    display: flex;
                }
                #delete-confirm-dialog .dialog-box {
                    background: #222;
                    border: 1px solid #555;
                    border-radius: 8px;
                    padding: 24px;
                    max-width: 400px;
                    width: 90%;
                    text-align: center;
                    color: #e0e0e0;
                }
                #delete-confirm-dialog .dialog-box p {
                    margin: 0 0 16px 0;
                    font-size: 14px;
                }
                #delete-confirm-dialog .dialog-box .dialog-filename {
                    font-weight: bold;
                    color: #fff;
                    word-break: break-all;
                }
                #delete-confirm-dialog .dialog-box .dialog-buttons {
                    display: flex;
                    gap: 12px;
                    justify-content: center;
                }
                #delete-confirm-dialog .dialog-box .dialog-buttons button {
                    padding: 6px 16px;
                    border-radius: 4px;
                    border: 1px solid #555;
                    cursor: pointer;
                    font-size: 13px;
                }
                #delete-confirm-dialog .dialog-box .dialog-buttons .confirm-btn {
                    background: #a33;
                    color: #fff;
                    border-color: #c44;
                }
                #delete-confirm-dialog .dialog-box .dialog-buttons .confirm-btn:hover {
                    background: #c44;
                }
                #delete-confirm-dialog .dialog-box .dialog-buttons .cancel-btn {
                    background: #333;
                    color: #e0e0e0;
                }
                #delete-confirm-dialog .dialog-box .dialog-buttons .cancel-btn:hover {
                    background: #444;
                }
            </style>
        </head>
        <body>
            <img src="/stream" alt="Camera Stream" />
            <div id="audio-status-panel" role="status" aria-live="polite" aria-atomic="true">
                <span class="label">Audio:</span>
                <span id="audio-state" class="value">Loading...</span>
                <span class="label" style="margin-left:16px;">Reason:</span>
                <span id="audio-reason" class="value"></span>
            </div>
            <div id="capture-gallery">
                <div class="gallery-header">
                    <h2>Captures</h2>
                    <div class="header-buttons">
                        <button id="take-capture-btn" type="button">Take Capture</button>
                        <button id="gallery-refresh-btn" type="button">Refresh</button>
                    </div>
                </div>
                <div id="gallery-grid" class="gallery-grid"></div>
                <p id="gallery-empty" class="gallery-empty">No captures yet</p>
            </div>
            <div id="delete-confirm-dialog" role="dialog" aria-modal="true" aria-labelledby="delete-dialog-title">
                <div class="dialog-box">
                    <p id="delete-dialog-title">Delete capture <span id="delete-dialog-filename" class="dialog-filename"></span>?</p>
                    <div class="dialog-buttons">
                        <button type="button" class="cancel-btn" id="delete-cancel-btn">Cancel</button>
                        <button type="button" class="confirm-btn" id="delete-confirm-btn">Delete</button>
                    </div>
                </div>
            </div>
            <script>
                (function() {
                    var stateEl = document.getElementById('audio-state');
                    var reasonEl = document.getElementById('audio-reason');
                    var wasUnavailable = false;

                    function pollStatus() {
                        fetch('/api/audio-status')
                            .then(function(res) {
                                if (!res.ok) throw new Error('HTTP ' + res.status);
                                return res.json();
                            })
                            .then(function(data) {
                                stateEl.textContent = data.state || 'Unknown';
                                stateEl.className = 'value';
                                reasonEl.textContent = data.reason || '';
                                reasonEl.className = 'value';
                                wasUnavailable = false;
                            })
                            .catch(function() {
                                stateEl.textContent = 'Status unavailable';
                                stateEl.className = 'value unavailable';
                                reasonEl.textContent = '';
                                wasUnavailable = true;
                            });
                    }

                    pollStatus();
                    setInterval(pollStatus, 5000);
                })();
            </script>
            <script>
                (function() {
                    var gridEl = document.getElementById('gallery-grid');
                    var emptyEl = document.getElementById('gallery-empty');
                    var refreshBtn = document.getElementById('gallery-refresh-btn');
                    var captureBtn = document.getElementById('take-capture-btn');
                    var galleryEl = document.getElementById('capture-gallery');
                    var deleteDialog = document.getElementById('delete-confirm-dialog');
                    var deleteDialogFilename = document.getElementById('delete-dialog-filename');
                    var deleteCancelBtn = document.getElementById('delete-cancel-btn');
                    var deleteConfirmBtn = document.getElementById('delete-confirm-btn');
                    var isLoading = false;
                    var isCaptureLoading = false;
                    var pendingDelete = null;
                    var deletingFiles = new Set();

                    var months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

                    function parseFilenameTimestamp(filename) {
                        // Format: yyyy-MMM-dd_HH-mm-ss.jpg e.g. 2025-Jan-15_14-30-22.jpg
                        var name = filename.replace('.jpg', '').replace('.JPG', '');
                        var parts = name.split('_');
                        if (parts.length < 2) return filename;
                        var datePart = parts[0]; // yyyy-MMM-dd
                        var timePart = parts[1]; // HH-mm-ss
                        var datePieces = datePart.split('-');
                        if (datePieces.length < 3) return filename;
                        var year = datePieces[0];
                        var month = datePieces[1];
                        var day = datePieces[2];
                        var timePieces = timePart.split('-');
                        if (timePieces.length < 3) return filename;
                        var hour = timePieces[0];
                        var minute = timePieces[1];
                        var second = timePieces[2];
                        return month + ' ' + parseInt(day, 10) + ', ' + year + ' ' + hour + ':' + minute + ':' + second;
                    }

                    function renderGallery(captures) {
                        gridEl.innerHTML = '';
                        if (!captures || captures.length === 0) {
                            emptyEl.style.display = '';
                            return;
                        }
                        emptyEl.style.display = 'none';
                        for (var i = 0; i < captures.length; i++) {
                            var filename = captures[i];
                            var thumbDiv = document.createElement('div');
                            thumbDiv.className = 'thumb';
                            thumbDiv.setAttribute('data-filename', filename);

                            var link = document.createElement('a');
                            link.href = '/api/captures/' + encodeURIComponent(filename);
                            link.target = '_blank';
                            link.rel = 'noopener';

                            var img = document.createElement('img');
                            img.src = '/api/captures/' + encodeURIComponent(filename);
                            img.alt = filename;
                            img.loading = 'lazy';
                            link.appendChild(img);

                            var deleteBtn = document.createElement('button');
                            deleteBtn.className = 'delete-btn';
                            deleteBtn.type = 'button';
                            deleteBtn.textContent = '\u00D7';
                            deleteBtn.title = 'Delete ' + filename;
                            deleteBtn.setAttribute('aria-label', 'Delete ' + filename);
                            deleteBtn.setAttribute('data-filename', filename);
                            if (deletingFiles.has(filename)) {
                                deleteBtn.disabled = true;
                                deleteBtn.textContent = '\u2026';
                            }
                            deleteBtn.addEventListener('click', function(e) {
                                e.preventDefault();
                                e.stopPropagation();
                                var fn = this.getAttribute('data-filename');
                                if (!deletingFiles.has(fn)) {
                                    showDeleteDialog(fn);
                                }
                            });

                            var ts = document.createElement('span');
                            ts.className = 'timestamp';
                            ts.textContent = parseFilenameTimestamp(filename);

                            thumbDiv.appendChild(link);
                            thumbDiv.appendChild(deleteBtn);
                            thumbDiv.appendChild(ts);
                            gridEl.appendChild(thumbDiv);
                        }
                    }

                    function fetchCaptures() {
                        if (isLoading) return;
                        isLoading = true;
                        refreshBtn.disabled = true;
                        refreshBtn.textContent = 'Loading...';

                        fetch('/api/captures')
                            .then(function(res) {
                                if (!res.ok) throw new Error('HTTP ' + res.status);
                                return res.json();
                            })
                            .then(function(data) {
                                renderGallery(data);
                            })
                            .catch(function() {
                                gridEl.innerHTML = '';
                                emptyEl.style.display = '';
                                emptyEl.textContent = 'Failed to load captures';
                            })
                            .finally(function() {
                                isLoading = false;
                                refreshBtn.disabled = false;
                                refreshBtn.textContent = 'Refresh';
                            });
                    }

                    refreshBtn.addEventListener('click', fetchCaptures);
                    fetchCaptures();

                    function showGalleryError(message) {
                        var errorDiv = document.createElement('div');
                        errorDiv.className = 'gallery-error';
                        errorDiv.textContent = message;
                        galleryEl.insertBefore(errorDiv, gridEl);
                        setTimeout(function() {
                            if (errorDiv.parentNode) {
                                errorDiv.parentNode.removeChild(errorDiv);
                            }
                        }, 5000);
                    }

                    function takeCapture() {
                        if (isCaptureLoading) return;
                        isCaptureLoading = true;
                        captureBtn.disabled = true;
                        captureBtn.textContent = 'Capturing...';

                        fetch('/api/captures', { method: 'POST' })
                            .then(function(res) {
                                if (res.status === 201) {
                                    fetchCaptures();
                                } else {
                                    return res.json().then(function(data) {
                                        showGalleryError(data.error || 'Capture failed');
                                    });
                                }
                            })
                            .catch(function() {
                                showGalleryError('Network error: could not reach the server');
                            })
                            .finally(function() {
                                isCaptureLoading = false;
                                captureBtn.disabled = false;
                                captureBtn.textContent = 'Take Capture';
                            });
                    }

                    captureBtn.addEventListener('click', takeCapture);

                    function showDeleteDialog(filename) {
                        pendingDelete = filename;
                        deleteDialogFilename.textContent = filename;
                        deleteDialog.classList.add('visible');
                    }

                    function hideDeleteDialog() {
                        pendingDelete = null;
                        deleteDialog.classList.remove('visible');
                    }

                    function confirmDelete() {
                        if (!pendingDelete) return;
                        var filename = pendingDelete;
                        hideDeleteDialog();

                        deletingFiles.add(filename);
                        var thumbDiv = gridEl.querySelector('[data-filename="' + CSS.escape(filename) + '"]');
                        var btn = thumbDiv ? thumbDiv.querySelector('.delete-btn') : null;
                        if (btn) {
                            btn.disabled = true;
                            btn.textContent = '\u2026';
                        }

                        fetch('/api/captures/' + encodeURIComponent(filename), { method: 'DELETE' })
                            .then(function(res) {
                                if (res.status === 204) {
                                    if (thumbDiv && thumbDiv.parentNode) {
                                        thumbDiv.parentNode.removeChild(thumbDiv);
                                    }
                                    // Show empty message if no thumbnails remain
                                    if (gridEl.children.length === 0) {
                                        emptyEl.style.display = '';
                                    }
                                } else {
                                    return res.json().then(function(data) {
                                        showGalleryError(data.error || 'Delete failed');
                                    });
                                }
                            })
                            .catch(function() {
                                showGalleryError('Network error: could not reach the server');
                            })
                            .finally(function() {
                                deletingFiles.delete(filename);
                                if (btn) {
                                    btn.disabled = false;
                                    btn.textContent = '\u00D7';
                                }
                            });
                    }

                    deleteCancelBtn.addEventListener('click', hideDeleteDialog);
                    deleteConfirmBtn.addEventListener('click', confirmDelete);
                })();
            </script>
        </body>
        </html>
        """;

    public static async Task<int> Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure console logging with timestamp format
        builder.Logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
        });

        // Configure Kestrel to listen on 0.0.0.0:8080 (HTTP only, no HTTPS)
        builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(8080));

        // Bind CameraSettings from the "Camera" configuration section
        builder.Services.Configure<CameraSettings>(builder.Configuration.GetSection("Camera"));
        builder.Services.AddSingleton<IValidateOptions<CameraSettings>, CameraSettingsValidator>();
        builder.Services.AddOptionsWithValidateOnStart<CameraSettings>();

        // Bind MotionSettings from the "Motion" configuration section
        builder.Services.Configure<MotionSettings>(builder.Configuration.GetSection("Motion"));
        builder.Services.AddSingleton<IValidateOptions<MotionSettings>, MotionSettingsValidator>();
        builder.Services.AddOptionsWithValidateOnStart<MotionSettings>();

        // Register TimeProvider
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

        // Register MotionDetector as hosted service
        builder.Services.AddHostedService<MotionDetector>();

        // Register FrameBroadcaster as singleton
        builder.Services.AddSingleton<IFrameBroadcaster, FrameBroadcaster>();

        // Register CameraService as both ICameraService singleton and hosted service
        builder.Services.AddSingleton<ICameraService, CameraService>();
        builder.Services.AddHostedService(sp => (CameraService)sp.GetRequiredService<ICameraService>());

        // Configure graceful shutdown timeout to 5 seconds
        builder.Services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(5));

        // Audio settings
        builder.Services.Configure<AudioSettings>(builder.Configuration.GetSection("Audio"));
        builder.Services.AddSingleton<IValidateOptions<AudioSettings>, AudioSettingsValidator>();
        builder.Services.AddOptionsWithValidateOnStart<AudioSettings>();

        // Audio services
        builder.Services.AddSingleton<ISolarCalculator, SolarCalculatorWrapper>();
        builder.Services.AddSingleton<IAudioProcessManager, AudioProcessManager>();
        builder.Services.AddSingleton<IWeatherService, WeatherService>();
        builder.Services.AddHostedService(sp => (WeatherService)sp.GetRequiredService<IWeatherService>());
        builder.Services.AddSingleton<AudioService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<AudioService>());
        builder.Services.AddHttpClient<WeatherService>();

        var app = builder.Build();

        MapRoutes(app);

        app.Logger.LogInformation("SwiftCam starting on http://0.0.0.0:8080");

        try
        {
            await app.RunAsync();
            return Environment.ExitCode;
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            app.Logger.LogCritical(
                "Failed to start: port 8080 is already in use. {Message}",
                ex.Message);
            return 1;
        }
        catch (SocketException ex)
        {
            app.Logger.LogCritical(
                "Failed to start: port 8080 is already in use. {Message}",
                ex.Message);
            return 1;
        }
    }

    internal static void MapRoutes(WebApplication app)
    {
        // GET / — serve the HTML video page
        app.MapGet("/", () => Results.Content(HtmlPage, "text/html"));

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
    }
}
