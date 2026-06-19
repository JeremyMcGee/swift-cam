using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SwiftCam;

namespace SwiftCam.Tests;

/// <summary>
/// Custom WebApplicationFactory that replaces CameraService with a no-op stub
/// so tests don't try to spawn libcamera-vid.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real CameraService hosted service registration
            services.RemoveAll<IHostedService>();

            // Replace ICameraService with a stub
            services.RemoveAll<ICameraService>();
            services.AddSingleton<ICameraService, StubCameraService>();
        });
    }

    private sealed class StubCameraService : ICameraService
    {
        public bool IsRunning => true;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void Dispose() { }
    }
}
