using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class AdminReadContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadsPreserveIndependentUnknownAndHistoricalObservations(bool recoverMissingExit)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        var visitStart = factory.Clock.Now;
        var entry = await Post<EntryDto>(client, "/api/garages/1/entries", new { licenseNumber = "hist-123" });
        factory.Clock.Now += TimeSpan.FromMinutes(1);
        var parkingStart = factory.Clock.Now;
        var linked = await Post<LotEntryDto>(client, "/api/garages/1/lots/1/entries", new { visitId = entry.VisitId });
        factory.Clock.Now += TimeSpan.FromMinutes(1);
        var unknownStart = factory.Clock.Now;
        var unknown = await Post<LotEntryDto>(client, "/api/garages/1/lots/2/entries", new { });
        factory.Clock.Now += TimeSpan.FromMinutes(1);
        if (!recoverMissingExit)
        {
            await Post<ReceiptDto>(client, "/api/garages/1/exits", new { licenseNumber = "hist-123" });
        }

        var newVisit = await Post<EntryDto>(client, "/api/garages/1/entries", new { licenseNumber = "hist-123" });
        Assert.NotEqual(entry.VisitId, newVisit.VisitId);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            (await database.ParkingLots.SingleAsync(lot => lot.Id == 3)).CurrentStatus = ParkingLotStatus.Unknown;
            await database.SaveChangesAsync();
        }

        var before = await Snapshot(factory);
        factory.Clock.Now += TimeSpan.FromHours(1);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            Assert.Single((await client.GetFromJsonAsync<List<GarageDto>>("/api/garages"))!);
            var availability = (await client.GetFromJsonAsync<AvailabilityDto>("/api/garages/1/availability", JsonOptions))!;
            Assert.Equal((5, 2, 2, 1, 1, false), (availability.Capacity, availability.FreeLots, availability.OccupiedLots, availability.UnknownLots, availability.ActiveVisits, availability.IsFull));
            var parkings = (await client.GetFromJsonAsync<List<ActiveParkingDto>>("/api/garages/1/parkings/active"))!;
            Assert.Equal(2, parkings.Count);
            var historical = Assert.Single(parkings, parking => parking.ParkingId == linked.ParkingId);
            Assert.Equal(entry.VisitId, historical.VisitId);
            Assert.Equal("HIST-123", historical.LicenseNumber);
            Assert.Equal(1, historical.LotNumber);
            Assert.Equal(parkingStart, historical.StartAt);
            Assert.NotEqual(visitStart, historical.StartAt);
            var unidentified = Assert.Single(parkings, parking => parking.ParkingId == unknown.ParkingId);
            Assert.Null(unidentified.VisitId);
            Assert.Null(unidentified.LicenseNumber);
            Assert.Equal(2, unidentified.LotNumber);
            Assert.Equal(unknownStart, unidentified.StartAt);
            var visit = Assert.Single((await client.GetFromJsonAsync<List<ActiveVisitDto>>("/api/garages/1/visits/active"))!);
            Assert.Equal(newVisit.VisitId, visit.VisitId);
            Assert.Equal(newVisit.StartAt, visit.StartAt);
            Assert.Equal("HIST-123", visit.LicenseNumber);
            Assert.Null(visit.LotId);
            Assert.Null(visit.LotNumber);
        }

        Assert.Equal(before, await Snapshot(factory));
    }

    private static async Task<T> Post<T>(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task<string> Snapshot(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var visits = await database.Visits.AsNoTracking().OrderBy(visit => visit.Id)
            .Select(visit => new { visit.Id, visit.ParkingGarageId, visit.LicenseNumber, visit.StartAt, visit.EndAt, visit.ClosedAt, visit.Status, visit.ClosureReason, visit.Cost }).ToListAsync();
        var parkings = await database.Parkings.AsNoTracking().OrderBy(parking => parking.Id)
            .Select(parking => new { parking.Id, parking.VisitId, parking.ParkingLotId, parking.StartAt, parking.EndAt, parking.ClosedAt, parking.Status }).ToListAsync();
        var lots = await database.ParkingLots.AsNoTracking().OrderBy(lot => lot.Id)
            .Select(lot => new { lot.Id, lot.ParkingGarageId, lot.Number, lot.CurrentStatus }).ToListAsync();
        var garages = await database.ParkingGarages.AsNoTracking().OrderBy(garage => garage.Id)
            .Select(garage => new { garage.Id, garage.Name, garage.ParkingFeeConfiguration }).ToListAsync();
        return JsonSerializer.Serialize(new { visits, parkings, lots, garages }, JsonOptions);
    }
}
