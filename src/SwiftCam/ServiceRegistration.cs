using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SwiftCam;

/// <summary>
/// Extension methods for registering all application services with the DI container.
/// </summary>
internal static class ServiceRegistration
{
    internal static WebApplicationBuilder AddSwiftCamServices(this WebApplicationBuilder builder)
    {
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

        return builder;
    }
}
