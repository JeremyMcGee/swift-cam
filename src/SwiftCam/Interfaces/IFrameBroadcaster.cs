namespace SwiftCam;

/// <summary>
/// Distributes captured frames to all registered client subscriptions.
/// </summary>
public interface IFrameBroadcaster
{
    /// <summary>
    /// Publishes a JPEG frame to all active subscribers.
    /// </summary>
    /// <param name="jpegData">The raw JPEG image data.</param>
    void PublishFrame(byte[] jpegData);

    /// <summary>
    /// Creates a new subscription for receiving frames.
    /// </summary>
    /// <returns>A frame subscription that can be used to await new frames.</returns>
    IFrameSubscription Subscribe();

    /// <summary>
    /// Gets the number of currently connected clients.
    /// </summary>
    int ClientCount { get; }
}
