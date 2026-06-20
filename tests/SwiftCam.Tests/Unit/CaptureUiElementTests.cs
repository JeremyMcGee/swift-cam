using System.Net;
using Xunit;

namespace SwiftCam.Tests.Unit;

public class CaptureUiElementTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CaptureUiElementTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRoot_ContainsTakeCaptureButtonInGalleryHeader()
    {
        var response = await _client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("gallery-header", body);
        Assert.Contains("id=\"take-capture-btn\"", body);
        Assert.Contains("Take Capture", body);
    }

    [Fact]
    public async Task GetRoot_ContainsDeleteButtonClassInThumbnailStyles()
    {
        var response = await _client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The delete-btn class is defined in styles and used dynamically on thumbnails
        Assert.Contains("delete-btn", body);
    }

    [Fact]
    public async Task GetRoot_ContainsDeleteConfirmationDialog()
    {
        var response = await _client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"delete-confirm-dialog\"", body);
        Assert.Contains("dialog-box", body);
        Assert.Contains("delete-dialog-filename", body);
    }
}
