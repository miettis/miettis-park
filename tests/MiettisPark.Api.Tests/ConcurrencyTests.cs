using Microsoft.EntityFrameworkCore;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;
using Xunit;

namespace MiettisPark.Api.Tests;

// Both real service calls rendezvous before the test double serializes their transactions.
// These tests do not exercise PostgreSQL locks or unique constraints.
public sealed class ConcurrencyTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompetingLotCommandsHaveOneWinner(bool sameVisit)
    {
        using var store = new TestStore();
        var visit = await store.Service.EnterGarageAsync(1, "RACE", Token);
        using var first = store.NewContext();
        using var second = store.NewContext();
        var a = store.NewService(first);
        var b = store.NewService(second);
        store.Coordinator.ArmPair();
        var outcomes = await Task.WhenAll(
            Result(() => a.EnterLotAsync(1, 1, sameVisit ? visit.VisitId : null, Token)),
            Result(() => b.EnterLotAsync(1, sameVisit ? 2 : 1, sameVisit ? visit.VisitId : null, Token)));
        Assert.Single(outcomes, x => x == "OK");
        Assert.Single(outcomes, x => x == (sameVisit ? "VISIT_ALREADY_PARKED" : "LOT_UNAVAILABLE"));
        using var fresh = store.NewContext();
        var parking = Assert.Single(await fresh.Parkings.ToListAsync());
        Assert.Equal(ParkingStatus.Active, parking.Status);
        Assert.Equal(ParkingLotStatus.Occupied, (await fresh.ParkingLots.SingleAsync(x => x.Id == parking.ParkingLotId)).CurrentStatus);
        Assert.Equal(sameVisit ? 1 : 0, await fresh.Parkings.CountAsync(x => x.VisitId == visit.VisitId));
        Assert.Equal(4, await fresh.ParkingLots.CountAsync(x => x.CurrentStatus == ParkingLotStatus.Free));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NormalizedEntryRaceHasOnlyOneNewVisit(bool recovery)
    {
        using var store = new TestStore();
        int? oldId = null;
        if (recovery)
        {
            oldId = (await store.Service.EnterGarageAsync(1, "RACE", Token)).VisitId;
            store.Clock.Now += TimeSpan.FromMinutes(2) + TimeSpan.FromTicks(1);
        }

        using var first = store.NewContext();
        using var second = store.NewContext();
        var a = store.NewService(first);
        var b = store.NewService(second);
        store.Coordinator.ArmPair();
        var results = await Task.WhenAll(a.EnterGarageAsync(1, " race ", Token), b.EnterGarageAsync(1, "RACE", Token));
        Assert.Single(results, x => x.Outcome == EntryOutcome.Duplicate);
        Assert.Single(results, x => x.Outcome == (recovery ? EntryOutcome.RecoveredMissingExit : EntryOutcome.Entry));
        Assert.Equal(results[0].VisitId, results[1].VisitId);
        Assert.Equal(results[0].StartAt, results[1].StartAt);
        using var fresh = store.NewContext();
        var active = Assert.Single(await fresh.Visits.Where(x => x.Status == VisitStatus.Active).ToListAsync());
        Assert.Equal("RACE", active.LicenseNumber);
        Assert.Equal(store.Clock.Now, active.StartAt);
        Assert.Equal(recovery ? 2 : 1, await fresh.Visits.CountAsync());
        if (oldId != null)
        {
            var old = await fresh.Visits.SingleAsync(x => x.Id == oldId);
            Assert.Equal(VisitStatus.Interrupted, old.Status);
            Assert.Null(old.Cost);
            Assert.Null(old.EndAt);
            Assert.Equal(store.Clock.Now, old.ClosedAt);
        }
    }

    [Fact]
    public async Task IndependentUnknownCarsBothPersistOnDifferentLots()
    {
        using var store = new TestStore();
        using var first = store.NewContext();
        using var second = store.NewContext();
        var a = store.NewService(first);
        var b = store.NewService(second);
        store.Coordinator.ArmPair();
        await Task.WhenAll(a.EnterLotAsync(1, 1, null, Token), b.EnterLotAsync(1, 2, null, Token));
        using var fresh = store.NewContext();
        var parkings = await store.NewService(fresh).GetActiveParkingsAsync(1, Token);
        Assert.Equal(2, parkings.Count);
        Assert.All(parkings, x =>
        {
            Assert.Null(x.VisitId);
            Assert.Null(x.LicenseNumber);
        });
        Assert.Equal(2, await fresh.Parkings.CountAsync(x => x.Status == ParkingStatus.Active));
        Assert.Equal(2, await fresh.ParkingLots.CountAsync(x => x.CurrentStatus == ParkingLotStatus.Occupied));
        Assert.Empty(await fresh.Visits.ToListAsync());
    }

    [Fact]
    public async Task DifferentRegistrationsCanExceedCapacityWhenFull()
    {
        using var store = new TestStore();
        foreach (var lot in await store.Database.ParkingLots.ToListAsync())
        {
            lot.CurrentStatus = lot.Id % 2 == 0 ? ParkingLotStatus.Unknown : ParkingLotStatus.Occupied;
        }

        await store.Database.SaveChangesAsync();
        for (var pair = 0; pair < 3; pair++)
        {
            using var first = store.NewContext();
            using var second = store.NewContext();
            var a = store.NewService(first);
            var b = store.NewService(second);
            store.Coordinator.ArmPair();
            var results = await Task.WhenAll(a.EnterGarageAsync(1, $"A-{pair}", Token), b.EnterGarageAsync(1, $"B-{pair}", Token));
            Assert.All(results, x => Assert.Equal(EntryOutcome.Entry, x.Outcome));
        }

        using var fresh = store.NewContext();
        var availability = await store.NewService(fresh).GetAvailabilityAsync(1, Token);
        Assert.True(availability.IsFull);
        Assert.Equal(5, availability.Capacity);
        Assert.Equal(6, availability.ActiveVisits);
        Assert.Equal(0, availability.FreeLots);
        Assert.Equal(3, availability.OccupiedLots);
        Assert.Equal(2, availability.UnknownLots);
    }

    private static async Task<string> Result(Func<Task<LotEntryDto>> action)
    {
        try
        {
            await action();
            return "OK";
        }
        catch (ParkingError error)
        {
            Assert.Equal(409, error.StatusCode);
            return error.Code;
        }
    }
}
