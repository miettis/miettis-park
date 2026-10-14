using Microsoft.EntityFrameworkCore;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;
using Npgsql;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class RecoveryTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [Theory]
    [InlineData(0)]
    [InlineData(1199999999)]
    [InlineData(1200000000)]
    public async Task DuplicateUsesOriginalStartAndNeverChangesParking(long ticks)
    {
        using var store = new TestStore();
        var entry = await store.Service.EnterGarageAsync(1, " boundary ", Token);
        await store.Service.EnterLotAsync(1, 1, entry.VisitId, Token);
        var original = await StateAsync(store.Database);
        store.Clock.Now = entry.StartAt.AddTicks(ticks);
        for (var repeat = 0; repeat < 3; repeat++)
        {
            var result = await store.Service.EnterGarageAsync(1, "BOUNDARY", Token);
            Assert.Equal(EntryOutcome.Duplicate, result.Outcome);
            Assert.Equal(entry.VisitId, result.VisitId);
            Assert.Equal(entry.StartAt, result.StartAt);
        }

        using var fresh = store.NewContext();
        Assert.Equal(original, await StateAsync(fresh));
        store.Clock.Now = entry.StartAt.AddMinutes(2).AddTicks(1);
        Assert.Equal(EntryOutcome.RecoveredMissingExit, (await store.Service.EnterGarageAsync(1, "boundary", Token)).Outcome);
    }

    [Theory]
    [InlineData(1200000001, 1200000000)]
    [InlineData(300000001, 300000000)]
    [InlineData(1, 0)]
    public async Task RecoveryClosesBeforeInsertAndPreservesHistoricalParking(long ticks, long window)
    {
        using var store = new TestStore();
        store.Options.DuplicateEntryWindow = TimeSpan.FromTicks(window);
        var entry = await store.Service.EnterGarageAsync(1, "RECOVER", Token);
        await store.Service.EnterLotAsync(1, 1, entry.VisitId, Token);
        var originalParking = await ParkingStateAsync(store.Database);
        var originalLots = await LotStateAsync(store.Database);
        store.Clock.Now = entry.StartAt.AddTicks(ticks);
        var writesBefore = store.Probe.Writes.Count;
        var recovered = await store.Service.EnterGarageAsync(1, " recover ", Token);
        Assert.Equal(EntryOutcome.RecoveredMissingExit, recovered.Outcome);
        Assert.NotEqual(entry.VisitId, recovered.VisitId);
        Assert.Equal(store.Clock.Now, recovered.StartAt);
        Assert.Equal(new[] { "Modified:Interrupted", "Added:Active" }, store.Probe.Writes.Skip(writesBefore));
        using var fresh = store.NewContext();
        var old = await fresh.Visits.SingleAsync(x => x.Id == entry.VisitId);
        Assert.Equal(VisitStatus.Interrupted, old.Status);
        Assert.Equal("MissingExitBeforeNewEntry", old.ClosureReason);
        Assert.Equal(entry.StartAt, old.StartAt);
        Assert.Equal(store.Clock.Now, old.ClosedAt);
        Assert.Null(old.EndAt);
        Assert.Null(old.Cost);
        Assert.Equal(originalParking, await ParkingStateAsync(fresh));
        Assert.Equal(originalLots, await LotStateAsync(fresh));
        Assert.Single(await fresh.Visits.Where(x => x.Status == VisitStatus.Active).ToListAsync());
        Assert.Null((await store.Service.GetActiveVisitsAsync(1, Token)).Single().LotId);
        var leave = await store.Service.LeaveLotAsync(1, 1, Token);
        Assert.False(leave.MissingParkingObservation);
        using var after = store.NewContext();
        var parking = await after.Parkings.SingleAsync();
        Assert.Equal(entry.VisitId, parking.VisitId);
        Assert.Equal(ParkingStatus.Completed, parking.Status);
        Assert.Equal(store.Clock.Now, parking.EndAt);
        Assert.Equal(parking.EndAt, parking.ClosedAt);
        Assert.Equal(ParkingLotStatus.Free, (await after.ParkingLots.SingleAsync(x => x.Id == 1)).CurrentStatus);
    }

    [Fact]
    public async Task NonDefaultWindowIncludesItsExactBoundary()
    {
        using var store = new TestStore();
        store.Options.DuplicateEntryWindow = TimeSpan.FromSeconds(30);
        var entry = await store.Service.EnterGarageAsync(1, "CUSTOM", Token);
        store.Clock.Now += TimeSpan.FromSeconds(30);
        Assert.Equal(EntryOutcome.Duplicate, (await store.Service.EnterGarageAsync(1, "custom", Token)).Outcome);
        store.Clock.Now += TimeSpan.FromTicks(1);
        Assert.Equal(EntryOutcome.RecoveredMissingExit, (await store.Service.EnterGarageAsync(1, "CUSTOM", Token)).Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRecoveryRollsBackAlreadySavedClosure(bool failCommit)
    {
        using var store = new TestStore();
        await store.Service.EnterGarageAsync(1, "ROLLBACK", Token);
        await store.Service.EnterLotAsync(1, 1, 1, Token);
        var before = await StateAsync(store.Database);
        store.Clock.Now += TimeSpan.FromMinutes(3);
        if (failCommit)
        {
            store.Transactions.CommitFailure = new InvalidOperationException("Injected commit failure");
        }
        else
        {
            store.Probe.FailAt = store.Probe.Calls + 2;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Service.EnterGarageAsync(1, "rollback", Token));
        Assert.Contains("Modified:Interrupted", store.Probe.Writes);
        Assert.Equal(new[] { "rollback", "dispose" }, store.Transactions.Events.TakeLast(2));
        using var fresh = store.NewContext();
        Assert.Equal(before, await StateAsync(fresh));
        store.Transactions.CommitFailure = null;
        Assert.Equal(EntryOutcome.RecoveredMissingExit, (await store.Service.EnterGarageAsync(1, "ROLLBACK", Token)).Outcome);
    }

    [Fact]
    public async Task CompletedVisitAndRepeatedExitNeverChangeParkingOrStoredReceipt()
    {
        using var store = new TestStore();
        var entry = await store.Service.EnterGarageAsync(1, "RECEIPT", Token);
        await store.Service.EnterLotAsync(1, 1, entry.VisitId, Token);
        var parkingBefore = await ParkingStateAsync(store.Database);
        var lotsBefore = await LotStateAsync(store.Database);
        store.Clock.Now += TimeSpan.FromHours(4);
        var receipt = await store.Service.LeaveGarageAsync(1, "receipt", Token);
        using (var fresh = store.NewContext())
        {
            Assert.Equal(parkingBefore, await ParkingStateAsync(fresh));
            Assert.Equal(lotsBefore, await LotStateAsync(fresh));
        }

        var afterExit = await StateAsync(store.Database);
        store.Clock.Now += TimeSpan.FromHours(1);
        var error = await Assert.ThrowsAsync<ParkingError>(() => store.Service.LeaveGarageAsync(1, "RECEIPT", Token));
        Assert.Equal("ACTIVE_VISIT_NOT_FOUND", error.Code);
        using (var fresh = store.NewContext())
        {
            Assert.Equal(afterExit, await StateAsync(fresh));
        }

        var next = await store.Service.EnterGarageAsync(1, "receipt", Token);
        Assert.NotEqual(receipt.VisitId, next.VisitId);
        using (var fresh = store.NewContext())
        {
            var old = await fresh.Visits.SingleAsync(x => x.Id == receipt.VisitId);
            Assert.Equal(receipt.StartAt, old.StartAt);
            Assert.Equal(receipt.EndAt, old.EndAt);
            Assert.Equal(receipt.ClosedAt, old.ClosedAt);
            Assert.Equal(receipt.Cost, old.Cost);
            Assert.Equal(2, await fresh.Visits.CountAsync());
            Assert.Single(await fresh.Parkings.ToListAsync());
        }

        Assert.False((await store.Service.LeaveLotAsync(1, 1, Token)).MissingParkingObservation);
        using var after = store.NewContext();
        Assert.Equal(ParkingStatus.Completed, (await after.Parkings.SingleAsync()).Status);
        Assert.Equal(ParkingLotStatus.Free, (await after.ParkingLots.SingleAsync(x => x.Id == 1)).CurrentStatus);
    }

    [Theory]
    [InlineData("IX_parkings_parking_lot_id", "LOT_UNAVAILABLE")]
    [InlineData("IX_parkings_visit_id", "VISIT_ALREADY_PARKED")]
    [InlineData("IX_visits_parking_garage_id_license_number", "ACTIVE_VISIT_CONFLICT")]
    public async Task SaveChangesUniqueConflictsRollbackBeforeDomainTranslation(string constraint, string code)
    {
        using var store = new TestStore();
        var before = await StateAsync(store.Database);
        store.Probe.FailAt = store.Probe.Calls + 1;
        store.Probe.Failure = DatabaseFailure(constraint);
        var error = await Assert.ThrowsAsync<ParkingError>(() => constraint.StartsWith("IX_parkings", StringComparison.Ordinal)
            ? AsTask(store.Service.EnterLotAsync(1, 1, null, Token))
            : AsTask(store.Service.EnterGarageAsync(1, "PRIVATE-PLATE", Token)));
        Assert.Equal(code, error.Code);
        Assert.Equal(409, error.StatusCode);
        Assert.DoesNotContain("PRIVATE", error.Message);
        Assert.DoesNotContain("SQL", error.Message);
        Assert.Equal(new[] { "rollback", "dispose" }, store.Transactions.Events.TakeLast(2));
        Assert.DoesNotContain("commit", store.Transactions.Events);
        Assert.Empty(store.Database.ChangeTracker.Entries());
        using var fresh = store.NewContext();
        Assert.Equal(before, await StateAsync(fresh));
    }

    [Fact]
    public async Task NullVisitIsNormalWhenReleasingAnIndependentParking()
    {
        using var store = new TestStore();
        await store.Service.EnterLotAsync(1, 1, null, Token);
        var exit = await store.Service.LeaveLotAsync(1, 1, Token);
        Assert.False(exit.MissingParkingObservation);
        Assert.NotNull(exit.ParkingId);
        using var fresh = store.NewContext();
        var parking = await fresh.Parkings.SingleAsync();
        Assert.Null(parking.VisitId);
        Assert.Equal(ParkingStatus.Completed, parking.Status);
        Assert.Equal(store.Clock.Now, parking.EndAt);
        Assert.Equal(parking.EndAt, parking.ClosedAt);
        Assert.Equal(ParkingLotStatus.Free, (await fresh.ParkingLots.SingleAsync(x => x.Id == 1)).CurrentStatus);
    }
    public static DbUpdateException DatabaseFailure(string constraint) => new("SQL PRIVATE-PLATE", new PostgresException("SELECT PRIVATE-PLATE driver secret", "ERROR", "ERROR", "23505", constraintName: constraint));
    private static async Task AsTask<T>(Task<T> task) => await task;
    public static async Task<string> StateAsync(ParkingDbContext database)
    {
        var visits = await database.Visits.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        return string.Join(";", visits.Select(x => $"{x.Id}|{x.ParkingGarageId}|{x.LicenseNumber}|{x.StartAt:O}|{x.EndAt:O}|{x.ClosedAt:O}|{x.Status}|{x.ClosureReason}|{x.Cost}"))
            + "#" + await ParkingStateAsync(database) + "#" + await LotStateAsync(database);
    }

    private static async Task<string> ParkingStateAsync(ParkingDbContext database)
    {
        var rows = await database.Parkings.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        return string.Join(";", rows.Select(x => $"{x.Id}|{x.ParkingLotId}|{x.VisitId}|{x.StartAt:O}|{x.EndAt:O}|{x.ClosedAt:O}|{x.Status}"));
    }

    private static async Task<string> LotStateAsync(ParkingDbContext database)
    {
        var rows = await database.ParkingLots.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        return string.Join(";", rows.Select(x => $"{x.Id}|{x.ParkingGarageId}|{x.Number}|{x.CurrentStatus}"));
    }
}
