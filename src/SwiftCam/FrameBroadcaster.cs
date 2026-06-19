using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SwiftCam;

/// <summary>
/// Distributes captured frames to all registered client subscriptions using bounded channels.
/// Each subscriber gets an independent channel so that a slow consumer does not block others.
/// </summary>
public class FrameBroadcaster : IFrameBroadcaster
{
    private const int MaxSubscribers = 10;
    private const int ChannelCapacity = 3;

    private readonly ConcurrentDictionary<Guid, Channel<byte[]>> _subscribers = new();

    /// <inheritdoc />
    public int ClientCount => _subscribers.Count;

    /// <inheritdoc />
    public void PublishFrame(byte[] jpegData)
    {
        foreach (var kvp in _subscribers)
        {
            // TryWrite is non-blocking. With DropOldest policy, it will always succeed
            // by discarding the oldest item if the channel is full.
            kvp.Value.Writer.TryWrite(jpegData);
        }
    }

    /// <inheritdoc />
    public IFrameSubscription Subscribe()
    {
        if (_subscribers.Count >= MaxSubscribers)
        {
            throw new MaxClientsExceededException();
        }

        var id = Guid.NewGuid();

        var options = new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true
        };

        var channel = Channel.CreateBounded<byte[]>(options);

        if (!_subscribers.TryAdd(id, channel))
        {
            // Extremely unlikely with Guid keys, but handle defensively
            throw new InvalidOperationException("Failed to register subscriber.");
        }

        return new FrameSubscription(id, channel.Reader, this);
    }

    private void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Represents a single client's subscription to the frame stream.
    /// Disposing unregisters the client from the broadcaster.
    /// </summary>
    private sealed class FrameSubscription : IFrameSubscription
    {
        private readonly Guid _id;
        private readonly ChannelReader<byte[]> _reader;
        private readonly FrameBroadcaster _broadcaster;
        private bool _disposed;

        public FrameSubscription(Guid id, ChannelReader<byte[]> reader, FrameBroadcaster broadcaster)
        {
            _id = id;
            _reader = reader;
            _broadcaster = broadcaster;
        }

        /// <inheritdoc />
        public async ValueTask<byte[]> WaitForFrameAsync(CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return await _reader.ReadAsync(ct);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _broadcaster.Unsubscribe(_id);
        }
    }
}
