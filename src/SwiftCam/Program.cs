using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SwiftCam;

public class Program
{
    private const string HtmlPage = """
        <!DOCTYPE html>
        <html>
        <head>
            <title>Raspberry Pi Camera</title>
            <style>
                body { margin: 0; background: #000; display: flex; justify-content: center; align-items: center; min-height: 100vh; }
                img { width: 100%; max-width: 1280px; height: auto; }
            </style>
        </head>
        <body>
            <img src="/stream" alt="Camera Stream" />
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

        // Register FrameBroadcaster as singleton
        builder.Services.AddSingleton<IFrameBroadcaster, FrameBroadcaster>();

        // Register CameraService as both ICameraService singleton and hosted service
        builder.Services.AddSingleton<ICameraService, CameraService>();
        builder.Services.AddHostedService(sp => (CameraService)sp.GetRequiredService<ICameraService>());

        // Configure graceful shutdown timeout to 5 seconds
        builder.Services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(5));

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

        // Fallback catch-all — return 404 for all other paths
        app.MapFallback((HttpContext context) =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/plain";
            return context.Response.WriteAsync("Not Found");
        });
    }
}
