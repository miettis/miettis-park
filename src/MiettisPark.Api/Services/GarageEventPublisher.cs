using System.Collections.Concurrent;
using System.Threading.Channels;

namespace MiettisPark.Api.Services;

public sealed record GarageChanged(int GarageId, string Kind);

public interface IGarageEventPublisher
{
    void Publish(GarageChanged change);
}

public sealed class GarageEventPublisher : IGarageEventPublisher
{
    public const int BufferCapacity = 64;
    private readonly ConcurrentDictionary<Subscription, byte> subscriptions = new();
    public int SubscriberCount => subscriptions.Count;

    public Subscription Subscribe()
    {
        var subscription = new Subscription(this);
        subscriptions.TryAdd(subscription, 0);
        return subscription;
    }

    public void Publish(GarageChanged change)
    {
        foreach (var subscription in subscriptions.Keys)
        {
            if (!subscription.Channel.Writer.TryWrite(change))
            {
                subscription.Dispose();
            }
        }
    }

    public sealed class Subscription : IDisposable
    {
        private readonly GarageEventPublisher owner;
        private readonly CancellationTokenSource stopped = new();
        private int disposed;
        internal Channel<GarageChanged> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<GarageChanged>(
            new BoundedChannelOptions(BufferCapacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
        public ChannelReader<GarageChanged> Reader => Channel.Reader;
        public CancellationToken Stopped => stopped.Token;
        internal Subscription(GarageEventPublisher owner) => this.owner = owner;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            owner.subscriptions.TryRemove(this, out _);
            Channel.Writer.TryComplete();
            stopped.Cancel();
        }
    }
}
