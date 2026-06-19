using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Unit tests for CameraService configuration and error handling.
/// Validates Requirements 3.2, 3.3, 3.4.
/// </summary>
public class CameraServiceConfigTests
{
    #region Test Doubles

    private class FakeFrameBroadcaster : IFrameBroadcaster
    {
        public int ClientCount => 0;
        public void PublishFrame(byte[] jpegData) { }
        public IFrameSubscription Subscribe() => throw new NotImplementedException();
    }

    private class FakeApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public bool StopRequested { get; private set; }

        public void StopApplication()
        {
            StopRequested = true;
        }
    }

    #endregion

    /// <summary>
    /// Verifies the CameraService uses the correct process name (libcamera-vid).
    /// Validates: Requirement 3.1
    /// </summary>
    [Fact]
    public void ProcessNames_ShouldContain_RpicamVidAndLibcameraVid()
    {
        var field = typeof(CameraService)
            .GetField("ProcessNames", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        var value = field!.GetValue(null) as string[];
        Assert.NotNull(value);
        Assert.Contains("rpicam-vid", value);
        Assert.Contains("libcamera-vid", value);
        // rpicam-vid should be tried first (preferred on newer Raspberry Pi OS)
        Assert.Equal("rpicam-vid", value![0]);
    }

    /// <summary>
    /// Verifies the process arguments include the correct resolution (640x480).
    /// Validates: Requirement 3.2
    /// </summary>
    [Fact]
    public void ProcessArguments_ShouldContain_Resolution640x480()
    {
        var args = GetProcessArguments();

        Assert.Contains("--width 640", args);
        Assert.Contains("--height 480", args);
    }

    /// <summary>
    /// Verifies the process arguments include the correct framerate (15 fps).
    /// Validates: Requirement 3.2
    /// </summary>
    [Fact]
    public void ProcessArguments_ShouldContain_Framerate15()
    {
        var args = GetProcessArguments();

        Assert.Contains("--framerate 15", args);
    }

    /// <summary>
    /// Verifies the process arguments include JPEG quality level of 80 (within 70-85 range).
    /// Validates: Requirement 3.3
    /// </summary>
    [Fact]
    public void ProcessArguments_ShouldContain_Quality80()
    {
        var args = GetProcessArguments();

        Assert.Contains("-q 80", args);
    }

    /// <summary>
    /// Verifies the process arguments include MJPEG codec.
    /// Validates: Requirement 3.2
    /// </summary>
    [Fact]
    public void ProcessArguments_ShouldContain_MjpegCodec()
    {
        var args = GetProcessArguments();

        Assert.Contains("--codec mjpeg", args);
    }

    /// <summary>
    /// Verifies the process arguments include indefinite duration (-t 0).
    /// </summary>
    [Fact]
    public void ProcessArguments_ShouldContain_IndefiniteDuration()
    {
        var args = GetProcessArguments();

        Assert.Contains("-t 0", args);
    }

    /// <summary>
    /// Verifies the process arguments include output to stdout (-o -).
    /// </summary>
    [Fact]
    public void ProcessArguments_ShouldContain_OutputToStdout()
    {
        var args = GetProcessArguments();

        Assert.Contains("-o -", args);
    }

    /// <summary>
    /// Verifies the process arguments include no-preview flag (-n).
    /// </summary>
    [Fact]
    public void ProcessArguments_ShouldContain_NoPreview()
    {
        var args = GetProcessArguments();

        Assert.Contains("-n", args);
    }

    /// <summary>
    /// Verifies that when the camera process fails to start (libcamera-vid not found),
    /// the service signals application shutdown.
    /// Validates: Requirement 3.4
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenProcessFailsToStart_SignalsApplicationShutdown()
    {
        var broadcaster = new FakeFrameBroadcaster();
        var lifetime = new FakeApplicationLifetime();
        var logger = NullLogger<CameraService>.Instance;
        var options = Options.Create(new CameraSettings());

        using var service = new CameraService(options, broadcaster, lifetime, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // StartAsync triggers ExecuteAsync internally
        await service.StartAsync(cts.Token);

        // Wait for the service to detect the failure and request shutdown
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!lifetime.StopRequested && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.True(lifetime.StopRequested,
            "CameraService should signal application shutdown when the camera process fails to start");
    }

    /// <summary>
    /// Verifies that when the camera process fails to start, IsRunning remains false.
    /// Validates: Requirement 3.4
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenProcessFailsToStart_IsRunningIsFalse()
    {
        var broadcaster = new FakeFrameBroadcaster();
        var lifetime = new FakeApplicationLifetime();
        var logger = NullLogger<CameraService>.Instance;
        var options = Options.Create(new CameraSettings());

        using var service = new CameraService(options, broadcaster, lifetime, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await service.StartAsync(cts.Token);

        // Wait for the service to detect the failure
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!lifetime.StopRequested && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.False(service.IsRunning,
            "CameraService.IsRunning should be false when the process fails to start");
    }

    #region Helpers

    private static string GetProcessArguments()
    {
        var options = Options.Create(new CameraSettings());
        var broadcaster = new FakeFrameBroadcaster();
        var lifetime = new FakeApplicationLifetime();
        var logger = NullLogger<CameraService>.Instance;

        using var service = new CameraService(options, broadcaster, lifetime, logger);

        var method = typeof(CameraService)
            .GetMethod("BuildProcessArguments", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        return method!.Invoke(service, null) as string ?? string.Empty;
    }

    #endregion
}
