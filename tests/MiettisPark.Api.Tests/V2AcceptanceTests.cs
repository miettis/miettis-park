using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class V2AcceptanceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task TwoCreatedGaragesPersistLayoutsIndependentOccupancyAndClockReceiptsAcrossApiReload()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        var garages = new List<GarageDto>();
        var layouts = new List<LayoutDto>();
        var entries = new List<EntryDto>();
        var startAt = factory.Clock.Now;

        for (var index = 0; index < 2; index++)
        {
            var request = new GarageRequest($"Hyväksyntähalli {index + 1}", $"Testikatu {index + 1}", 60.17 + index, 24.94 + index);
            using var createdResponse = await client.PostAsJsonAsync("/api/garages", request, JsonOptions);
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
            var garage = (await createdResponse.Content.ReadFromJsonAsync<GarageDto>(JsonOptions))!;
            garages.Add(garage);
            Assert.Equal((request.Name, request.Address, request.Latitude, request.Longitude),
                (garage.Name, garage.Address, garage.Latitude, garage.Longitude));
            Assert.Equal((0, 0, 0, 0, true),
                (garage.Capacity, garage.FreeLots, garage.OccupiedLots, garage.UnknownLots, garage.IsFull));

            var empty = (await client.GetFromJsonAsync<LayoutDto>($"/api/garages/{garage.Id}/layout", JsonOptions))!;
            Assert.Empty(empty.Lots);
            Assert.Equal(LayoutCellType.Empty, Assert.Single(empty.Cells).Type);
            var painted = new LayoutRequest(empty.Version, 4, 1,
                [new(0, 0, LayoutCellType.Road, null, null),
                 new(1, 0, LayoutCellType.ParkingLot, null, null),
                 new(2, 0, LayoutCellType.ParkingLot, null, null),
                 new(3, 0, LayoutCellType.Empty, null, null)], []);
            using var savedResponse = await client.PutAsJsonAsync($"/api/garages/{garage.Id}/layout", painted, JsonOptions);
            savedResponse.EnsureSuccessStatusCode();
            var layout = (await savedResponse.Content.ReadFromJsonAsync<LayoutDto>(JsonOptions))!;
            layouts.Add(layout);
            Assert.Equal(empty.Version + 1, layout.Version);
            Assert.Equal(new[] { 1, 2 }, layout.Lots.OrderBy(lot => lot.Number).Select(lot => lot.Number));
            Assert.All(layout.Lots, lot => Assert.True(lot.Id > 0));
            Assert.Equal(LayoutCellType.Road, layout.Cells.Single(cell => cell.X == 0).Type);
            Assert.Equal(LayoutCellType.Empty, layout.Cells.Single(cell => cell.X == 3).Type);

            using var entryResponse = await client.PostAsJsonAsync($"/api/garages/{garage.Id}/entries", new LicenseRequest(" v2-accept "));
            entryResponse.EnsureSuccessStatusCode();
            var entry = (await entryResponse.Content.ReadFromJsonAsync<EntryDto>(JsonOptions))!;
            entries.Add(entry);
            Assert.Equal(startAt, entry.StartAt);
            var firstLot = layout.Lots.Single(lot => lot.Number == 1);
            var secondLot = layout.Lots.Single(lot => lot.Number == 2);
            using var linkedResponse = await client.PostAsJsonAsync($"/api/garages/{garage.Id}/lots/{firstLot.Id}/entries", new LotEntryRequest(entry.VisitId));
            linkedResponse.EnsureSuccessStatusCode();
            using var independentResponse = await client.PostAsJsonAsync($"/api/garages/{garage.Id}/lots/{secondLot.Id}/entries", new { });
            independentResponse.EnsureSuccessStatusCode();
            Assert.Null((await independentResponse.Content.ReadFromJsonAsync<LotEntryDto>())!.VisitId);
        }

        Assert.NotEqual(entries[0].VisitId, entries[1].VisitId);
        Assert.Empty(layouts[0].Lots.Select(lot => lot.Id).Intersect(layouts[1].Lots.Select(lot => lot.Id)));
        using var wrongGarageResponse = await client.PostAsJsonAsync($"/api/garages/{garages[0].Id}/lots/{layouts[1].Lots[0].Id}/entries", new { });
        Assert.Equal(HttpStatusCode.NotFound, wrongGarageResponse.StatusCode);

        factory.Clock.Now += TimeSpan.FromHours(3) + TimeSpan.FromTicks(1);
        using var reloaded = factory.CreateClient();
        var summaries = (await reloaded.GetFromJsonAsync<List<GarageDto>>("/api/garages"))!;
        foreach (var garage in garages)
        {
            var summary = summaries.Single(value => value.Id == garage.Id);
            Assert.Equal((2, 0, 2, 0, true),
                (summary.Capacity, summary.FreeLots, summary.OccupiedLots, summary.UnknownLots, summary.IsFull));
            using var fullEntryResponse = await reloaded.PostAsJsonAsync($"/api/garages/{garage.Id}/entries", new LicenseRequest("FULL-ALLOWED"));
            fullEntryResponse.EnsureSuccessStatusCode();
            Assert.True((await fullEntryResponse.Content.ReadFromJsonAsync<EntryDto>(JsonOptions))!.GateOpened);
            using var exitResponse = await reloaded.PostAsJsonAsync($"/api/garages/{garage.Id}/exits", new LicenseRequest("V2-ACCEPT"));
            exitResponse.EnsureSuccessStatusCode();
            var receipt = (await exitResponse.Content.ReadFromJsonAsync<ReceiptDto>(JsonOptions))!;
            Assert.Equal(9.30m, receipt.Cost);
            Assert.Equal("EUR", receipt.Currency);
            Assert.Equal(startAt, receipt.StartAt);
            Assert.Equal(factory.Clock.Now, receipt.EndAt);
            Assert.Equal(receipt.EndAt, receipt.ClosedAt);
            Assert.Equal(TimeSpan.FromHours(3) + TimeSpan.FromTicks(1), receipt.Duration);
            Assert.True(receipt.GateOpened);
        }

        using var freshClient = factory.CreateClient();
        for (var index = 0; index < garages.Count; index++)
        {
            var garage = garages[index];
            var layout = (await freshClient.GetFromJsonAsync<LayoutDto>($"/api/garages/{garage.Id}/layout", JsonOptions))!;
            Assert.Equal(layouts[index].Version, layout.Version);
            Assert.Equal(layouts[index].Cells, layout.Cells);
            Assert.All(layout.Lots, lot => Assert.Equal(ParkingLotStatus.Occupied, lot.CurrentStatus));
            var parkings = (await freshClient.GetFromJsonAsync<List<ActiveParkingDto>>($"/api/garages/{garage.Id}/parkings/active"))!;
            Assert.Equal(2, parkings.Count);
            Assert.Single(parkings, parking => parking.VisitId == entries[index].VisitId);
            Assert.Single(parkings, parking => parking.VisitId == null && parking.LicenseNumber == null);
            var activeVisits = (await freshClient.GetFromJsonAsync<List<ActiveVisitDto>>($"/api/garages/{garage.Id}/visits/active"))!;
            Assert.Equal("FULL-ALLOWED", Assert.Single(activeVisits).LicenseNumber);
        }

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        foreach (var entry in entries)
        {
            var visit = await database.Visits.AsNoTracking().SingleAsync(value => value.Id == entry.VisitId);
            Assert.Equal(VisitStatus.Completed, visit.Status);
            Assert.Equal(9.30m, visit.Cost);
            Assert.Equal(startAt, visit.StartAt);
            Assert.Equal(factory.Clock.Now, visit.EndAt);
            Assert.Equal(visit.EndAt, visit.ClosedAt);
            var parking = await database.Parkings.AsNoTracking().SingleAsync(value => value.VisitId == visit.Id);
            Assert.Equal(ParkingStatus.Active, parking.Status);
            Assert.Null(parking.EndAt);
        }
    }
}
