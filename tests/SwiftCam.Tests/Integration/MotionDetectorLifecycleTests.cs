using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace SwiftCam.Tests.Integration;

public class MotionDetectorLifecycleTests
{
    [Fact]
    public async Task StartAsync_SubscribesToBroadcaster()
    {
        // Arrange
        var broadcaster = new FakeFrameBroadcaster();
        var detector = CreateDetector(broadcaster);

        // Act
        await detector.StartAsync(CancellationToken.None);

        // Wait for Subscribe to be called (signal-based, no arbitrary delay)
        var subscribed = await broadcaster.SubscribedSignal.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(subscribed, "Subscribe was not called within timeout");
        Assert.Equal(1, broadcaster.SubscribeCallCount);

        // Cleanup
        await detector.StopAsync(CancellationToken.None);
        detector.Dispose();
    }

    [Fact]
    public async Task StopAsync_DisposesSubscription()
    {
        // Arrange
        var broadcaster = new FakeFrameBroadcaster();
        var detector = CreateDetector(broadcaster);

        await detector.StartAsync(CancellationToken.None);

        // Wait for Subscribe to be called
        var subscribed = await broadcaster.SubscribedSignal.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(subscribed, "Subscribe was not called within timeout");

        // Act
        await detector.StopAsync(CancellationToken.None);

        // Assert
        var subscription = broadcaster.LastSubscription;
        Assert.NotNull(subscription);
        Assert.True(subscription.DisposeWasCalled);

        // Cleanup
        detector.Dispose();
    }

    private static MotionDetector CreateDetector(IFrameBroadcaster broadcaster)
    {
        var options = Options.Create(new MotionSettings());
        var logger = NullLogger<MotionDetector>.Instance;
        return new MotionDetector(options, broadcaster, logger, TimeProvider.System);
    }

    /// <summary>
    /// Fake broadcaster that tracks Subscribe calls and signals when subscribed.
    /// </summary>
    private sealed class FakeFrameBroadcaster : IFrameBroadcaster
    {
        public int SubscribeCallCount { get; private set; }
        public FakeFrameSubscription? LastSubscription { get; private set; }
        public SemaphoreSlim SubscribedSignal { get; } = new(0, 1);
        public int ClientCount => 0;

        public void PublishFrame(byte[] jpegData) { }

        public IFrameSubscription Subscribe()
        {
            SubscribeCallCount++;
            LastSubscription = new FakeFrameSubscription();
            SubscribedSignal.Release();
            return LastSubscription;
        }
    }

    /// <summary>
    /// Fake subscription that blocks on WaitForFrameAsync until cancelled,
    /// and tracks whether Dispose was called.
    /// </summary>
    private sealed class FakeFrameSubscription : IFrameSubscription
    {
        public bool DisposeWasCalled { get; private set; }

        public ValueTask<byte[]> WaitForFrameAsync(CancellationToken ct)
        {
            // Throw OperationCanceledException when the token is triggered,
            // mimicking real subscription behavior on service stop.
            ct.ThrowIfCancellationRequested();

            // Return a task that completes only when cancelled
            var tcs = new TaskCompletionSource<byte[]>();
            ct.Register(() => tcs.TrySetCanceled(ct));
            return new ValueTask<byte[]>(tcs.Task);
        }

        public void Dispose()
        {
            DisposeWasCalled = true;
        }
    }
}
