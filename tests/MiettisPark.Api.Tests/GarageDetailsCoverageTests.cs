using Microsoft.EntityFrameworkCore;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class GarageDetailsCoverageTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [Theory]
    [InlineData("negative")]
    [InlineData("precision")]
    [InlineData("zero-unit")]
    [InlineData("gap")]
    [InlineData("overlap")]
    [InlineData("closed-last")]
    [InlineData("null-tier")]
    [InlineData("too-many")]
    public async Task InvalidPricingNeverChangesPersistedValues(string scenario)
    {
        using var store = new TestStore();
        var original = await store.Management.GetPricingAsync(1, Token);
        var tiers = DefaultParkingFees.Create();
        switch (scenario)
        {
            case "negative":
                tiers[0].Amount = -1m;
                break;
            case "precision":
                tiers[0].Amount = 0.001m;
                break;
            case "zero-unit":
                tiers[0].PerUnit = TimeSpan.Zero;
                break;
            case "gap":
                tiers[1].StartDuration += TimeSpan.FromMinutes(1);
                break;
            case "overlap":
                tiers[1].StartDuration -= TimeSpan.FromMinutes(1);
                break;
            case "closed-last":
                tiers[1].EndDuration = TimeSpan.FromHours(4);
                break;
            case "null-tier":
                tiers[0] = null!;
                break;
            case "too-many":
                tiers = Enumerable.Range(0, 21).Select(_ => new ParkingFeeTier()).ToList();
                break;
        }

        var error = await Assert.ThrowsAsync<ParkingError>(() =>
            store.Management.SavePricingAsync(1, new PricingRequest(tiers, original.Tiers), Token));
        Assert.Equal("INVALID_PRICING", error.Code);
        var unchanged = await store.Management.GetPricingAsync(1, Token);
        Assert.Equal(0.50m, unchanged.Tiers[0].Amount);
        Assert.Equal(0.30m, unchanged.Tiers[1].Amount);
    }

    [Fact]
    public async Task BrowserDecimalScaleDoesNotCauseFalseConflict()
    {
        using var store = new TestStore();
        var expected = DefaultParkingFees.Create();
        expected[0].Amount = 0.5m;
        expected[1].Amount = 0.3m;
        var tiers = DefaultParkingFees.Create();
        tiers[0].Amount = 1m;
        await store.Management.SavePricingAsync(1, new PricingRequest(tiers, expected), Token);
        Assert.Equal(1m, (await store.Management.GetPricingAsync(1, Token)).Tiers[0].Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPricingSaveOrCommitRollsBackAndPublishesNothing(bool commitFailure)
    {
        using var store = new TestStore();
        var publisher = new GarageEventPublisher();
        using var subscription = publisher.Subscribe();
        var management = new GarageManagementService(store.Database, store.Transactions, publisher);
        var original = await management.GetPricingAsync(1, Token);
        var tiers = DefaultParkingFees.Create();
        tiers[0].Amount = 2m;
        if (commitFailure)
        {
            store.Transactions.CommitFailure = new InvalidOperationException("Injected failure");
        }
        else
        {
            store.Probe.FailAt = store.Probe.Calls + 1;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            management.SavePricingAsync(1, new PricingRequest(tiers, original.Tiers), Token));
        Assert.False(subscription.Reader.TryRead(out _));
        Assert.Equal(0.50m, (await management.GetPricingAsync(1, Token)).Tiers[0].Amount);
    }

    [Fact]
    public async Task ConcurrentPricingUpdatesRejectOneStaleBaseline()
    {
        using var store = new TestStore();
        var baseline = await store.Management.GetPricingAsync(1, Token);
        await using var leftDatabase = store.NewContext();
        await using var rightDatabase = store.NewContext();
        var left = new GarageManagementService(leftDatabase, new TestTransactions(leftDatabase, store.Coordinator));
        var right = new GarageManagementService(rightDatabase, new TestTransactions(rightDatabase, store.Coordinator));
        var first = DefaultParkingFees.Create();
        var second = DefaultParkingFees.Create();
        first[0].Amount = 1m;
        second[0].Amount = 2m;
        store.Coordinator.ArmPair();
        async Task<string> Submit(GarageManagementService management, List<ParkingFeeTier> tiers)
        {
            try
            {
                await management.SavePricingAsync(1, new PricingRequest(tiers, baseline.Tiers), Token);
                return "OK";
            }
            catch (ParkingError error)
            {
                return error.Code;
            }
        }

        var outcomes = await Task.WhenAll(Submit(left, first), Submit(right, second));
        Assert.Single(outcomes, outcome => outcome == "OK");
        Assert.Single(outcomes, outcome => outcome == "PRICING_CONFLICT");
    }

    [Fact]
    public async Task HistoryIsGarageScopedOrderedDeterministicallyAndPaged()
    {
        using var store = new TestStore();
        var other = await store.Management.CreateAsync(new GarageRequest("Other", "Street 1", 60, 24), Token);
        var start = store.Clock.Now;
        for (var index = 0; index < 4; index++)
        {
            store.Database.Visits.Add(new Visit
            {
                ParkingGarageId = 1,
                LicenseNumber = $"CAR-{index}",
                StartAt = start,
                EndAt = start.AddHours(1),
                ClosedAt = start.AddHours(1),
                Status = VisitStatus.Completed,
                Cost = index
            });
        }

        store.Database.Visits.Add(new Visit
        {
            ParkingGarageId = other.Id,
            LicenseNumber = "OTHER-GARAGE",
            StartAt = start.AddDays(1)
        });
        await store.Database.SaveChangesAsync();
        var first = await store.Management.GetVisitHistoryAsync(1, 0, 2, Token);
        var second = await store.Management.GetVisitHistoryAsync(1, 2, 2, Token);
        Assert.Equal(new[] { "CAR-3", "CAR-2" }, first.Select(visit => visit.LicenseNumber));
        Assert.Equal(new[] { "CAR-1", "CAR-0" }, second.Select(visit => visit.LicenseNumber));
        Assert.Equal(4, first.Concat(second).Select(visit => visit.VisitId).Distinct().Count());
        Assert.Single(await store.Management.GetVisitHistoryAsync(other.Id, 0, 50, Token));
        Assert.Equal(5, await store.Database.Visits.CountAsync());
    }
}
