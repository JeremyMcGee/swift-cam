using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace SwiftCam.Tests.Unit;

public class CaptureApiTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly string _tempDir;

    public CaptureApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _tempDir = Path.Combine(Path.GetTempPath(), "swiftcam-test-captures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private HttpClient CreateClientWithCaptureDir(string captureDir)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<MotionSettings>(opts =>
                {
                    opts.CaptureDirectory = captureDir;
                });
            });
        }).CreateClient();

        return client;
    }

    [Fact]
    public async Task GetCaptures_EmptyDirectory_Returns200WithEmptyArray()
    {
        var client = CreateClientWithCaptureDir(_tempDir);

        var response = await client.GetAsync("/api/captures");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var filenames = JsonSerializer.Deserialize<string[]>(body);
        Assert.NotNull(filenames);
        Assert.Empty(filenames);
    }

    [Fact]
    public async Task GetCaptureFile_MissingFile_Returns404()
    {
        var client = CreateClientWithCaptureDir(_tempDir);

        var response = await client.GetAsync("/api/captures/2025-Jan-15_14-30-22.jpg");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCaptureFile_PathTraversal_Returns400()
    {
        var client = CreateClientWithCaptureDir(_tempDir);

        var response = await client.GetAsync("/api/captures/..%2Fetc%2Fpasswd");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCaptureFile_WrongExtension_Returns400()
    {
        var client = CreateClientWithCaptureDir(_tempDir);

        var response = await client.GetAsync("/api/captures/test.png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCaptureFile_ValidFile_Returns200WithJpegContentType()
    {
        // Create a test JPEG file in the temp directory
        var filename = "2025-Jan-15_14-30-22.jpg";
        var filePath = Path.Combine(_tempDir, filename);
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        await File.WriteAllBytesAsync(filePath, jpegBytes);

        var client = CreateClientWithCaptureDir(_tempDir);

        var response = await client.GetAsync($"/api/captures/{filename}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(jpegBytes, content);
    }
}
