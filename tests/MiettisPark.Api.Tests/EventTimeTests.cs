using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class EventTimeTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(600, 0.5)]
    [InlineData(10800, 9)]
    [InlineData(10801, 9.3)]
    [InlineData(14400, 10.8)]
    public async Task ServiceAndApiPersistExactReceipt(int seconds, double expected)
    {
        using var store = new TestStore();
        var entered = await store.Service.EnterGarageAsync(1, " receipt ", Start.ToOffset(TimeSpan.FromHours(3)), default);
        var receipt = await store.Service.LeaveGarageAsync(1, "RECEIPT", Start.AddSeconds(seconds), default);
        Assert.Equal((decimal)expected, receipt.Cost);
        Assert.Equal(Start, entered.EffectiveOccurredAt);
        Assert.Equal(TimeSpan.Zero, entered.StartAt.Offset);
        Assert.Equal(Start.AddSeconds(seconds), receipt.EffectiveOccurredAt);
        Assert.Equal(TimeSpan.FromSeconds(seconds), receipt.Duration);
        using var fresh = store.NewContext();
        var saved = await fresh.Visits.SingleAsync();
        Assert.Equal(receipt.Cost, saved.Cost);
        Assert.Equal(receipt.EndAt, saved.EndAt);
        Assert.Equal(receipt.EndAt, saved.ClosedAt);
        Assert.Equal(VisitStatus.Completed, saved.Status);
        Assert.Equal(0, store.Clock.Reads);

        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        (await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "receipt", occurredAt = Start })).EnsureSuccessStatusCode();
        var response = await client.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = "receipt", occurredAt = Start.AddSeconds(seconds) });
        response.EnsureSuccessStatusCode();
        var apiReceipt = (await response.Content.ReadFromJsonAsync<ReceiptDto>())!;
        Assert.Equal(receipt.Cost, apiReceipt.Cost);
        Assert.Equal(receipt.Duration, apiReceipt.Duration);
        Assert.Equal(receipt.EffectiveOccurredAt, apiReceipt.EffectiveOccurredAt);
    }

    [Fact]
    public async Task MissingTimeIsReadOncePerCommandAndExplicitOffsetsAreUtc()
    {
        using var store = new TestStore();
        var entry = await store.Service.EnterGarageAsync(1, "default", default);
        Assert.Equal(1, store.Clock.Reads);
        var parking = await store.Service.EnterLotAsync(1, 1, entry.VisitId, default);
        Assert.Equal(2, store.Clock.Reads);
        var exit = await store.Service.LeaveLotAsync(1, 1, default);
        Assert.Equal(3, store.Clock.Reads);
        var receipt = await store.Service.LeaveGarageAsync(1, "default", default);
        Assert.Equal(4, store.Clock.Reads);
        Assert.Equal(store.Clock.Now, entry.EffectiveOccurredAt);
        Assert.Equal(store.Clock.Now, parking.EffectiveOccurredAt);
        Assert.Equal(store.Clock.Now, exit.EffectiveOccurredAt);
        Assert.Equal(store.Clock.Now, receipt.EffectiveOccurredAt);
        var explicitTime = Start.ToOffset(TimeSpan.FromHours(-5));
        var explicitEntry = await store.Service.EnterGarageAsync(1, "other", explicitTime, default);
        var explicitParking = await store.Service.EnterLotAsync(1, 2, explicitEntry.VisitId, explicitTime, default);
        var explicitExit = await store.Service.LeaveLotAsync(1, 2, explicitTime, default);
        var explicitReceipt = await store.Service.LeaveGarageAsync(1, "other", explicitTime, default);
        Assert.All(new[] { explicitEntry.EffectiveOccurredAt, explicitParking.EffectiveOccurredAt, explicitExit.EffectiveOccurredAt, explicitReceipt.EffectiveOccurredAt }, time =>
        {
            Assert.Equal(Start, time);
            Assert.Equal(TimeSpan.Zero, time.Offset);
        });
        Assert.Equal(4, store.Clock.Reads);
    }

    [Fact]
    public async Task DisabledRejectsAllExplicitCommandsButDefaultsWork()
    {
        Assert.False(new SimulationOptions().Enabled);
        using var store = new TestStore();
        store.Simulation.Enabled = false;
        await Error(() => store.Service.EnterGarageAsync(1, "test", Start, default), "SIMULATION_TIME_DISABLED");
        await Error(() => store.Service.EnterLotAsync(1, 1, null, Start, default), "SIMULATION_TIME_DISABLED");
        await Error(() => store.Service.LeaveLotAsync(1, 1, Start, default), "SIMULATION_TIME_DISABLED");
        await Error(() => store.Service.LeaveGarageAsync(1, "test", Start, default), "SIMULATION_TIME_DISABLED");
        Assert.Empty(store.Transactions.Events);
        await store.Service.EnterGarageAsync(1, "test", default);
        await store.Service.EnterLotAsync(1, 1, null, default);
        await store.Service.LeaveLotAsync(1, 1, default);
        await store.Service.LeaveGarageAsync(1, "test", default);
    }

    [Theory]
    [InlineData("entries")]
    [InlineData("exits")]
    [InlineData("lots/1/entries")]
    [InlineData("lots/1/exit")]
    public async Task ApiValidatesOffsetAndDisabledBeforeWriting(string path)
    {
        using var factory = new ApiFactory { SimulationEnabled = false };
        using var client = factory.CreateSeededClient();
        foreach (var time in new[] { "2026-01-01T10:00:00", "invalid", "2026-02-30T10:00:00Z", "2026-01-01" })
        {
            await Problem(await client.PostAsJsonAsync($"/api/garages/1/{path}", new { licenseNumber = "test", occurredAt = time }), HttpStatusCode.BadRequest, "INVALID_REQUEST");
        }
        await Problem(await client.PostAsJsonAsync($"/api/garages/1/{path}", new { licenseNumber = "test", occurredAt = Start }), HttpStatusCode.BadRequest, "SIMULATION_TIME_DISABLED");
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Visits);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Parkings);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"occurredAt\":null}")]
    public async Task ApiPreservesEmptyNullAndBodylessCalls(string body)
    {
        using var factory = new ApiFactory { SimulationEnabled = false };
        using var client = factory.CreateSeededClient();
        (await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "test", occurredAt = (string?)null })).EnsureSuccessStatusCode();
        var entry = await client.PostAsync("/api/garages/1/lots/1/entries", new StringContent(body, Encoding.UTF8, "application/json"));
        entry.EnsureSuccessStatusCode();
        Assert.Equal(factory.Clock.Now, (await entry.Content.ReadFromJsonAsync<LotEntryDto>())!.EffectiveOccurredAt);
        var exit = await client.PostAsync("/api/garages/1/lots/1/exit", null);
        exit.EnsureSuccessStatusCode();
        Assert.Equal(factory.Clock.Now, (await exit.Content.ReadFromJsonAsync<LotExitDto>())!.EffectiveOccurredAt);
        (await client.PostAsync("/api/garages/1/lots/1/exit", new StringContent(body, Encoding.UTF8, "application/json"))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = "test" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DuplicateBoundaryRecoveryAndHistoryAreAtomicAndIndependentOfParking()
    {
        using var store = new TestStore();
        var entry = await store.Service.EnterGarageAsync(1, "same", Start, default);
        var parking = await store.Service.EnterLotAsync(1, 1, entry.VisitId, Start, default);
        var duplicateTime = Start.Add(store.Options.DuplicateEntryWindow);
        var duplicate = await store.Service.EnterGarageAsync(1, " SAME ", duplicateTime, default);
        Assert.Equal(EntryOutcome.Duplicate, duplicate.Outcome);
        Assert.Equal(entry.VisitId, duplicate.VisitId);
        Assert.Equal(Start, duplicate.StartAt);
        Assert.Equal(duplicateTime, duplicate.EffectiveOccurredAt);
        await Error(() => store.Service.EnterGarageAsync(1, "same", Start.AddTicks(-1), default), "EVENT_TIME_CONFLICT", Start);
        var recoveryTime = duplicateTime.AddTicks(1);
        var recovered = await store.Service.EnterGarageAsync(1, "same", recoveryTime, default);
        Assert.Equal(EntryOutcome.RecoveredMissingExit, recovered.Outcome);
        Assert.Equal(recoveryTime, recovered.StartAt);
        var interrupted = await store.Database.Visits.AsNoTracking().SingleAsync(x => x.Id == entry.VisitId);
        Assert.Equal(VisitStatus.Interrupted, interrupted.Status);
        Assert.Equal(recoveryTime, interrupted.ClosedAt);
        Assert.Null(interrupted.EndAt);
        Assert.Null(interrupted.Cost);
        await Error(() => store.Service.EnterGarageAsync(1, "same", duplicateTime, default), "EVENT_TIME_CONFLICT", recoveryTime);
        await Error(() => store.Service.LeaveGarageAsync(1, "same", duplicateTime, default), "EVENT_TIME_CONFLICT", recoveryTime);
        await store.Service.LeaveGarageAsync(1, "same", recoveryTime, default);
        await Error(() => store.Service.EnterGarageAsync(1, "same", duplicateTime, default), "EVENT_TIME_CONFLICT", recoveryTime);
        Assert.Equal(ParkingStatus.Active, (await store.Database.Parkings.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(parking.ParkingId, (await store.Database.Parkings.AsNoTracking().SingleAsync()).Id);
        Assert.Equal(ParkingLotStatus.Occupied, (await store.Database.ParkingLots.AsNoTracking().SingleAsync(x => x.Id == 1)).CurrentStatus);
        await store.Service.LeaveLotAsync(1, 1, recoveryTime, default);
    }

    [Fact]
    public async Task ParkingHistoryLinkAndNoopBoundsAreValidatedBeforeMutation()
    {
        using var store = new TestStore();
        var visit = await store.Service.EnterGarageAsync(1, "linked", Start, default);
        await Error(() => store.Service.EnterLotAsync(1, 1, visit.VisitId, Start.AddTicks(-1), default), "EVENT_TIME_CONFLICT", Start);
        Assert.Empty(store.Database.Parkings);
        var end = Start.AddHours(1);
        await store.Service.EnterLotAsync(1, 1, visit.VisitId, Start, default);
        await Error(() => store.Service.LeaveLotAsync(1, 1, Start.AddTicks(-1), default), "EVENT_TIME_CONFLICT", Start);
        Assert.Equal(ParkingStatus.Active, (await store.Database.Parkings.AsNoTracking().SingleAsync()).Status);
        await store.Service.LeaveLotAsync(1, 1, end, default);
        await Error(() => store.Service.EnterLotAsync(1, 1, null, end.AddTicks(-1), default), "EVENT_TIME_CONFLICT", end);
        await Error(() => store.Service.LeaveLotAsync(1, 1, end.AddTicks(-1), default), "EVENT_TIME_CONFLICT", end);
        var noop = await store.Service.LeaveLotAsync(1, 1, end, default);
        Assert.Null(noop.ParkingId);
        Assert.Equal(end, noop.EffectiveOccurredAt);
        await store.Service.EnterLotAsync(1, 1, null, end, default);
        await store.Service.LeaveLotAsync(1, 1, end, default);
        await store.Service.LeaveGarageAsync(1, "linked", end, default);
        await Error(() => store.Service.EnterLotAsync(1, 2, visit.VisitId, Start, default), "VISIT_NOT_ACTIVE");
        var history = await store.Database.Parkings.SingleAsync(x => x.Id == 1);
        history.Status = ParkingStatus.Interrupted;
        history.EndAt = null;
        history.ClosedAt = end.AddHours(1);
        await store.Database.SaveChangesAsync();
        await Error(() => store.Service.EnterLotAsync(1, 1, null, end, default), "EVENT_TIME_CONFLICT", end.AddHours(1));
        Assert.Equal(2, await store.Database.Parkings.CountAsync());
        Assert.Equal(ParkingLotStatus.Free, (await store.Database.ParkingLots.AsNoTracking().SingleAsync(x => x.Id == 1)).CurrentStatus);
    }

    [Fact]
    public async Task ConflictRollsBackWithoutSseAndApiReportsUtcMinimumForAllCommands()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        using var subscription = factory.Services.GetRequiredService<GarageEventPublisher>().Subscribe();
        var entry = await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "rollback", occurredAt = Start.ToOffset(TimeSpan.FromHours(3)) });
        entry.EnsureSuccessStatusCode();
        Assert.True(subscription.Reader.TryRead(out _));
        var before = await client.GetStringAsync("/api/garages/1/visits/active");
        foreach (var path in new[] { "entries", "exits", "lots/1/entries" })
        {
            using var doc = JsonDocument.Parse(await entry.Content.ReadAsStringAsync());
            var response = await client.PostAsJsonAsync($"/api/garages/1/{path}", new { licenseNumber = "rollback", visitId = doc.RootElement.GetProperty("visitId").GetInt32(), occurredAt = Start.AddTicks(-1) });
            await Problem(response, HttpStatusCode.Conflict, "EVENT_TIME_CONFLICT", Start);
            Assert.False(subscription.Reader.TryRead(out _));
            Assert.Equal(before, await client.GetStringAsync("/api/garages/1/visits/active"));
        }
        (await client.PostAsJsonAsync("/api/garages/1/lots/1/entries", new { occurredAt = Start })).EnsureSuccessStatusCode();
        Assert.True(subscription.Reader.TryRead(out _));
        await Problem(await client.PostAsJsonAsync("/api/garages/1/lots/1/exit", new { occurredAt = Start.AddTicks(-1) }), HttpStatusCode.Conflict, "EVENT_TIME_CONFLICT", Start);
        Assert.False(subscription.Reader.TryRead(out _));
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var visit = await database.Visits.SingleAsync();
        Assert.Null(visit.Cost);
        Assert.Null(visit.EndAt);
        Assert.Equal(VisitStatus.Active, visit.Status);
        Assert.Equal(ParkingStatus.Active, (await database.Parkings.SingleAsync()).Status);
    }

    [Fact]
    public async Task RecoverySecondSaveFailureRestoresBothHistories()
    {
        using var store = new TestStore();
        var entered = await store.Service.EnterGarageAsync(1, "rollback", Start, default);
        await store.Service.EnterLotAsync(1, 1, entered.VisitId, Start, default);
        store.Probe.FailAt = store.Probe.Calls + 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Service.EnterGarageAsync(1, "rollback", Start.AddHours(1), default));
        var visit = await store.Database.Visits.AsNoTracking().SingleAsync();
        Assert.Equal(VisitStatus.Active, visit.Status);
        Assert.Null(visit.ClosedAt);
        Assert.Null(visit.EndAt);
        Assert.Null(visit.Cost);
        Assert.Equal(ParkingStatus.Active, (await store.Database.Parkings.AsNoTracking().SingleAsync()).Status);
        Assert.Contains("rollback", store.Transactions.Events);
    }

    [Fact]
    public async Task CoordinatedCommandsKeepIndependentTimesAndHistoryConflicts()
    {
        using var store = new TestStore();
        using var one = store.NewContext();
        using var two = store.NewContext();
        var first = store.NewService(one);
        var second = store.NewService(two);
        store.Coordinator.ArmPair();
        var results = await Task.WhenAll(first.EnterGarageAsync(1, "future", Start.AddYears(1), default), second.EnterGarageAsync(1, "past", Start.AddYears(-1), default));
        Assert.Equal(Start.AddYears(1), results[0].EffectiveOccurredAt);
        Assert.Equal(Start.AddYears(-1), results[1].EffectiveOccurredAt);
        store.Coordinator.ArmPair();
        var lots = await Task.WhenAll(first.EnterLotAsync(1, 1, null, Start.AddYears(1), default), second.EnterLotAsync(1, 2, null, Start.AddYears(-1), default));
        Assert.Equal(Start.AddYears(1), lots[0].EffectiveOccurredAt);
        Assert.Equal(Start.AddYears(-1), lots[1].EffectiveOccurredAt);
        await first.EnterGarageAsync(1, "race", Start, default);
        store.Coordinator.ArmPair();
        var valid = first.EnterGarageAsync(1, "race", Start, default);
        var conflict = Error(() => second.EnterGarageAsync(1, "race", Start.AddTicks(-1), default), "EVENT_TIME_CONFLICT", Start);
        await Task.WhenAll(valid, conflict);
        Assert.Equal(EntryOutcome.Duplicate, (await valid).Outcome);
        using var fresh = store.NewContext();
        Assert.Single(await fresh.Visits.Where(x => x.LicenseNumber == "RACE" && x.Status == VisitStatus.Active).ToListAsync());
        Assert.Equal(0, store.Clock.Reads);
    }

    [Fact]
    public async Task ResolvedTimeSurvivesTransactionWaitAndSeparateGarages()
    {
        using var store = new TestStore();
        using var one = store.NewContext();
        using var two = store.NewContext();
        var garage = await store.Management.CreateAsync(new GarageRequest("Other", "Street", 0, 0), default);
        store.Coordinator.ArmPair();
        store.Clock.Now = Start;
        var pending = store.NewService(one).EnterGarageAsync(1, "same", default);
        store.Clock.Now = Start.AddYears(-1);
        var other = store.NewService(two).EnterGarageAsync(garage.Id, "same", default);
        await Task.WhenAll(pending, other);
        Assert.Equal(Start, (await pending).EffectiveOccurredAt);
        Assert.Equal(Start.AddYears(-1), (await other).EffectiveOccurredAt);
        Assert.Equal(2, store.Clock.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApiChecksCompletedAndInterruptedHistoryBeforeReentry(bool interrupted)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        (await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "history", occurredAt = Start })).EnsureSuccessStatusCode();
        var end = Start.AddHours(1);
        (await client.PostAsJsonAsync($"/api/garages/1/{(interrupted ? "entries" : "exits")}", new { licenseNumber = "history", occurredAt = end })).EnsureSuccessStatusCode();
        if (interrupted)
        {
            (await client.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = "history", occurredAt = end })).EnsureSuccessStatusCode();
        }
        var before = factory.Probe.Calls;
        await Problem(await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = " HISTORY ", occurredAt = end.AddTicks(-1) }), HttpStatusCode.Conflict, "EVENT_TIME_CONFLICT", end);
        Assert.Equal(before, factory.Probe.Calls);
        (await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "history", occurredAt = end })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ApiUsesOffsetTimeForAllFourCommandsAndDuplicate()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        var value = Start.ToOffset(TimeSpan.FromHours(3));
        var entry = await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "offset", occurredAt = value });
        entry.EnsureSuccessStatusCode();
        using var entryJson = JsonDocument.Parse(await entry.Content.ReadAsStringAsync());
        var id = entryJson.RootElement.GetProperty("visitId").GetInt32();
        var duplicate = await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "offset", occurredAt = value.AddMinutes(2) });
        duplicate.EnsureSuccessStatusCode();
        using var duplicateJson = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.Equal("Duplicate", duplicateJson.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(Start, duplicateJson.RootElement.GetProperty("startAt").GetDateTimeOffset());
        Assert.Equal(Start.AddMinutes(2), duplicateJson.RootElement.GetProperty("effectiveOccurredAt").GetDateTimeOffset());
        var parking = await client.PostAsJsonAsync("/api/garages/1/lots/1/entries", new { visitId = id, occurredAt = value });
        parking.EnsureSuccessStatusCode();
        var exit = await client.PostAsJsonAsync("/api/garages/1/lots/1/exit", new { occurredAt = value });
        exit.EnsureSuccessStatusCode();
        var receipt = await client.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = "offset", occurredAt = value });
        receipt.EnsureSuccessStatusCode();
        foreach (var response in new[] { entry, parking, exit, receipt })
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var effective = json.RootElement.GetProperty("effectiveOccurredAt").GetDateTimeOffset();
            Assert.Equal(Start, effective);
            Assert.Equal(TimeSpan.Zero, effective.Offset);
        }
    }

    [Fact]
    public async Task OpenApiPreservesOptionalOffsetRequestsAndEffectiveResponseTimes()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "LicenseRequest", "LotEntryRequest", "LotExitRequest" })
        {
            var schema = schemas.GetProperty(name);
            var time = schema.GetProperty("properties").GetProperty("occurredAt");
            Assert.Equal("date-time", time.GetProperty("format").GetString());
            Assert.Contains(time.GetProperty("type").EnumerateArray(), item => item.GetString() == "null");
            if (schema.TryGetProperty("required", out var required))
            {
                Assert.DoesNotContain(required.EnumerateArray(), item => item.GetString() == "occurredAt");
            }
        }
        foreach (var name in new[] { "EntryDto", "LotEntryDto", "LotExitDto", "ReceiptDto" })
        {
            Assert.Equal("date-time", schemas.GetProperty(name).GetProperty("properties").GetProperty("effectiveOccurredAt").GetProperty("format").GetString());
        }
        var exit = document.RootElement.GetProperty("paths").GetProperty("/api/garages/{garageId}/lots/{lotId}/exit").GetProperty("post").GetProperty("requestBody");
        Assert.False(exit.TryGetProperty("required", out var bodyRequired) && bodyRequired.GetBoolean());
    }

    private static async Task Error(Func<Task> action, string code, DateTimeOffset? minimum = null)
    {
        ParkingError? error = null;
        try
        {
            await action();
        }
        catch (ParkingError caught)
        {
            error = caught;
        }
        Assert.NotNull(error);
        Assert.Equal(code, error.Code);
        Assert.Equal(minimum, error.MinimumOccurredAt);
    }

    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status, string code, DateTimeOffset? minimum = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        if (minimum.HasValue)
        {
            var actual = json.RootElement.GetProperty("minimumOccurredAt").GetDateTimeOffset();
            Assert.Equal(minimum.Value, actual);
            Assert.Equal(TimeSpan.Zero, actual.Offset);
        }
    }
}
