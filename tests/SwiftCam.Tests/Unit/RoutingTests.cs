using System.Net;
using System.Net.Http;
using Xunit;

namespace SwiftCam.Tests.Unit;

public class RoutingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RoutingTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetNonexistent_Returns404()
    {
        var response = await _client.GetAsync("/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetNestedUnknownPath_Returns404()
    {
        var response = await _client.GetAsync("/foo/bar");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NotFound_ReturnsContentTypePlainText()
    {
        var response = await _client.GetAsync("/nonexistent");

        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task NotFound_BodyContainsNotFound()
    {
        var response = await _client.GetAsync("/nonexistent");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Not Found", body);
    }

    [Fact]
    public async Task PostStream_Returns405()
    {
        var response = await _client.PostAsync("/stream", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task PutStream_Returns405()
    {
        var response = await _client.PutAsync("/stream", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task DeleteStream_Returns405()
    {
        var response = await _client.DeleteAsync("/stream");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
