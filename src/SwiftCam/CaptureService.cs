namespace SwiftCam;

/// <summary>
/// Provides manual capture functionality by subscribing to the frame broadcaster,
/// waiting for the next available frame, and saving it to disk.
/// </summary>
public static class CaptureService
{
    /// <summary>
    /// Captures a single frame from the broadcaster and saves it to disk.
    /// Returns the filename (not full path) of the saved capture.
    /// </summary>
    /// <param name="broadcaster">The frame broadcaster to subscribe to.</param>
    /// <param name="captureDirectory">Directory where captures are saved.</param>
    /// <param name="timeProvider">Provides the current UTC timestamp for filename generation.</param>
    /// <param name="timeout">Maximum time to wait for a frame before throwing TimeoutException.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The filename of the saved capture.</returns>
    /// <exception cref="TimeoutException">Thrown if no frame arrives within the timeout.</exception>
    /// <exception cref="IOException">Thrown if the file cannot be written.</exception>
    public static async Task<string> CaptureFrameAsync(
        IFrameBroadcaster broadcaster,
        string captureDirectory,
        TimeProvider timeProvider,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        IFrameSubscription subscription = broadcaster.Subscribe();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            byte[] frameData;
            try
            {
                frameData = await subscription.WaitForFrameAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException("No camera frame available within the configured timeout.");
            }

            var timestamp = timeProvider.GetUtcNow().UtcDateTime;
            var filename = GenerateUniqueFilename(timestamp, captureDirectory);

            var fullPath = Path.Combine(captureDirectory, filename);
            Directory.CreateDirectory(captureDirectory);
            await File.WriteAllBytesAsync(fullPath, frameData, ct);

            return filename;
        }
        finally
        {
            subscription.Dispose();
        }
    }

    /// <summary>
    /// Generates a unique filename for the given timestamp by appending
    /// a numeric suffix (_1, _2, ...) if the base filename already exists
    /// in the capture directory.
    /// </summary>
    /// <param name="timestamp">The timestamp to generate the filename from.</param>
    /// <param name="captureDirectory">The directory to check for existing files.</param>
    /// <returns>A unique filename (not full path).</returns>
    public static string GenerateUniqueFilename(DateTime timestamp, string captureDirectory)
    {
        var baseFilename = CaptureWriter.GenerateFilename(timestamp);

        if (!File.Exists(Path.Combine(captureDirectory, baseFilename)))
        {
            return baseFilename;
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(baseFilename);
        var extension = Path.GetExtension(baseFilename);

        int suffix = 1;
        while (true)
        {
            var candidate = $"{nameWithoutExtension}_{suffix}{extension}";
            if (!File.Exists(Path.Combine(captureDirectory, candidate)))
            {
                return candidate;
            }
            suffix++;
        }
    }
}
