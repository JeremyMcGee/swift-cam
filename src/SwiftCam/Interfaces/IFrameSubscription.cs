namespace SwiftCam;

/// <summary>
/// Represents a client's subscription to the frame stream.
/// Disposing the subscription unregisters the client from the broadcaster.
/// </summary>
public interface IFrameSubscription : IDisposable
{
    /// <summary>
    /// Waits for the next available JPEG frame.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the wait (e.g., on client disconnect).</param>
    /// <returns>The raw JPEG image data of the next frame.</returns>
    ValueTask<byte[]> WaitForFrameAsync(CancellationToken ct);
}
