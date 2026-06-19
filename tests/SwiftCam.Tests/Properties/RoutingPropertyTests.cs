using System.Net;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using SwiftCam;
using Xunit;

namespace SwiftCam.Tests.Properties;

/// <summary>
/// Property-based tests for routing behavior.
/// </summary>
public class RoutingPropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RoutingPropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Property 1: Unknown paths return 404.
    /// For any HTTP GET request path that is not "/" and not "/stream",
    /// the Web Server shall respond with HTTP status 404 and a plain-text body.
    /// Validates: Requirements 2.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnknownPaths_Return404WithPlainTextBody()
    {
        return Prop.ForAll(
            UnknownPathArbitrary(),
            path =>
            {
                using var client = _factory.CreateClient();

                var response = client.GetAsync(path).GetAwaiter().GetResult();
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var contentType = response.Content.Headers.ContentType?.MediaType;

                var is404 = response.StatusCode == HttpStatusCode.NotFound;
                var isPlainText = contentType == "text/plain";
                var bodyContainsNotFound = body.Contains("Not Found");

                return (is404 && isPlainText && bodyContainsNotFound)
                    .Label($"path=\"{path}\", status={response.StatusCode}, " +
                           $"contentType=\"{contentType}\", body=\"{body}\"");
            });
    }

    /// <summary>
    /// Property 3: Non-GET methods on /stream return 405.
    /// For any HTTP method other than GET (POST, PUT, DELETE, PATCH),
    /// sending a request to the "/stream" path shall return HTTP status 405
    /// without initiating a stream.
    /// Validates: Requirements 4.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonGetMethodsOnStream_Return405()
    {
        return Prop.ForAll(
            NonGetMethodArbitrary(),
            method =>
            {
                using var client = _factory.CreateClient();

                var request = new HttpRequestMessage(new HttpMethod(method), "/stream");
                var response = client.SendAsync(request).GetAwaiter().GetResult();

                var is405 = response.StatusCode == HttpStatusCode.MethodNotAllowed;
                var contentType = response.Content.Headers.ContentType?.MediaType;
                var isNotStream = contentType != "multipart/x-mixed-replace";

                return (is405 && isNotStream)
                    .Label($"method={method}, status={response.StatusCode}, " +
                           $"contentType=\"{contentType}\"");
            });
    }

    /// <summary>
    /// Creates an Arbitrary that generates random HTTP methods from
    /// {POST, PUT, DELETE, PATCH} (non-GET methods).
    /// </summary>
    private static Arbitrary<string> NonGetMethodArbitrary()
    {
        var methods = new[] { "POST", "PUT", "DELETE", "PATCH" };
        return Gen.Elements(methods).ToArbitrary();
    }

    /// <summary>
    /// Creates an Arbitrary that generates random valid URL paths
    /// excluding "/" and "/stream".
    /// </summary>
    private static Arbitrary<string> UnknownPathArbitrary()
    {
        // Valid path segment characters (subset safe for URLs)
        var segmentChars = "abcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();

        var segmentGen = Gen.Choose(1, 12)
            .SelectMany(len =>
                Gen.ArrayOf(len, Gen.Elements(segmentChars))
                    .Select(chars => new string(chars)));

        var pathGen = Gen.Choose(1, 4)
            .SelectMany(segmentCount =>
                Gen.ArrayOf(segmentCount, segmentGen)
                    .Select(segments => "/" + string.Join("/", segments)))
            .Where(p => p != "/" && p != "/stream");

        return pathGen.ToArbitrary();
    }
}
