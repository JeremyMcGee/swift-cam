using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace SwiftCam.Tests.Unit;

public class CaptureEndpointTests : IDisposable
{
    private readonly string _tempDir;

    public CaptureEndpointTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "swiftcam-endpoint-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private HttpClient CreateClient(IFrameBroadcaster? broadcaster = null, string? captureDir = null)
    {
        var factory = new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<MotionSettings>(opts =>
                {
                    opts.CaptureDirectory = captureDir ?? _tempDir;
                });

                if (broadcaster != null)
                {
                    services.RemoveAll<IFrameBroadcaster>();
                    services.AddSingleton(broadcaster);
                }
            });
        });

        return factory.CreateClient();
    }

    // --- POST /api/captures tests ---

    [Fact]
    public async Task PostCaptures_Success_Returns201WithFilename()
    {
        var frameData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        var broadcaster = new FakeFrameBroadcaster(frameData);
        var client = CreateClient(broadcaster);

        var response = await client.PostAsync("/api/captures", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("filename", out var filenameProp));
        var filename = filenameProp.GetString();
        Assert.NotNull(filename);
        Assert.EndsWith(".jpg", filename);

        // Verify the file was actually written
        var savedPath = Path.Combine(_tempDir, filename);
        Assert.True(File.Exists(savedPath));
        var savedBytes = await File.ReadAllBytesAsync(savedPath);
        Assert.Equal(frameData, savedBytes);
    }

    [Fact]
    public async Task PostCaptures_Timeout_Returns503()
    {
        var broadcaster = new TimeoutFrameBroadcaster();
        var client = CreateClient(broadcaster);

        var response = await client.PostAsync("/api/captures", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Contains("camera", errorProp.GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostCaptures_FileWriteFails_Returns500()
    {
        var frameData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var broadcaster = new FakeFrameBroadcaster(frameData);
        // Use a non-existent path under a read-only or invalid location
        // On Windows, writing to an invalid path like NUL directory causes IOException
        var invalidDir = Path.Combine(_tempDir, "nonexistent", new string('x', 300));
        var client = CreateClient(broadcaster, invalidDir);

        var response = await client.PostAsync("/api/captures", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Contains("file system", errorProp.GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    // --- DELETE /api/captures/{filename} tests ---

    [Fact]
    public async Task DeleteCapture_FileExists_Returns204()
    {
        var filename = "2025-Jan-15_14-30-22.jpg";
        var filePath = Path.Combine(_tempDir, filename);
        await File.WriteAllBytesAsync(filePath, new byte[] { 0xFF, 0xD8 });

        var client = CreateClient();

        var response = await client.DeleteAsync($"/api/captures/{filename}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task DeleteCapture_FileNotFound_Returns404()
    {
        var client = CreateClient();

        var response = await client.DeleteAsync("/api/captures/2025-Jan-15_14-30-22.jpg");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Contains("not found", errorProp.GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteCapture_PathTraversal_Returns400()
    {
        var client = CreateClient();

        var response = await client.DeleteAsync("/api/captures/..%2Fsecret.jpg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Contains("path traversal", errorProp.GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteCapture_NonJpgExtension_Returns400()
    {
        var client = CreateClient();

        var response = await client.DeleteAsync("/api/captures/photo.png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Contains(".jpg", errorProp.GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteCapture_FileSystemError_Returns500()
    {
        // Create a file and then make it un-deletable by locking it with a stream
        var filename = "2025-Jun-20_10-00-00.jpg";
        var filePath = Path.Combine(_tempDir, filename);
        await File.WriteAllBytesAsync(filePath, new byte[] { 0xFF, 0xD8 });

        // Lock the file by opening it exclusively
        using var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var client = CreateClient();

        var response = await client.DeleteAsync($"/api/captures/{filename}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Contains("could not be completed", errorProp.GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    // --- Test doubles ---

    /// <summary>
    /// A fake IFrameBroadcaster that returns the provided frame data from subscriptions.
    /// </summary>
    private sealed class FakeFrameBroadcaster : IFrameBroadcaster
    {
        private readonly byte[] _frameData;

        public FakeFrameBroadcaster(byte[] frameData) => _frameData = frameData;

        public int ClientCount => 0;

        public void PublishFrame(byte[] jpegData) { }

        public IFrameSubscription Subscribe() => new FakeSubscription(_frameData);

        private sealed class FakeSubscription : IFrameSubscription
        {
            private readonly byte[] _data;
            public FakeSubscription(byte[] data) => _data = data;
            public ValueTask<byte[]> WaitForFrameAsync(CancellationToken ct) => new(_data);
            public void Dispose() { }
        }
    }

    /// <summary>
    /// A fake IFrameBroadcaster whose subscription always throws OperationCanceledException
    /// to simulate a timeout scenario.
    /// </summary>
    private sealed class TimeoutFrameBroadcaster : IFrameBroadcaster
    {
        public int ClientCount => 0;
        public void PublishFrame(byte[] jpegData) { }
        public IFrameSubscription Subscribe() => new TimeoutSubscription();

        private sealed class TimeoutSubscription : IFrameSubscription
        {
            public ValueTask<byte[]> WaitForFrameAsync(CancellationToken ct)
                => throw new OperationCanceledException(ct);
            public void Dispose() { }
        }
    }
}
