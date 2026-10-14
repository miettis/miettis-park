using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services.PlateRecognition;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class V3AcceptanceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData(10800, 900)]
    [InlineData(10801, 930)]
    [InlineData(14400, 1080)]
    public async Task ImageEntryThroughIndependentParkingAndExitPersistsRealReceipt(int seconds, int expectedCents)
    {
        var reader = new Reader();
        using var factory = new ApiFactory { PlateReader = reader };
        using var client = factory.CreateSeededClient();
        var start = new DateTimeOffset(2026, 10, 15, 13, 0, 0, TimeSpan.FromHours(3));
        var utcStart = start.ToUniversalTime();
        var parkingStart = utcStart.AddMinutes(10);
        var parkingEnd = utcStart.AddHours(1);
        var visitEnd = utcStart.AddSeconds(seconds);
        var before = (await client.GetFromJsonAsync<AvailabilityDto>("/api/garages/1/availability", Json))!;
        using var image = new MagickImage(MagickColors.White, 2, 2);
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(image.ToByteArray(MagickFormat.Png)), "image", "car.png");
        upload.Add(new StringContent(start.ToString("O")), "occurredAt");

        using var entered = await client.PostAsync("/api/garages/1/entries/from-image", upload);
        entered.EnsureSuccessStatusCode();
        var entry = (await entered.Content.ReadFromJsonAsync<ImageEntryDto>(Json))!;
        Assert.Equal(1, reader.Calls);
        Assert.Equal("ABC-123", entry.RecognizedLicenseNumber);
        Assert.Equal(entry.RecognizedLicenseNumber, entry.LicenseNumber);
        Assert.Equal(EntryOutcome.Entry, entry.Outcome);
        Assert.True(entry.GateOpened);
        Assert.Equal(utcStart, entry.StartAt);
        Assert.Equal(utcStart, entry.EffectiveOccurredAt);
        Assert.Equal(TimeSpan.Zero, entry.StartAt.Offset);
        var afterImage = (await client.GetFromJsonAsync<AvailabilityDto>("/api/garages/1/availability", Json))!;
        Assert.Equal(before.Capacity, afterImage.Capacity);
        Assert.Equal(before.FreeLots, afterImage.FreeLots);
        Assert.Equal(before.OccupiedLots, afterImage.OccupiedLots);
        Assert.Equal(before.UnknownLots, afterImage.UnknownLots);
        Assert.Equal(before.Lots.ToArray(), afterImage.Lots.ToArray());
        Assert.Equal(before.ActiveVisits + 1, afterImage.ActiveVisits);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var visit = await db.Visits.AsNoTracking().SingleAsync();
            Assert.Equal(entry.VisitId, visit.Id);
            Assert.Equal(VisitStatus.Active, visit.Status);
            Assert.Null(visit.EndAt);
            Assert.Null(visit.ClosedAt);
            Assert.Null(visit.Cost);
            Assert.Empty(await db.Parkings.ToListAsync());
        }

        using var parked = await client.PostAsJsonAsync("/api/garages/1/lots/1/entries", new { visitId = (int?)null, occurredAt = parkingStart });
        parked.EnsureSuccessStatusCode();
        var parking = (await parked.Content.ReadFromJsonAsync<LotEntryDto>(Json))!;
        Assert.Null(parking.VisitId);
        Assert.Equal(parkingStart, parking.StartAt);
        Assert.Equal(parkingStart, parking.EffectiveOccurredAt);
        var activeParking = Assert.Single((await client.GetFromJsonAsync<ActiveParkingDto[]>("/api/garages/1/parkings/active", Json))!);
        Assert.Equal(parking.ParkingId, activeParking.ParkingId);
        Assert.Null(activeParking.VisitId);
        Assert.Null(activeParking.LicenseNumber);
        var activeVisit = Assert.Single((await client.GetFromJsonAsync<ActiveVisitDto[]>("/api/garages/1/visits/active", Json))!);
        Assert.Equal(entry.VisitId, activeVisit.VisitId);
        Assert.Null(activeVisit.LotId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            Assert.Equal(VisitStatus.Active, (await db.Visits.SingleAsync()).Status);
            var saved = await db.Parkings.SingleAsync();
            Assert.Equal(ParkingStatus.Active, saved.Status);
            Assert.Null(saved.VisitId);
            Assert.Null(saved.EndAt);
            Assert.Null(saved.ClosedAt);
            Assert.Equal(ParkingLotStatus.Occupied, (await db.ParkingLots.SingleAsync(x => x.Id == 1)).CurrentStatus);
        }

        using var released = await client.PostAsJsonAsync("/api/garages/1/lots/1/exit", new { occurredAt = parkingEnd });
        released.EnsureSuccessStatusCode();
        var release = (await released.Content.ReadFromJsonAsync<LotExitDto>(Json))!;
        Assert.Equal(parking.ParkingId, release.ParkingId);
        Assert.False(release.MissingParkingObservation);
        Assert.Equal(parkingEnd, release.EffectiveOccurredAt);
        Assert.Empty((await client.GetFromJsonAsync<ActiveParkingDto[]>("/api/garages/1/parkings/active", Json))!);
        Assert.Equal(entry.VisitId, Assert.Single((await client.GetFromJsonAsync<ActiveVisitDto[]>("/api/garages/1/visits/active", Json))!).VisitId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var visit = await db.Visits.SingleAsync();
            Assert.Equal(VisitStatus.Active, visit.Status);
            Assert.Null(visit.EndAt);
            Assert.Null(visit.ClosedAt);
            Assert.Null(visit.Cost);
        }

        using var exited = await client.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = " abc-123 ", occurredAt = visitEnd.ToOffset(TimeSpan.FromHours(-4)) });
        exited.EnsureSuccessStatusCode();
        var receipt = (await exited.Content.ReadFromJsonAsync<ReceiptDto>(Json))!;
        Assert.Equal(entry.VisitId, receipt.VisitId);
        Assert.Equal("ABC-123", receipt.LicenseNumber);
        Assert.Equal(utcStart, receipt.StartAt);
        Assert.Equal(visitEnd, receipt.EndAt);
        Assert.Equal(visitEnd, receipt.ClosedAt);
        Assert.Equal(visitEnd, receipt.EffectiveOccurredAt);
        Assert.Equal(TimeSpan.Zero, receipt.EndAt.Offset);
        Assert.Equal(TimeSpan.FromSeconds(seconds), receipt.Duration);
        Assert.Equal(expectedCents / 100m, receipt.Cost);
        Assert.Equal("EUR", receipt.Currency);
        Assert.True(receipt.GateOpened);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var visit = await db.Visits.AsNoTracking().SingleAsync();
            Assert.Equal(entry.VisitId, visit.Id);
            Assert.Equal("ABC-123", visit.LicenseNumber);
            Assert.Equal(VisitStatus.Completed, visit.Status);
            Assert.Equal(utcStart, visit.StartAt);
            Assert.Equal(TimeSpan.Zero, visit.StartAt.Offset);
            Assert.Equal(receipt.EndAt, visit.EndAt);
            Assert.Equal(receipt.ClosedAt, visit.ClosedAt);
            Assert.Equal(receipt.Cost, visit.Cost);
            Assert.Null(visit.ClosureReason);
            var saved = await db.Parkings.AsNoTracking().SingleAsync();
            Assert.Equal(parking.ParkingId, saved.Id);
            Assert.Equal(ParkingStatus.Completed, saved.Status);
            Assert.Null(saved.VisitId);
            Assert.Equal(parkingStart, saved.StartAt);
            Assert.Equal(TimeSpan.Zero, saved.StartAt.Offset);
            Assert.Equal(parkingEnd, saved.EndAt);
            Assert.Equal(parkingEnd, saved.ClosedAt);
            Assert.Equal(ParkingLotStatus.Free, (await db.ParkingLots.SingleAsync(x => x.Id == 1)).CurrentStatus);
        }

        var final = (await client.GetFromJsonAsync<AvailabilityDto>("/api/garages/1/availability", Json))!;
        Assert.Equal(before.ActiveVisits, final.ActiveVisits);
        Assert.Equal(before.Lots.ToArray(), final.Lots.ToArray());
        Assert.Empty((await client.GetFromJsonAsync<ActiveVisitDto[]>("/api/garages/1/visits/active", Json))!);
        Assert.Empty((await client.GetFromJsonAsync<ActiveParkingDto[]>("/api/garages/1/parkings/active", Json))!);
        Assert.Equal(1, reader.Calls);
    }

    private sealed class Reader : ILicensePlateReader
    {
        public bool IsAvailable => true;
        public int Calls { get; private set; }

        public Task<PlateReadResult> ReadAsync(ValidatedPlateImage image, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.Equal("image/png", image.MediaType);
            Calls++;
            return Task.FromResult(new PlateReadResult([" abc-123 "], false));
        }
    }
}
