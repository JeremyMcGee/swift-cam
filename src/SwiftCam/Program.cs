using System.Net.Sockets;

namespace SwiftCam;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddSwiftCamServices();

        var app = builder.Build();
        app.MapSwiftCamRoutes();

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
}
