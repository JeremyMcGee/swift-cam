using System.Net;
using System.Net.Http;
using Xunit;

namespace SwiftCam.Tests.Unit;

public class HtmlPageTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HtmlPageTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRoot_ReturnsHttp200()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRoot_ReturnsContentTypeTextHtml()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetRoot_ContainsDoctypeHtmlDeclaration()
    {
        var response = await _client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("<!DOCTYPE html>", body);
    }

    [Fact]
    public async Task GetRoot_ContainsTitleRaspberryPiCamera()
    {
        var response = await _client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>Raspberry Pi Camera</title>", body);
    }

    [Fact]
    public async Task GetRoot_ContainsImgElementWithStreamSrc()
    {
        var response = await _client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("<img", body);
        Assert.Contains("src=\"/stream\"", body);
    }
}
