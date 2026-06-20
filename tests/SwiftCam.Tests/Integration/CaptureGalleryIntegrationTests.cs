using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace SwiftCam.Tests.Integration;

/// <summary>
/// Integration tests for the capture gallery endpoints.
/// Validates content-type headers, non-interference with existing endpoints,
/// and correct application startup with gallery routes registered.
/// Requirements: 1.5, 2.1, 6.1, 6.2
/// </summary>
public class CaptureGalleryIntegrationTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly string _tempDir;

    public CaptureGalleryIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _tempDir = Path.Combine(Path.GetTempPath(), "swiftcam-gallery-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private HttpClient CreateClientWithCaptureDir(string captureDir)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<MotionSettings>(opts =>
                {
                    opts.CaptureDirectory = captureDir;
                });
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetCaptures_ReturnsApplicationJsonContentType()
    {
        // Arrange
        var client = CreateClientWithCaptureDir(_tempDir);

        // Act
        var response = await client.GetAsync("/api/captures");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetCaptureFile_ValidFile_ReturnsImageJpegContentType()
    {
        // Arrange
        var filename = "2025-Jan-15_14-30-22.jpg";
        var filePath = Path.Combine(_tempDir, filename);
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        await File.WriteAllBytesAsync(filePath, jpegBytes);

        var client = CreateClientWithCaptureDir(_tempDir);

        // Act
        var response = await client.GetAsync($"/api/captures/{filename}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CaptureEndpoints_DoNotInterfereWithStream()
    {
        // Arrange - make a request to captures first
        var client = CreateClientWithCaptureDir(_tempDir);
        await client.GetAsync("/api/captures");

        // Act - verify /stream route is still registered by sending a non-GET method
        // which returns 405 immediately (proving the route is matched without blocking on frames)
        var request = new HttpRequestMessage(HttpMethod.Post, "/stream");
        var response = await client.SendAsync(request);

        // Assert - 405 confirms the stream endpoint is still registered and functional
        // (If gallery routes broke routing, we'd get 404 instead)
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task CaptureEndpoints_DoNotInterfereWithAudioStatus()
    {
        // Arrange - make a request to captures first
        var client = CreateClientWithCaptureDir(_tempDir);
        await client.GetAsync("/api/captures");

        // Act - /api/audio-status should still work
        var response = await client.GetAsync("/api/audio-status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ApplicationStartsCorrectly_WithGalleryRoutesRegistered()
    {
        // Arrange & Act - creating a client exercises the full app startup pipeline
        var client = CreateClientWithCaptureDir(_tempDir);

        // Assert - all capture gallery routes respond correctly
        var capturesResponse = await client.GetAsync("/api/captures");
        Assert.Equal(HttpStatusCode.OK, capturesResponse.StatusCode);

        var fileResponse = await client.GetAsync("/api/captures/nonexistent.jpg");
        Assert.Equal(HttpStatusCode.NotFound, fileResponse.StatusCode);

        var invalidResponse = await client.GetAsync("/api/captures/test.png");
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        // Home page still works
        var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
    }
}
