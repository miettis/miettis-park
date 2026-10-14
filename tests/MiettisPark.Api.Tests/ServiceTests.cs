using Microsoft.EntityFrameworkCore;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;
using Npgsql;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class ServiceTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [Fact]
    public async Task NormalVisitMovesLotsAndPersistsReceiptWithServerTimes()
    {
        using var store = new TestStore();
        var start = store.Clock.Now;
        var entry = await store.Service.EnterGarageAsync(1, " abc-123 ", Token);
        Assert.Equal("ABC-123", entry.LicenseNumber);
        Assert.Equal(start, entry.StartAt);
        var duplicate = await store.Service.EnterGarageAsync(1, "ABC-123", Token);
        Assert.Equal(entry.VisitId, duplicate.VisitId);
        Assert.Equal(EntryOutcome.Duplicate, duplicate.Outcome);
        store.Clock.Now += TimeSpan.FromMinutes(10);
        await store.Service.EnterLotAsync(1, 1, entry.VisitId, Token);
        store.Clock.Now += TimeSpan.FromMinutes(10);
        await store.Service.LeaveLotAsync(1, 1, Token);
        await store.Service.EnterLotAsync(1, 2, entry.VisitId, Token);
        Assert.Equal(start, (await store.Service.GetActiveVisitsAsync(1, Token)).Single().StartAt);
        Assert.Equal(2, (await store.Service.GetActiveVisitsAsync(1, Token)).Single().LotId);
        store.Clock.Now = start + TimeSpan.FromHours(3) + TimeSpan.FromTicks(1);
        var receipt = await store.Service.LeaveGarageAsync(1, " abc-123 ", Token);
        Assert.Equal(9.30m, receipt.Cost);
        Assert.Equal(store.Clock.Now, receipt.EndAt);
        Assert.Equal(receipt.EndAt, receipt.ClosedAt);
        Assert.Equal(receipt.EndAt - start, receipt.Duration);
        Assert.Equal("EUR", receipt.Currency);
        Assert.True(receipt.GateOpened);
        Assert.Empty(await store.Service.GetActiveVisitsAsync(1, Token));
        Assert.Single(await store.Service.GetActiveParkingsAsync(1, Token));
        Assert.Equal(ParkingLotStatus.Occupied, (await store.Service.GetAvailabilityAsync(1, Token)).Lots.Single(x => x.Id == 2).CurrentStatus);
        store.Clock.Now += TimeSpan.FromHours(1);
        var repeat = await Assert.ThrowsAsync<ParkingError>(() => store.Service.LeaveGarageAsync(1, "ABC-123", Token));
        Assert.Equal("ACTIVE_VISIT_NOT_FOUND", repeat.Code);
        Assert.True(repeat.RequiresCustomerService);
        await store.Service.LeaveLotAsync(1, 2, Token);
        using var fresh = store.NewContext();
        var saved = await fresh.Visits.SingleAsync();
        Assert.Equal(receipt.Cost, saved.Cost);
        Assert.Equal(receipt.EndAt, saved.EndAt);
        Assert.Equal(receipt.ClosedAt, saved.ClosedAt);
        Assert.Equal(VisitStatus.Completed, saved.Status);
        Assert.All(await fresh.Parkings.ToListAsync(), p =>
        {
            Assert.Equal(ParkingStatus.Completed, p.Status);
            Assert.Equal(p.EndAt, p.ClosedAt);
        });
        Assert.Contains("lot:1", store.Transactions.Events);
        Assert.Equal("begin", store.Transactions.Events[0]);
        Assert.Equal("garage:1", store.Transactions.Events[1]);
    }

    [Theory]
    [InlineData(0, 900)]
    [InlineData(1, 930)]
    public async Task ThreeHourBoundaryPersistsReceiptUsingServerClock(int extraTicks, int expectedCents)
    {
        using var store = new TestStore();
        var start = store.Clock.Now;
        var entry = await store.Service.EnterGarageAsync(1, "BOUNDARY", Token);
        var duration = TimeSpan.FromHours(3) + TimeSpan.FromTicks(extraTicks);
        store.Clock.Now = start + duration;
        var receipt = await store.Service.LeaveGarageAsync(1, "BOUNDARY", Token);
        Assert.Equal(entry.VisitId, receipt.VisitId);
        Assert.Equal(start, receipt.StartAt);
        Assert.Equal(duration, receipt.Duration);
        Assert.Equal(expectedCents / 100m, receipt.Cost);
        Assert.Equal(store.Clock.Now, receipt.EndAt);
        Assert.Equal(receipt.EndAt, receipt.ClosedAt);
        Assert.True(receipt.GateOpened);
        Assert.Equal("EUR", receipt.Currency);
        using var fresh = store.NewContext();
        var saved = await fresh.Visits.SingleAsync();
        Assert.Equal(entry.VisitId, saved.Id);
        Assert.Equal(VisitStatus.Completed, saved.Status);
        Assert.Equal(start, saved.StartAt);
        Assert.Equal(receipt.EndAt, saved.EndAt);
        Assert.Equal(receipt.ClosedAt, saved.ClosedAt);
        Assert.Equal(receipt.Cost, saved.Cost);
    }

    [Fact]
    public async Task FreshContextCanContinueActiveVisitAndParking()
    {
        using var store = new TestStore();
        var entry = await store.Service.EnterGarageAsync(1, "RELOAD", Token);
        await store.Service.EnterLotAsync(1, 1, entry.VisitId, Token);
        using var fresh = store.NewContext();
        var service = store.NewService(fresh);
        Assert.Equal(entry.VisitId, (await service.GetActiveVisitsAsync(1, Token)).Single().VisitId);
        Assert.Equal(entry.VisitId, (await service.GetActiveParkingsAsync(1, Token)).Single().VisitId);
        await service.LeaveLotAsync(1, 1, Token);
        await service.EnterLotAsync(1, 2, entry.VisitId, Token);
        store.Clock.Now += TimeSpan.FromMinutes(10);
        Assert.Equal(0.50m, (await service.LeaveGarageAsync(1, "reload", Token)).Cost);
    }

    [Fact]
    public async Task IndependentObservationsAllowSeveralUnknownCarsAndFullGarageEntry()
    {
        using var store = new TestStore();
        await store.Service.EnterLotAsync(1, 1, null, Token);
        await store.Service.EnterLotAsync(1, 2, null, Token);
        foreach (var lot in await store.Database.ParkingLots.Where(x => x.Id > 2).ToListAsync())
        {
            lot.CurrentStatus = ParkingLotStatus.Unknown;
        }

        await store.Database.SaveChangesAsync();
        var availability = await store.Service.GetAvailabilityAsync(1, Token);
        Assert.True(availability.IsFull);
        Assert.Equal(5, availability.Capacity);
        Assert.Equal(0, availability.ActiveVisits);
        Assert.Equal(0, availability.FreeLots);
        Assert.Equal(2, availability.OccupiedLots);
        Assert.Equal(3, availability.UnknownLots);
        Assert.All(await store.Service.GetActiveParkingsAsync(1, Token), p =>
        {
            Assert.Null(p.VisitId);
            Assert.Null(p.LicenseNumber);
        });
        await store.Service.EnterGarageAsync(1, "FULL", Token);
        await store.Service.EnterGarageAsync(1, "OTHER", Token);
        Assert.Equal(2, (await store.Service.GetAvailabilityAsync(1, Token)).ActiveVisits);
        await store.Service.LeaveLotAsync(1, 1, Token);
        Assert.False((await store.Service.GetAvailabilityAsync(1, Token)).IsFull);
        var noOp = await store.Service.LeaveLotAsync(1, 1, Token);
        Assert.Null(noOp.ParkingId);
        Assert.False(noOp.MissingParkingObservation);
        var unknown = await store.Service.LeaveLotAsync(1, 3, Token);
        Assert.True(unknown.MissingParkingObservation);
        Assert.Null(unknown.ParkingId);
        var occupied = await store.Database.ParkingLots.SingleAsync(x => x.Id == 4);
        occupied.CurrentStatus = ParkingLotStatus.Occupied;
        await store.Database.SaveChangesAsync();
        Assert.True((await store.Service.LeaveLotAsync(1, 4, Token)).MissingParkingObservation);
        Assert.Equal(2, (await store.Service.GetActiveVisitsAsync(1, Token)).Count);
    }

    [Theory]
    [InlineData(ParkingLotStatus.Occupied)]
    [InlineData(ParkingLotStatus.Unknown)]
    public async Task NonFreeLotsRejectEntryAfterGarageThenLotLock(ParkingLotStatus status)
    {
        using var store = new TestStore();
        var lot = await store.Database.ParkingLots.SingleAsync(x => x.Id == 1);
        lot.CurrentStatus = status;
        await store.Database.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ParkingError>(() => store.Service.EnterLotAsync(1, 1, null, Token));
        Assert.Equal("LOT_UNAVAILABLE", error.Code);
        Assert.Equal(409, error.StatusCode);
        Assert.Equal(new[] { "begin", "garage:1", "lot:1", "rollback", "dispose" }, store.Transactions.Events);
        Assert.Empty(await store.Database.Parkings.ToListAsync());
    }

    [Fact]
    public async Task OptionalVisitValidationRejectsMissingForeignCompletedAndAlreadyParked()
    {
        using var store = new TestStore();
        store.Database.ParkingGarages.Add(new ParkingGarage { Id = 2, Name = "Other", ParkingFeeConfiguration = PricingTests.Tiers() });
        store.Database.ParkingLots.Add(new ParkingLot { Id = 6, ParkingGarageId = 2, Number = 1 });
        await store.Database.SaveChangesAsync();
        var foreign = await store.Service.EnterGarageAsync(2, "FOREIGN", Token);
        var active = await store.Service.EnterGarageAsync(1, "ACTIVE", Token);
        var completed = await store.Service.EnterGarageAsync(1, "DONE", Token);
        await store.Service.LeaveGarageAsync(1, "DONE", Token);
        Assert.Equal("VISIT_NOT_FOUND", (await Assert.ThrowsAsync<ParkingError>(() => store.Service.EnterLotAsync(1, 1, 999, Token))).Code);
        Assert.Equal("VISIT_NOT_FOUND", (await Assert.ThrowsAsync<ParkingError>(() => store.Service.EnterLotAsync(1, 1, foreign.VisitId, Token))).Code);
        Assert.Equal("VISIT_NOT_ACTIVE", (await Assert.ThrowsAsync<ParkingError>(() => store.Service.EnterLotAsync(1, 1, completed.VisitId, Token))).Code);
        await store.Service.EnterLotAsync(1, 1, active.VisitId, Token);
        Assert.Equal("VISIT_ALREADY_PARKED", (await Assert.ThrowsAsync<ParkingError>(() => store.Service.EnterLotAsync(1, 2, active.VisitId, Token))).Code);
        Assert.Equal("LOT_NOT_FOUND", (await Assert.ThrowsAsync<ParkingError>(() => store.Service.EnterLotAsync(1, 6, null, Token))).Code);
        Assert.Equal("LOT_NOT_FOUND", (await Assert.ThrowsAsync<ParkingError>(() => store.Service.LeaveLotAsync(1, 6, Token))).Code);
        Assert.Equal("GARAGE_NOT_FOUND", (await Assert.ThrowsAsync<ParkingError>(() => store.Service.EnterGarageAsync(99, "X", Token))).Code);
        Assert.Equal(ParkingLotStatus.Free, (await store.Database.ParkingLots.SingleAsync(x => x.Id == 2)).CurrentStatus);
        Assert.Single(await store.Database.Parkings.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901")]
    public async Task EntryAndExitShareLicenseValidation(string? license)
    {
        using var store = new TestStore();
        var entry = await Assert.ThrowsAsync<ParkingError>(() => store.Service.EnterGarageAsync(1, license, Token));
        var exit = await Assert.ThrowsAsync<ParkingError>(() => store.Service.LeaveGarageAsync(1, license, Token));
        Assert.Equal("INVALID_LICENSE_NUMBER", entry.Code);
        Assert.Equal(entry.Code, exit.Code);
        Assert.Equal(400, exit.StatusCode);
        Assert.Empty(store.Transactions.Events);
    }

    [Theory]
    [InlineData("IX_parkings_parking_lot_id", "LOT_UNAVAILABLE")]
    [InlineData("IX_parkings_visit_id", "VISIT_ALREADY_PARKED")]
    [InlineData("IX_visits_parking_garage_id_license_number", "ACTIVE_VISIT_CONFLICT")]
    public async Task KnownDatabaseConflictsAreSanitizedAfterTransactionDisposal(string constraint, string code)
    {
        using var store = new TestStore();
        store.Transactions.CommitFailure = new DbUpdateException("private SQL", new PostgresException("private SQL", "ERROR", "ERROR", "23505", constraintName: constraint));
        var error = await Assert.ThrowsAsync<ParkingError>(() => store.Service.EnterGarageAsync(1, "ABC", Token));
        Assert.Equal(code, error.Code);
        Assert.Equal(409, error.StatusCode);
        Assert.DoesNotContain("SQL", error.Message);
        Assert.Equal("dispose", store.Transactions.Events.Last());
        Assert.Empty(store.Database.ChangeTracker.Entries());
    }

    [Fact]
    public async Task UnknownDatabaseFailuresAreNotDisguisedAsConflicts()
    {
        using var store = new TestStore();
        store.Transactions.CommitFailure = new DbUpdateException("private SQL", new PostgresException("private SQL", "ERROR", "ERROR", "23505", constraintName: "unexpected"));
        await Assert.ThrowsAsync<DbUpdateException>(() => store.Service.EnterGarageAsync(1, "ABC", Token));
    }
}
