using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SwiftCam;

namespace SwiftCam.Tests.Properties;

/// <summary>
/// Property-based tests for MJPEG frame format integrity.
/// </summary>
public class MjpegFormatPropertyTests
{
    /// <summary>
    /// Property 2: MJPEG frame format integrity.
    /// For any byte array representing JPEG image data, formatting it as an MJPEG
    /// multipart part shall produce output containing the boundary marker "--frame",
    /// a Content-Type header of "image/jpeg", a Content-Length header whose numeric
    /// value equals the byte array length, and the original byte data unchanged.
    /// Validates: Requirements 4.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MjpegFrame_ContainsCorrectBoundaryHeadersAndData()
    {
        return Prop.ForAll(
            FrameDataArbitrary(),
            frameData =>
            {
                // Arrange
                var broadcaster = new FrameBroadcaster();
                var memoryStream = new MemoryStream();

                var httpContext = new DefaultHttpContext();
                httpContext.Response.Body = memoryStream;

                var logger = NullLogger.Instance;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                // Start the writer in a background task — it will subscribe internally
                var writeTask = Task.Run(async () =>
                {
                    await MjpegStreamWriter.WriteStreamAsync(httpContext, broadcaster, logger, cts.Token);
                });

                // Wait until the writer has subscribed (ClientCount becomes 1)
                SpinWait.SpinUntil(() => broadcaster.ClientCount >= 1, TimeSpan.FromSeconds(2));

                // Publish the frame
                broadcaster.PublishFrame(frameData);

                // Wait for the output to appear in the memory stream
                SpinWait.SpinUntil(() => memoryStream.Position > 0, TimeSpan.FromSeconds(2));

                // Give a brief moment for flush to complete
                Thread.Sleep(50);

                // Cancel to stop the writer
                cts.Cancel();

                try
                {
                    writeTask.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) { }
                catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException)) { }

                // Get the bytes written to the stream
                var output = memoryStream.ToArray();
                var outputStr = Encoding.ASCII.GetString(output);

                // Assert: contains boundary "--frame"
                var containsBoundary = outputStr.Contains("--frame\r\n");

                // Assert: contains Content-Type header
                var containsContentType = outputStr.Contains("Content-Type: image/jpeg\r\n");

                // Assert: contains correct Content-Length header
                var expectedContentLength = $"Content-Length: {frameData.Length}\r\n";
                var containsContentLength = outputStr.Contains(expectedContentLength);

                // Assert: original bytes are present unchanged in output
                var containsOriginalBytes = ContainsSubsequence(output, frameData);

                return (containsBoundary && containsContentType && containsContentLength && containsOriginalBytes)
                    .Label($"boundary={containsBoundary}, contentType={containsContentType}, " +
                           $"contentLength={containsContentLength}, originalBytes={containsOriginalBytes}, " +
                           $"outputLen={output.Length}");
            });
    }

    /// <summary>
    /// Creates an Arbitrary that generates non-null byte arrays from 1 byte to 500KB.
    /// </summary>
    private static Arbitrary<byte[]> FrameDataArbitrary()
    {
        var gen = Gen.Choose(1, 500 * 1024)
            .SelectMany(size => Gen.ArrayOf(size, Gen.Choose(0, 255).Select(i => (byte)i)));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Checks if the haystack byte array contains the needle byte array as a contiguous subsequence.
    /// </summary>
    private static bool ContainsSubsequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0) return true;
        if (haystack.Length < needle.Length) return false;

        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return true;
        }
        return false;
    }
}
