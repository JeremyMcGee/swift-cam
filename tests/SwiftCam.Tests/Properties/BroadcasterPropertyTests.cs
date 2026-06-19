using FsCheck;
using FsCheck.Xunit;
using SwiftCam;

namespace SwiftCam.Tests.Properties;

/// <summary>
/// Property-based tests for the FrameBroadcaster component.
/// </summary>
public class BroadcasterPropertyTests
{
    /// <summary>
    /// Property 4: Frame broadcast delivers to all subscribers.
    /// For any set of active subscribers (1 to 10), when a frame is published
    /// to the broadcaster, every subscriber shall receive that frame.
    /// Validates: Requirements 5.1, 5.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FrameBroadcast_DeliversToAllSubscribers()
    {
        return Prop.ForAll(
            Gen.Choose(1, 10).ToArbitrary(),
            Arb.From<NonEmptyArray<byte>>(),
            (subscriberCount, frameData) =>
            {
                var broadcaster = new FrameBroadcaster();
                var subscriptions = new List<IFrameSubscription>();

                try
                {
                    // Subscribe N clients
                    for (int i = 0; i < subscriberCount; i++)
                    {
                        subscriptions.Add(broadcaster.Subscribe());
                    }

                    // Publish a frame
                    var frame = frameData.Get;
                    broadcaster.PublishFrame(frame);

                    // Verify all subscribers receive the frame
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                    foreach (var subscription in subscriptions)
                    {
                        var received = subscription.WaitForFrameAsync(cts.Token)
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();

                        if (!received.SequenceEqual(frame))
                            return false;
                    }

                    return true;
                }
                finally
                {
                    foreach (var sub in subscriptions)
                    {
                        sub.Dispose();
                    }
                }
            });
    }

    /// <summary>
    /// Property 5: Slow subscriber isolation.
    /// For any set of subscribers where one subscriber is not consuming frames,
    /// publishing frames beyond the channel capacity shall still deliver the latest
    /// frames to all other (fast) subscribers without blocking or dropping their frames.
    /// Validates: Requirements 5.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SlowSubscriber_DoesNotBlockFastSubscribers()
    {
        return Prop.ForAll(
            Gen.Choose(1, 9).ToArbitrary(),
            Gen.Choose(5, 10).ToArbitrary(),
            (fastSubscriberCount, frameCount) =>
            {
                var broadcaster = new FrameBroadcaster();
                var fastSubscriptions = new List<IFrameSubscription>();
                IFrameSubscription? slowSubscription = null;

                try
                {
                    // Subscribe one "slow" client that never reads
                    slowSubscription = broadcaster.Subscribe();

                    // Subscribe N "fast" clients
                    for (int i = 0; i < fastSubscriberCount; i++)
                    {
                        fastSubscriptions.Add(broadcaster.Subscribe());
                    }

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                    // Publish frames one at a time. After each publish,
                    // fast subscribers immediately read. The slow subscriber never reads.
                    // This proves the slow subscriber doesn't block publishing or delivery.
                    for (int i = 0; i < frameCount; i++)
                    {
                        var frame = new byte[] { (byte)(i + 1), 0xAA, 0xBB };
                        broadcaster.PublishFrame(frame);

                        // Each fast subscriber reads the frame immediately
                        foreach (var fastSub in fastSubscriptions)
                        {
                            var received = fastSub.WaitForFrameAsync(cts.Token)
                                .AsTask()
                                .GetAwaiter()
                                .GetResult();

                            if (!received.SequenceEqual(frame))
                                return false;
                        }
                    }

                    // If we got here, all M frames (M > channel capacity) were
                    // delivered to fast subscribers without blocking, despite the
                    // slow subscriber never consuming any frames.
                    return true;
                }
                finally
                {
                    foreach (var sub in fastSubscriptions)
                    {
                        sub.Dispose();
                    }
                    slowSubscription?.Dispose();
                }
            });
    }
}
