using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Time.Testing;

namespace SwiftCam.Tests.Unit;

// Feature: capture-management, Property 1: Capture save round-trip

/// <summary>
/// Property-based test verifying capture frame round-trip integrity through CaptureService.
/// For any non-empty byte array representing JPEG frame data, if CaptureFrameAsync completes
/// successfully, reading the saved file from the capture directory yields byte content
/// identical to the original frame data.
///
/// **Validates: Requirements 1.1**
/// </summary>
public class CaptureServiceRoundTripPropertyTests
{
    /// <summary>
    /// Property 1: Capture save round-trip
    /// For any valid byte array representing JPEG frame data, calling CaptureFrameAsync
    /// with a mocked IFrameBroadcaster that returns the data should produce a file on disk
    /// whose contents are byte-for-byte identical to the original frame data.
    ///
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CaptureFrameAsync_SavesFrameData_ByteForByteIdentical()
    {
        return Prop.ForAll(
            Arb.From(Gen.ArrayOf(Gen.Choose(0, 255).Select(i => (byte)i))
                .Where(arr => arr.Length > 0)),
            frameData =>
            {
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                try
                {
                    var mockSubscription = new MockFrameSubscription(frameData);
                    var mockBroadcaster = new MockFrameBroadcaster(mockSubscription);

                    var timeProvider = new FakeTimeProvider(
                        new DateTimeOffset(2025, 6, 15, 14, 30, 22, TimeSpan.Zero));

                    var filename = CaptureService.CaptureFrameAsync(
                        mockBroadcaster,
                        tempDir,
                        timeProvider,
                        TimeSpan.FromSeconds(5))
                        .GetAwaiter().GetResult();

                    var savedFilePath = Path.Combine(tempDir, filename);
                    var readBack = File.ReadAllBytes(savedFilePath);

                    return frameData.SequenceEqual(readBack)
                        .Label($"Written {frameData.Length} bytes but read back {readBack.Length} bytes");
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
            });
    }

    private class MockFrameSubscription : IFrameSubscription
    {
        private readonly byte[] _frameData;

        public MockFrameSubscription(byte[] frameData)
        {
            _frameData = frameData;
        }

        public ValueTask<byte[]> WaitForFrameAsync(CancellationToken ct)
        {
            return new ValueTask<byte[]>(_frameData);
        }

        public void Dispose() { }
    }

    private class MockFrameBroadcaster : IFrameBroadcaster
    {
        private readonly IFrameSubscription _subscription;

        public MockFrameBroadcaster(IFrameSubscription subscription)
        {
            _subscription = subscription;
        }

        public IFrameSubscription Subscribe() => _subscription;

        public void PublishFrame(byte[] jpegData) { }

        public int ClientCount => 1;
    }
}
