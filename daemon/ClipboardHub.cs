using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Clipwell.Daemon;

/// <summary>
/// Fan-out for live clipboard events. WebSocket and SSE endpoints each subscribe
/// and receive every broadcast payload. Subscribers that fall behind are dropped
/// rather than allowed to back-pressure the watcher.
/// </summary>
public sealed class ClipboardHub
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    public int SubscriberCount => _subscribers.Count;

    public (Guid Id, ChannelReader<string> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        // Bounded + DropOldest: a slow client loses stale events, never stalls others.
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        _subscribers[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }

    public void Broadcast(string payload)
    {
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryWrite(payload);
    }
}
