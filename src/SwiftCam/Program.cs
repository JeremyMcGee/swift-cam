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
                #capture-gallery .gallery-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
                    gap: 12px;
                }
                #capture-gallery .gallery-grid .thumb {
                    display: flex;
                    flex-direction: column;
                    align-items: center;
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
                #capture-gallery .gallery-empty {
                    color: #999;
                    font-style: italic;
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
                    <button id="gallery-refresh-btn" type="button">Refresh</button>
                </div>
                <div id="gallery-grid" class="gallery-grid"></div>
                <p id="gallery-empty" class="gallery-empty">No captures yet</p>
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
                    var isLoading = false;

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

                            var link = document.createElement('a');
                            link.href = '/api/captures/' + encodeURIComponent(filename);
                            link.target = '_blank';
                            link.rel = 'noopener';

                            var img = document.createElement('img');
                            img.src = '/api/captures/' + encodeURIComponent(filename);
                            img.alt = filename;
                            img.loading = 'lazy';
                            link.appendChild(img);

                            var ts = document.createElement('span');
                            ts.className = 'timestamp';
                            ts.textContent = parseFilenameTimestamp(filename);

                            thumbDiv.appendChild(link);
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

        // Fallback catch-all — return 404 for all other paths
        app.MapFallback((HttpContext context) =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/plain";
            return context.Response.WriteAsync("Not Found");
        });
    }
}
