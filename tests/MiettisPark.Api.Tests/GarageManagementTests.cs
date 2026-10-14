using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class GarageManagementTests
{
    private static readonly CancellationToken Token = CancellationToken.None;
    private static GarageRequest NewGarage(string name = " Toinen halli ") => new(name, " Katu 1 ", 60.17, 24.94);
    private static LayoutRequest Edit(LayoutDto layout) => new(layout.Version, layout.Width, layout.Height,
        layout.Cells.Select(cell => new LayoutCellRequest(cell.X, cell.Y, cell.Type, cell.LotId,
            cell.LotId is { } id ? layout.Lots.Single(lot => lot.Id == id).Number : null)).ToList(), []);

    [Fact]
    public async Task CreateUpdateReloadAndSeparateGaragePricingPreserveMvp()
    {
        using var store = new TestStore();
        var created = await store.Management.CreateAsync(NewGarage(), Token);
        Assert.Equal("Toinen halli", created.Name);
        Assert.Equal("Katu 1", created.Address);
        Assert.Equal(0, created.Capacity);
        Assert.True(created.IsFull);
        var layout = await store.Management.GetLayoutAsync(created.Id, Token);
        Assert.Equal((1, 1, 1), (layout.Width, layout.Height, layout.Version));
        Assert.Equal(LayoutCellType.Empty, Assert.Single(layout.Cells).Type);
        await store.Management.UpdateAsync(1, new GarageRequest("Vanha", null, null, null), Token);
        await store.Management.UpdateAsync(created.Id, NewGarage("Muokattu"), Token);
        await using var reload = store.NewContext();
        Assert.Equal("Muokattu", (await reload.ParkingGarages.FindAsync(created.Id))!.Name);
        Assert.Null((await reload.ParkingGarages.FindAsync(1))!.Latitude);
        Assert.Equal(1, (await store.Management.GetLayoutAsync(created.Id, Token)).Version);
        var old = await store.Database.ParkingGarages.FindAsync(1);
        old!.ParkingFeeConfiguration = [new() { Amount = 2m, PerUnit = TimeSpan.FromMinutes(10), StartDuration = TimeSpan.Zero }];
        await store.Database.SaveChangesAsync();
        await store.Service.EnterGarageAsync(1, "same", Token);
        await store.Service.EnterGarageAsync(created.Id, "same", Token);
        store.Clock.Now += TimeSpan.FromMinutes(1);
        Assert.Equal(2m, (await store.Service.LeaveGarageAsync(1, "same", Token)).Cost);
        Assert.Equal(0.5m, (await store.Service.LeaveGarageAsync(created.Id, "same", Token)).Cost);
        Assert.Equal(5, (await store.Service.GetAvailabilityAsync(1, Token)).Capacity);
    }

    public static IEnumerable<object[]> InvalidDetails()
    {
        yield return [new GarageRequest(" ", "a", 1, 1)];
        yield return [new GarageRequest(new string('a', 121), "a", 1, 1)];
        yield return [new GarageRequest("a", null, 1, 1)];
        yield return [new GarageRequest("a", " ", 1, 1)];
        yield return [new GarageRequest("a", new string('a', 301), 1, 1)];
        yield return [new GarageRequest("a", "a", null, null)];
        yield return [new GarageRequest("a", "a", 1, null)];
        yield return [new GarageRequest("a", "a", null, 1)];
        yield return [new GarageRequest("a", "a", double.NaN, 1)];
        yield return [new GarageRequest("a", "a", 1, double.PositiveInfinity)];
        yield return [new GarageRequest("a", "a", 90.1, 1)];
        yield return [new GarageRequest("a", "a", -90.1, 1)];
        yield return [new GarageRequest("a", "a", 1, 180.1)];
        yield return [new GarageRequest("a", "a", 1, -180.1)];
    }

    [Theory]
    [MemberData(nameof(InvalidDetails))]
    public async Task InvalidCreationDoesNotWrite(GarageRequest input)
    {
        using var store = new TestStore();
        var error = await Assert.ThrowsAsync<ParkingError>(() => store.Management.CreateAsync(input, Token));
        Assert.Equal((400, "INVALID_GARAGE"), (error.StatusCode, error.Code));
        Assert.Single(await store.Database.ParkingGarages.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public async Task InvalidLayoutsRollback(int scenario)
    {
        using var store = new TestStore();
        var edit = Edit(await store.Management.GetLayoutAsync(1, Token));
        edit = scenario switch
        {
            0 => edit with { Width = 0 },
            1 => edit with { Height = 51 },
            2 => edit with { Cells = null },
            3 => edit with { Cells = edit.Cells!.Skip(1).ToList() },
            4 => edit with { RemovedLotIds = null },
            5 => edit with { RemovedLotIds = [1] },
            16 => edit with { RemovedLotIds = [999] },
            6 => edit with { Cells = [edit.Cells![0], edit.Cells[0], .. edit.Cells.Skip(2)] },
            _ => edit
        };
        if (scenario >= 7 && scenario != 16)
        {
            edit.Cells![0] = scenario switch
            {
                7 => edit.Cells[0] with { X = -1 },
                8 => edit.Cells[0] with { Type = (LayoutCellType)99 },
                9 => edit.Cells[0] with { Number = 0 },
                10 => edit.Cells[0] with { LotId = 0 },
                11 => edit.Cells[0] with { Number = null },
                12 => edit.Cells[0] with { Type = LayoutCellType.Road },
                13 => edit.Cells[0] with { LotId = 2, Number = 9 },
                14 => edit.Cells[0] with { Number = 2 },
                _ => edit.Cells[0] with { LotId = 999 }
            };
        }

        var before = await State(store.Database);
        var error = await Assert.ThrowsAsync<ParkingError>(() => store.Management.SaveLayoutAsync(1, edit, Token));
        Assert.Equal(scenario >= 15 ? 404 : 400, error.StatusCode);
        Assert.Equal(before, await State(store.Database));
    }

    [Fact]
    public async Task NewNumbersAreSmallestFreeInRowOrderAndIdsAreCanonical()
    {
        using var store = new TestStore();
        var garage = await store.Management.CreateAsync(NewGarage(), Token);
        var request = new LayoutRequest(1, 3, 1,
            [new(2, 0, LayoutCellType.ParkingLot, null, null), new(0, 0, LayoutCellType.ParkingLot, null, 2), new(1, 0, LayoutCellType.ParkingLot, null, null)], []);
        var layout = await store.Management.SaveLayoutAsync(garage.Id, request, Token);
        Assert.Equal(2, layout.Version);
        Assert.Equal(new[] { 2, 1, 3 }, layout.Cells.Select(cell => layout.Lots.Single(lot => lot.Id == cell.LotId).Number));
        Assert.All(layout.Cells, cell => Assert.True(cell.LotId > 0));
        Assert.Equal(3, (await store.Service.GetAvailabilityAsync(garage.Id, Token)).Capacity);
        var foreign = Edit(await store.Management.GetLayoutAsync(1, Token));
        foreign.Cells![0] = foreign.Cells[0] with { LotId = layout.Lots[0].Id };
        Assert.Equal("LOT_NOT_FOUND", (await Assert.ThrowsAsync<ParkingError>(() => store.Management.SaveLayoutAsync(1, foreign, Token))).Code);
        var foreignRemoval = Edit(await store.Management.GetLayoutAsync(1, Token)) with { RemovedLotIds = [layout.Lots[0].Id] };
        Assert.Equal("LOT_NOT_FOUND", (await Assert.ThrowsAsync<ParkingError>(() => store.Management.SaveLayoutAsync(1, foreignRemoval, Token))).Code);
    }

    [Fact]
    public async Task SwapsAndMovesPreserveOccupiedIdentityAndActiveParking()
    {
        using var store = new TestStore();
        var parking = await store.Service.EnterLotAsync(1, 1, null, Token);
        var request = Edit(await store.Management.GetLayoutAsync(1, Token));
        request.Cells![0] = request.Cells[0] with { LotId = 2, Number = 1 };
        request.Cells[1] = request.Cells[1] with { LotId = 1, Number = 2 };
        var result = await store.Management.SaveLayoutAsync(1, request, Token);
        Assert.Equal(1, result.Cells[1].LotId);
        Assert.Equal(2, result.Lots.Single(lot => lot.Id == 1).Number);
        Assert.Equal(ParkingLotStatus.Occupied, result.Lots.Single(lot => lot.Id == 1).CurrentStatus);
        Assert.False(result.Lots.Single(lot => lot.Id == 1).CanRemove);
        Assert.Equal(parking.ParkingId, (await store.Database.Parkings.SingleAsync()).Id);
        Assert.Equal(ParkingStatus.Active, (await store.Database.Parkings.SingleAsync()).Status);
        Assert.Equal(new[] { "garage:1", "lot:1", "lot:2", "lot:3", "lot:4", "lot:5" }, store.Transactions.Events.Where(e => e.StartsWith("garage:") || e.StartsWith("lot:")).TakeLast(6));
    }

    [Theory]
    [InlineData(ParkingLotStatus.Occupied, false)]
    [InlineData(ParkingLotStatus.Unknown, false)]
    [InlineData(ParkingLotStatus.Free, true)]
    public async Task RemovalConflictChecksStatusAndActiveParking(ParkingLotStatus status, bool active)
    {
        using var store = new TestStore();
        if (active)
        {
            await store.Service.EnterLotAsync(1, 1, null, Token);
        }

        (await store.Database.ParkingLots.FindAsync(1))!.CurrentStatus = status;
        await store.Database.SaveChangesAsync();
        var layout = await store.Management.GetLayoutAsync(1, Token);
        Assert.False(layout.Lots.Single(lot => lot.Id == 1).CanRemove);
        var request = Edit(layout);
        request.Cells![0] = new(0, 0, LayoutCellType.Road, null, null);
        request.RemovedLotIds!.Add(1);
        var before = await State(store.Database);
        Assert.Equal("LOT_REMOVAL_CONFLICT", (await Assert.ThrowsAsync<ParkingError>(() => store.Management.SaveLayoutAsync(1, request, Token))).Code);
        Assert.Equal(before, await State(store.Database));
    }

    [Fact]
    public async Task ExplicitRetirementPreservesCompletedInterruptedAndActiveHistoryAndReusesNumber()
    {
        using var store = new TestStore();
        await store.Service.EnterGarageAsync(1, "history", Token);
        var visit = (await store.Service.GetActiveVisitsAsync(1, Token)).Single();
        await store.Service.EnterLotAsync(1, 1, visit.VisitId, Token);
        await store.Service.LeaveLotAsync(1, 1, Token);
        store.Clock.Now += TimeSpan.FromMinutes(1);
        await store.Service.LeaveGarageAsync(1, "history", Token);
        var interrupted = await store.Service.EnterGarageAsync(1, "recovery", Token);
        var activeParking = await store.Service.EnterLotAsync(1, 2, interrupted.VisitId, Token);
        store.Clock.Now += TimeSpan.FromMinutes(3);
        await store.Service.EnterGarageAsync(1, "recovery", Token);
        store.Database.Parkings.Add(new Parking { ParkingLotId = 1, StartAt = store.Clock.Now, ClosedAt = store.Clock.Now, Status = ParkingStatus.Interrupted });
        await store.Database.SaveChangesAsync();
        var request = Edit(await store.Management.GetLayoutAsync(1, Token));
        request.Cells![0] = new(0, 0, LayoutCellType.ParkingLot, null, null);
        request.RemovedLotIds!.Add(1);
        var result = await store.Management.SaveLayoutAsync(1, request, Token);
        Assert.NotEqual(1, result.Cells[0].LotId);
        Assert.Equal(1, result.Lots.Single(lot => lot.Id == result.Cells[0].LotId).Number);
        Assert.False((await store.Database.ParkingLots.FindAsync(1))!.IsActive);
        Assert.Equal(5, (await store.Service.GetAvailabilityAsync(1, Token)).Capacity);
        Assert.Equal("LOT_NOT_FOUND", (await Assert.ThrowsAsync<ParkingError>(() => store.Service.EnterLotAsync(1, 1, null, Token))).Code);
        Assert.Equal("LOT_NOT_FOUND", (await Assert.ThrowsAsync<ParkingError>(() => store.Service.LeaveLotAsync(1, 1, Token))).Code);
        await using var reload = store.NewContext();
        var history = await reload.Parkings.Include(parking => parking.Visit).Include(parking => parking.ParkingLot).ToListAsync();
        Assert.Contains(history, parking => parking.ParkingLotId == 1 && parking.Status == ParkingStatus.Completed && parking.Visit!.Status == VisitStatus.Completed);
        Assert.Contains(history, parking => parking.ParkingLotId == 1 && parking.Status == ParkingStatus.Interrupted);
        Assert.Contains(history, parking => parking.Id == activeParking.ParkingId && parking.Status == ParkingStatus.Active && parking.Visit!.Status == VisitStatus.Interrupted);
        Assert.All(history.Where(parking => parking.ParkingLotId == 1), parking => Assert.False(parking.ParkingLot.IsActive));
    }

    [Fact]
    public async Task StaleSaveDoesNotOverwriteAndRetirementFirstRejectsEnter()
    {
        using var store = new TestStore();
        var request = Edit(await store.Management.GetLayoutAsync(1, Token));
        request.Cells![0] = new(0, 0, LayoutCellType.Empty, null, null);
        request.RemovedLotIds!.Add(1);
        await store.Management.SaveLayoutAsync(1, request, Token);
        var before = await State(store.Database);
        Assert.Equal("LAYOUT_VERSION_CONFLICT", (await Assert.ThrowsAsync<ParkingError>(() => store.Management.SaveLayoutAsync(1, request, Token))).Code);
        Assert.Equal("LOT_NOT_FOUND", (await Assert.ThrowsAsync<ParkingError>(() => store.Service.EnterLotAsync(1, 1, null, Token))).Code);
        Assert.Equal(before, await State(store.Database));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task EverySaveStageAndCommitFailureRollBackAllData(int stage)
    {
        using var store = new TestStore();
        await store.Service.EnterGarageAsync(1, "unchanged", Token);
        await store.Service.EnterLotAsync(1, 5, null, Token);
        var request = Edit(await store.Management.GetLayoutAsync(1, Token));
        request.Cells![0] = request.Cells[0] with { Number = 2 };
        request.Cells[1] = request.Cells[1] with { Number = 1 };
        request.Cells[2] = new(2, 0, LayoutCellType.ParkingLot, null, null);
        request.RemovedLotIds!.Add(3);
        var before = await State(store.Database);
        if (stage < 4)
        {
            store.Probe.FailAt = store.Probe.Calls + stage;
        }
        else
        {
            store.Transactions.CommitFailure = new InvalidOperationException("Injected commit failure");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Management.SaveLayoutAsync(1, request, Token));
        await using var fresh = store.NewContext();
        Assert.Equal(before, await State(fresh));
        Assert.Empty(store.Database.ChangeTracker.Entries());
        Assert.Contains("rollback", store.Transactions.Events);
    }

    [Fact]
    public async Task ApiContractsReturnProblemCodesAndSummaryCounts()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        var response = await client.PostAsJsonAsync("/api/garages", NewGarage());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<GarageDto>())!;
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(0, created.Capacity);
        var invalid = await client.PostAsJsonAsync("/api/garages", NewGarage() with { Latitude = null });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("INVALID_GARAGE", (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var updated = await client.PutAsJsonAsync($"/api/garages/{created.Id}", NewGarage("Updated"));
        Assert.Equal("Updated", (await updated.Content.ReadFromJsonAsync<GarageDto>())!.Name);
        var missing = await client.GetAsync("/api/garages/999/layout");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var layout = (await client.GetFromJsonAsync<LayoutDto>("/api/garages/1/layout", jsonOptions))!;
        var stale = await client.PutAsJsonAsync("/api/garages/1/layout", Edit(layout) with { ExpectedVersion = 0 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("LAYOUT_VERSION_CONFLICT", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var garages = (await client.GetFromJsonAsync<List<GarageDto>>("/api/garages"))!;
        Assert.Equal(5, garages.Single(garage => garage.Id == 1).Capacity);
    }


    [Fact]
    public async Task SummaryCountsOnlyActiveLotsAndKeepsUnknownSeparateFromOccupiedAndVisits()
    {
        using var store = new TestStore();
        (await store.Database.ParkingLots.FindAsync(1))!.CurrentStatus = ParkingLotStatus.Unknown;
        (await store.Database.ParkingLots.FindAsync(2))!.CurrentStatus = ParkingLotStatus.Occupied;
        await store.Database.SaveChangesAsync();
        await store.Service.EnterGarageAsync(1, "not-a-lot", Token);
        var request = Edit(await store.Management.GetLayoutAsync(1, Token));
        request.Cells![4] = new(4, 0, LayoutCellType.Empty, null, null);
        request.RemovedLotIds!.Add(5);
        await store.Management.SaveLayoutAsync(1, request, Token);
        var garage = (await store.Service.GetGaragesAsync(Token)).Single();
        Assert.Equal((4, 2, 1, 1, false), (garage.Capacity, garage.FreeLots, garage.OccupiedLots, garage.UnknownLots, garage.IsFull));
        var availability = await store.Service.GetAvailabilityAsync(1, Token);
        Assert.Equal(4, availability.Capacity);
        Assert.Equal(1, availability.ActiveVisits);
        Assert.DoesNotContain(availability.Lots, lot => lot.Id == 5);
    }

    [Fact]
    public async Task CoordinatedLocalRemovalEnterRaceAllowsOnlyOneOutcome()
    {
        using var store = new TestStore();
        var request = Edit(await store.Management.GetLayoutAsync(1, Token));
        request.Cells![0] = new(0, 0, LayoutCellType.Empty, null, null);
        request.RemovedLotIds!.Add(1);
        await using var editorContext = store.NewContext();
        await using var simulationContext = store.NewContext();
        var management = new GarageManagementService(editorContext, new TestTransactions(editorContext, store.Coordinator));
        var simulation = store.NewService(simulationContext);
        store.Coordinator.ArmPair();
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => management.SaveLayoutAsync(1, request, Token)),
            Record.ExceptionAsync(() => simulation.EnterLotAsync(1, 1, null, Token)));
        Assert.Single(errors, error => error == null);
        var failure = Assert.IsType<ParkingError>(Assert.Single(errors, error => error != null));
        Assert.Contains(failure.Code, new[] { "LOT_NOT_FOUND", "LOT_REMOVAL_CONFLICT" });
        await using var fresh = store.NewContext();
        var lot = (await fresh.ParkingLots.FindAsync(1))!;
        Assert.Equal(lot.IsActive, await fresh.Parkings.AnyAsync(parking => parking.ParkingLotId == 1 && parking.Status == ParkingStatus.Active));
    }

    private static async Task<string> State(ParkingDbContext database) => JsonSerializer.Serialize(new
    {
        Garages = (await database.ParkingGarages.AsNoTracking().OrderBy(garage => garage.Id).ToListAsync()).Select(garage => new
        {
            garage.Id,
            garage.Name,
            garage.Address,
            garage.Latitude,
            garage.Longitude,
            garage.LayoutWidth,
            garage.LayoutHeight,
            garage.LayoutVersion,
            garage.LayoutCells,
            garage.ParkingFeeConfiguration
        }),
        Lots = (await database.ParkingLots.AsNoTracking().OrderBy(lot => lot.Id).ToListAsync()).Select(lot => new { lot.Id, lot.Number, lot.ParkingGarageId, lot.IsActive, lot.CurrentStatus }),
        Visits = (await database.Visits.AsNoTracking().OrderBy(visit => visit.Id).ToListAsync()).Select(visit => new { visit.Id, visit.Status, visit.LicenseNumber, visit.StartAt, visit.EndAt, visit.ClosedAt, visit.Cost, visit.ClosureReason }),
        Parkings = (await database.Parkings.AsNoTracking().OrderBy(parking => parking.Id).ToListAsync()).Select(parking => new { parking.Id, parking.ParkingLotId, parking.VisitId, parking.Status, parking.StartAt, parking.EndAt, parking.ClosedAt })
    });
}
