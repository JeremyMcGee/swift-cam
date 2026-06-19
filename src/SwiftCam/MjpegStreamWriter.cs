using System.Text;

namespace SwiftCam;

/// <summary>
/// Writes a multipart MJPEG stream to an HTTP response, delivering frames
/// from a broadcaster subscription until the client disconnects.
/// </summary>
public static class MjpegStreamWriter
{
    private const string Boundary = "frame";
    private static readonly byte[] BoundaryBytes = Encoding.ASCII.GetBytes($"--{Boundary}\r\n");
    private static readonly byte[] ContentTypeBytes = Encoding.ASCII.GetBytes("Content-Type: image/jpeg\r\n");
    private static readonly byte[] TrailingNewline = Encoding.ASCII.GetBytes("\r\n");

    /// <summary>
    /// Streams MJPEG frames to the client until cancellation (client disconnect).
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="broadcaster">The frame broadcaster to subscribe to.</param>
    /// <param name="logger">Logger for connection/disconnection events.</param>
    /// <param name="ct">Cancellation token triggered on client disconnect.</param>
    public static async Task WriteStreamAsync(
        HttpContext context,
        IFrameBroadcaster broadcaster,
        ILogger logger,
        CancellationToken ct)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        context.Response.ContentType = $"multipart/x-mixed-replace; boundary={Boundary}";
        context.Response.Headers["Cache-Control"] = "no-cache";

        var subscription = broadcaster.Subscribe();
        try
        {
            logger.LogInformation("Client connected: {ClientIp}", clientIp);

            var body = context.Response.Body;

            while (!ct.IsCancellationRequested)
            {
                var frame = await subscription.WaitForFrameAsync(ct);

                // Write boundary
                await body.WriteAsync(BoundaryBytes, ct);

                // Write Content-Type header
                await body.WriteAsync(ContentTypeBytes, ct);

                // Write Content-Length header
                var contentLengthBytes = Encoding.ASCII.GetBytes($"Content-Length: {frame.Length}\r\n");
                await body.WriteAsync(contentLengthBytes, ct);

                // Write blank line separating headers from body
                await body.WriteAsync(TrailingNewline, ct);

                // Write JPEG data
                await body.WriteAsync(frame, ct);

                // Write trailing \r\n after JPEG data
                await body.WriteAsync(TrailingNewline, ct);

                await body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — expected, exit cleanly
        }
        finally
        {
            subscription.Dispose();
            logger.LogInformation("Client disconnected: {ClientIp}", clientIp);
        }
    }
}
