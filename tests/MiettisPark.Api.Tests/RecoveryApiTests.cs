using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class RecoveryApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static readonly CancellationToken Token = CancellationToken.None;

    [Theory]
    [InlineData(null, 0, EntryOutcome.Duplicate)]
    [InlineData(null, 1199999999, EntryOutcome.Duplicate)]
    [InlineData(null, 1200000000, EntryOutcome.Duplicate)]
    [InlineData(null, 1200000001, EntryOutcome.RecoveredMissingExit)]
    [InlineData("00:00:30", 300000000, EntryOutcome.Duplicate)]
    [InlineData("00:00:30", 300000001, EntryOutcome.RecoveredMissingExit)]
    [InlineData("00:00:00", 0, EntryOutcome.Duplicate)]
    public async Task ApiUsesConfiguredWindowAndOriginalStart(string? window, long ticks, EntryOutcome outcome)
    {
        using var factory = new ApiFactory { DuplicateWindow = window };
        using var client = factory.CreateSeededClient();
        var entry = await Enter(client, "BOUNDARY");
        (await client.PostAsJsonAsync("/api/garages/1/lots/1/entries", new { visitId = entry.VisitId })).EnsureSuccessStatusCode();
        factory.Clock.Now = entry.StartAt.AddTicks(ticks);
        var repeated = await Enter(client, " boundary ");
        Assert.Equal(outcome, repeated.Outcome);
        if (outcome == EntryOutcome.Duplicate)
        {
            Assert.Equal(entry.VisitId, repeated.VisitId);
            Assert.Equal(entry.StartAt, repeated.StartAt);
        }
        else
        {
            Assert.NotEqual(entry.VisitId, repeated.VisitId);
            Assert.Equal(factory.Clock.Now, repeated.StartAt);
        }

        Assert.Equal(repeated.VisitId, (await Enter(client, "BOUNDARY")).VisitId);
        var parking = Assert.Single((await client.GetFromJsonAsync<List<ActiveParkingDto>>("/api/garages/1/parkings/active"))!);
        Assert.Equal(entry.VisitId, parking.VisitId);
        Assert.Equal(ParkingLotStatus.Occupied, (await client.GetFromJsonAsync<AvailabilityDto>("/api/garages/1/availability", JsonOptions))!.Lots.Single(x => x.Id == 1).CurrentStatus);
        Assert.Equal(repeated.VisitId, Assert.Single((await client.GetFromJsonAsync<List<ActiveVisitDto>>("/api/garages/1/visits/active"))!).VisitId);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var old = await database.Visits.SingleAsync(x => x.Id == entry.VisitId);
        Assert.Equal(entry.StartAt, old.StartAt);
        Assert.Equal(outcome == EntryOutcome.Duplicate ? VisitStatus.Active : VisitStatus.Interrupted, old.Status);
        Assert.Null(old.EndAt);
        Assert.Null(old.Cost);
        Assert.Equal(outcome == EntryOutcome.Duplicate ? null : "MissingExitBeforeNewEntry", old.ClosureReason);
    }

    [Theory]
    [InlineData("-00:00:01")]
    [InlineData("not-a-duration")]
    public void InvalidConfigurationFailsAtStartupWithoutDatabaseAccess(string value)
    {
        using var factory = new ApiFactory { DuplicateWindow = value };
        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateSeededClient());
        Assert.Contains("DuplicateEntryWindow", exception.ToString());
    }

    [Fact]
    public async Task MissingAndRepeatedExitNeverReturnCostOrHistoricalReceipt()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        await Problem(await client.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = "MISSING" }), 409, "ACTIVE_VISIT_NOT_FOUND", true);
        await Enter(client, "PAID");
        factory.Clock.Now += TimeSpan.FromHours(4);
        (await client.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = "PAID" })).EnsureSuccessStatusCode();
        string before;
        using (var scope = factory.Services.CreateScope())
        {
            before = await RecoveryTests.StateAsync(scope.ServiceProvider.GetRequiredService<ParkingDbContext>());
        }

        factory.Clock.Now += TimeSpan.FromHours(1);
        await Problem(await client.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = "paid" }), 409, "ACTIVE_VISIT_NOT_FOUND", true);
        using var after = factory.Services.CreateScope();
        Assert.Equal(before, await RecoveryTests.StateAsync(after.ServiceProvider.GetRequiredService<ParkingDbContext>()));
    }

    [Theory]
    [InlineData("missing-lot", 404, "LOT_NOT_FOUND")]
    [InlineData("foreign-lot", 404, "LOT_NOT_FOUND")]
    [InlineData("missing-visit", 404, "VISIT_NOT_FOUND")]
    [InlineData("foreign-visit", 404, "VISIT_NOT_FOUND")]
    [InlineData("completed", 409, "VISIT_NOT_ACTIVE")]
    [InlineData("interrupted", 409, "VISIT_NOT_ACTIVE")]
    [InlineData("occupied", 409, "LOT_UNAVAILABLE")]
    [InlineData("unknown", 409, "LOT_UNAVAILABLE")]
    [InlineData("already-parked", 409, "VISIT_ALREADY_PARKED")]
    public async Task ServiceAndApiRejectInvalidLotCommandsWithoutMutation(string scenario, int status, string code)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        var lotId = scenario == "missing-lot" ? 999 : scenario == "foreign-lot" ? 6 : 1;
        int? visitId = scenario is "missing-visit" ? 999 : scenario is "foreign-visit" or "completed" or "interrupted" or "already-parked" ? 1 : null;
        string before;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            db.ParkingGarages.Add(new ParkingGarage { Id = 2, Name = "Other", ParkingFeeConfiguration = PricingTests.Tiers() });
            db.ParkingLots.Add(new ParkingLot { Id = 6, ParkingGarageId = 2, Number = 1 });
            var visitStatus = scenario == "completed" ? VisitStatus.Completed : scenario == "interrupted" ? VisitStatus.Interrupted : VisitStatus.Active;
            db.Visits.Add(new Visit
            {
                Id = 1,
                ParkingGarageId = scenario == "foreign-visit" ? 2 : 1,
                LicenseNumber = "VALIDATE",
                StartAt = factory.Clock.Now,
                Status = visitStatus,
                EndAt = visitStatus == VisitStatus.Completed ? factory.Clock.Now : null,
                ClosedAt = visitStatus == VisitStatus.Active ? null : factory.Clock.Now,
                Cost = visitStatus == VisitStatus.Completed ? 0m : null,
                ClosureReason = visitStatus == VisitStatus.Interrupted ? "MissingExitBeforeNewEntry" : null
            });
            var lot = await db.ParkingLots.SingleAsync(x => x.Id == 1);
            lot.CurrentStatus = scenario == "occupied" ? ParkingLotStatus.Occupied : scenario == "unknown" ? ParkingLotStatus.Unknown : ParkingLotStatus.Free;
            if (scenario == "already-parked")
            {
                db.Parkings.Add(new Parking { ParkingLotId = 2, VisitId = 1, StartAt = factory.Clock.Now });
                (await db.ParkingLots.SingleAsync(x => x.Id == 2)).CurrentStatus = ParkingLotStatus.Occupied;
            }

            await db.SaveChangesAsync();
            before = await RecoveryTests.StateAsync(db);
            var error = await Assert.ThrowsAsync<ParkingError>(() => scope.ServiceProvider.GetRequiredService<ParkingService>().EnterLotAsync(1, lotId, visitId, Token));
            Assert.Equal(status, error.StatusCode);
            Assert.Equal(code, error.Code);
            Assert.Equal(before, await RecoveryTests.StateAsync(db));
        }

        await Problem(await client.PostAsJsonAsync($"/api/garages/1/lots/{lotId}/entries", new { visitId }), status, code);
        using var fresh = factory.Services.CreateScope();
        Assert.Equal(before, await RecoveryTests.StateAsync(fresh.ServiceProvider.GetRequiredService<ParkingDbContext>()));
    }

    [Theory]
    [InlineData("IX_parkings_parking_lot_id", 409, "LOT_UNAVAILABLE")]
    [InlineData("IX_parkings_visit_id", 409, "VISIT_ALREADY_PARKED")]
    [InlineData("IX_visits_parking_garage_id_license_number", 409, "ACTIVE_VISIT_CONFLICT")]
    [InlineData("unexpected", 500, "INTERNAL_ERROR")]
    public async Task SaveFailuresHaveSafeApiResponseAndRollback(string constraint, int status, string code)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        factory.Probe.FailAt = factory.Probe.Calls + 1;
        factory.Probe.Failure = RecoveryTests.DatabaseFailure(constraint);
        var response = constraint.StartsWith("IX_parkings", StringComparison.Ordinal)
            ? await client.PostAsJsonAsync("/api/garages/1/lots/1/entries", new { })
            : await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "PRIVATE-PLATE" });
        await Problem(response, status, code);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Empty(await database.Visits.ToListAsync());
        Assert.Empty(await database.Parkings.ToListAsync());
        Assert.All(await database.ParkingLots.ToListAsync(), x => Assert.Equal(ParkingLotStatus.Free, x.CurrentStatus));
    }

    private static async Task<EntryDto> Enter(HttpClient client, string license)
    {
        var response = await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = license });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EntryDto>(JsonOptions))!;
    }

    private static async Task Problem(HttpResponseMessage response, int status, string code, bool customerService = false)
    {
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(json.RootElement.GetProperty("gateOpened").GetBoolean());
        if (customerService)
        {
            Assert.True(json.RootElement.GetProperty("requiresCustomerService").GetBoolean());
        }

        Assert.False(json.RootElement.TryGetProperty("cost", out _));
        Assert.False(json.RootElement.TryGetProperty("visitId", out _));
        foreach (var forbidden in new[] { "SELECT", "SQL", "StackTrace", "stackTrace", "Npgsql", "PRIVATE-PLATE", "driver secret" })
        {
            Assert.DoesNotContain(forbidden, text);
        }
    }
}
