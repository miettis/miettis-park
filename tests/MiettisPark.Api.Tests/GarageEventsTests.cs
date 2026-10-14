using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Controllers;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;

namespace MiettisPark.Api.Tests;

public sealed class GarageEventsTests
{
    [Fact]
    public void PublisherFansOutAndDisconnectsOverflowWithoutBlockingOthers()
    {
        var publisher = new GarageEventPublisher();
        using var slow = publisher.Subscribe();
        using var fast = publisher.Subscribe();
        for (var index = 0; index <= GarageEventPublisher.BufferCapacity; index++)
        {
            var change = new GarageChanged(index + 1, "occupancy");
            publisher.Publish(change);
            Assert.True(fast.Reader.TryRead(out var received));
            Assert.Equal(change, received);
        }

        Assert.True(slow.Stopped.IsCancellationRequested);
        Assert.Equal(1, publisher.SubscriberCount);
        fast.Dispose();
        Assert.Equal(0, publisher.SubscriberCount);
    }

    [Fact]
    public async Task ServicesPublishOnlyAfterCommitAndDisposeAndKeepVisitIndependent()
    {
        using var store = new TestStore();
        var publisher = new ProbePublisher(store.Transactions);
        var service = new ParkingService(store.Database, store.Clock, new ParkingFeeCalculator(), store.Transactions,
            Microsoft.Extensions.Options.Options.Create(store.Options), publisher);
        var management = new GarageManagementService(store.Database, store.Transactions, publisher);
        var garage = await management.CreateAsync(new GarageRequest("Second", "Street", 0, 0), default);
        Assert.Equal(new GarageChanged(garage.Id, "garage"), publisher.Changes.Last());
        await management.UpdateAsync(garage.Id, new GarageRequest("Changed", "Street", 0, 0), default);
        var entry = await service.EnterGarageAsync(garage.Id, "SAME", default);
        var before = await service.GetAvailabilityAsync(garage.Id, default);
        Assert.Equal(0, before.Capacity);
        Assert.True(before.IsFull);
        await service.LeaveGarageAsync(garage.Id, "SAME", default);
        Assert.Equal(0, (await service.GetAvailabilityAsync(garage.Id, default)).OccupiedLots);
        Assert.All(publisher.Changes.Take(2), change => Assert.Equal("garage", change.Kind));
        Assert.All(publisher.Changes.Skip(2), change => Assert.Equal("occupancy", change.Kind));
        Assert.True(entry.VisitId > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSaveOrCommitAndValidationNeverPublish(bool commitFailure)
    {
        using var store = new TestStore();
        var publisher = new ProbePublisher(store.Transactions);
        var service = new ParkingService(store.Database, store.Clock, new ParkingFeeCalculator(), store.Transactions,
            Microsoft.Extensions.Options.Options.Create(store.Options), publisher);
        if (commitFailure)
        {
            store.Transactions.CommitFailure = new InvalidOperationException("commit");
        }
        else
        {
            store.Probe.FailAt = store.Probe.Calls + 1;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnterLotAsync(1, 1, null, default));
        Assert.Empty(publisher.Changes);
        Assert.Contains("rollback", store.Transactions.Events);
        store.Transactions.CommitFailure = null;
        store.Probe.FailAt = null;
        await Assert.ThrowsAsync<ParkingError>(() => service.EnterLotAsync(1, 999999, null, default));
        Assert.Empty(publisher.Changes);
        using var fresh = store.NewContext();
        Assert.Equal(ParkingLotStatus.Free, fresh.ParkingLots.Single(lot => lot.Id == 1).CurrentStatus);
    }

    [Fact]
    public async Task ApiStreamsImmediatelyWithAnonymousInvalidationAndDisconnectCleanup()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        using var cancellation = new CancellationTokenSource();
        using var response = await client.GetAsync("/api/garages/events", HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-cache", response.Headers.CacheControl!.ToString());
        Assert.Equal("no", response.Headers.GetValues("X-Accel-Buffering").Single());
        var publisher = factory.Services.GetRequiredService<GarageEventPublisher>();
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellation.Token));
        Assert.Equal(": connected", await reader.ReadLineAsync(cancellation.Token));
        Assert.Equal("", await reader.ReadLineAsync(cancellation.Token));
        publisher.Publish(new GarageChanged(1, "layout"));
        Assert.Equal("event: garage-changed", await reader.ReadLineAsync(cancellation.Token));
        Assert.Equal("data: {\"garageId\":1,\"kind\":\"layout\"}", await reader.ReadLineAsync(cancellation.Token));
        cancellation.Cancel();
        response.Dispose();
    }

    [Fact]
    public async Task HeartbeatAndSlowWriteTimeoutUseInjectedClockAndCleanSubscription()
    {
        var publisher = new GarageEventPublisher();
        var clock = new ManualClock();
        var stream = new FrameStream();
        var context = new DefaultHttpContext();
        context.Response.Body = stream;
        using var cancellation = new CancellationTokenSource();
        var controller = new GarageEventsController(publisher, clock) { ControllerContext = new() { HttpContext = context } };
        var task = controller.Get(cancellation.Token);
        Assert.Contains(": connected", stream.Text);
        clock.Fire();
        await stream.NextWrite.Task;
        Assert.Contains(": heartbeat", stream.Text);
        stream.Block = true;
        publisher.Publish(new GarageChanged(1, "garage"));
        await stream.Blocked.Task;
        clock.Fire();
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, publisher.SubscriberCount);
    }

    [Fact]
    public async Task LayoutInvalidationAndFailedManagementCommitPreserveSnapshot()
    {
        using var store = new TestStore();
        var publisher = new ProbePublisher(store.Transactions);
        var management = new GarageManagementService(store.Database, store.Transactions, publisher);
        var original = await management.GetLayoutAsync(1, default);
        var input = new LayoutRequest(original.Version, original.Width, original.Height,
            original.Cells.Select(cell => new LayoutCellRequest(cell.X, cell.Y, cell.Type, cell.LotId,
                cell.LotId is { } id ? original.Lots.Single(lot => lot.Id == id).Number : null)).ToList(), []);
        var updated = await management.SaveLayoutAsync(1, input, default);
        Assert.Equal(new GarageChanged(1, "layout"), Assert.Single(publisher.Changes));
        await Assert.ThrowsAsync<ParkingError>(() => management.SaveLayoutAsync(1, input, default));
        Assert.Single(publisher.Changes);
        store.Transactions.CommitFailure = new InvalidOperationException("commit");
        await Assert.ThrowsAsync<InvalidOperationException>(() => management.UpdateAsync(1, new GarageRequest("Changed", null, null, null), default));
        Assert.Single(publisher.Changes);
        Assert.Equal(updated.Version, (await management.GetLayoutAsync(1, default)).Version);
    }

    [Fact]
    public async Task RequestCancellationRemovesSubscriptionAndPendingHeartbeat()
    {
        var publisher = new GarageEventPublisher();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        var controller = new GarageEventsController(publisher, new ManualClock()) { ControllerContext = new() { HttpContext = context } };
        var task = controller.Get(cancellation.Token);
        Assert.Equal(1, publisher.SubscriberCount);
        cancellation.Cancel();
        await task;
        Assert.Equal(0, publisher.SubscriberCount);
    }

    [Fact]
    public async Task ApiSummaryCountsActiveLotsAndNotVisits()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var lots = database.ParkingLots.OrderBy(lot => lot.Id).ToList();
            foreach (var lot in lots.Take(3))
            {
                lot.CurrentStatus = ParkingLotStatus.Occupied;
            }
            lots[3].CurrentStatus = ParkingLotStatus.Unknown;
            database.ParkingLots.Add(new ParkingLot { ParkingGarageId = 1, Number = 6, IsActive = false, CurrentStatus = ParkingLotStatus.Occupied });
            await database.SaveChangesAsync();
        }
        var before = (await client.GetFromJsonAsync<List<GarageDto>>("/api/garages"))!.Single();
        Assert.Equal((5, 3, 1, 1), (before.Capacity, before.OccupiedLots, before.FreeLots, before.UnknownLots));
        var entered = await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "COUNT" });
        entered.EnsureSuccessStatusCode();
        var after = (await client.GetFromJsonAsync<List<GarageDto>>("/api/garages"))!.Single();
        Assert.Equal((before.Capacity, before.OccupiedLots, before.FreeLots, before.UnknownLots), (after.Capacity, after.OccupiedLots, after.FreeLots, after.UnknownLots));
    }

    private sealed class ProbePublisher(TestTransactions transactions) : IGarageEventPublisher
    {
        public List<GarageChanged> Changes { get; } = [];
        public void Publish(GarageChanged change)
        {
            Assert.Equal("dispose", transactions.Events.Last());
            Assert.Equal("commit", transactions.Events[^2]);
            Changes.Add(change);
        }
    }

    private sealed class FrameStream : MemoryStream
    {
        public bool Block { get; set; }
        public string Text => Encoding.UTF8.GetString(ToArray());
        public TaskCompletionSource NextWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int writes;
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Block)
            {
                Blocked.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            await base.WriteAsync(buffer, cancellationToken);
            if (++writes >= 2)
            {
                NextWrite.TrySetResult();
            }
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state);
            timers.Add(timer);
            return timer;
        }

        public void Fire()
        {
            foreach (var timer in timers.ToArray())
            {
                timer.Fire();
            }
        }

        private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            private bool disposed;
            public void Fire()
            {
                if (!disposed)
                {
                    disposed = true;
                    callback(state);
                }
            }
            public bool Change(TimeSpan dueTime, TimeSpan period) => !disposed;
            public void Dispose() => disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
