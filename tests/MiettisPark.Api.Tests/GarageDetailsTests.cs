using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class GarageDetailsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task PricingPersistsRejectsStaleWritesAndProtectsActiveVisits()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        var original = (await client.GetFromJsonAsync<PricingDto>("/api/garages/1/pricing"))!;
        List<ParkingFeeTier> tiers = [new()
        {
            Amount = 2m,
            PerUnit = TimeSpan.FromMinutes(10),
            StartDuration = TimeSpan.Zero
        }];
        var saved = await client.PutAsJsonAsync("/api/garages/1/pricing", new PricingRequest(tiers, original.Tiers));
        saved.EnsureSuccessStatusCode();
        var reloaded = (await client.GetFromJsonAsync<PricingDto>("/api/garages/1/pricing"))!;
        Assert.Equal(2m, Assert.Single(reloaded.Tiers).Amount);
        var stale = await client.PutAsJsonAsync("/api/garages/1/pricing", new PricingRequest(tiers, original.Tiers));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        (await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "ABC-123" })).EnsureSuccessStatusCode();
        var active = await client.PutAsJsonAsync("/api/garages/1/pricing", new PricingRequest(original.Tiers, tiers));
        Assert.Equal(HttpStatusCode.Conflict, active.StatusCode);
        Assert.Contains("ACTIVE_VISITS", await active.Content.ReadAsStringAsync());
        factory.Clock.Now += TimeSpan.FromMinutes(5);
        var exit = await client.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = "ABC-123" });
        exit.EnsureSuccessStatusCode();
        Assert.Equal(2m, (await exit.Content.ReadFromJsonAsync<ReceiptDto>())!.Cost);
        var history = (await client.GetFromJsonAsync<List<VisitHistoryDto>>("/api/garages/1/visits", JsonOptions))!;
        var visit = Assert.Single(history);
        Assert.Equal(VisitStatus.Completed, visit.Status);
        Assert.Equal(2m, visit.Cost);
        Assert.NotNull(visit.EndAt);
        (await client.PutAsJsonAsync("/api/garages/1/pricing", new PricingRequest(original.Tiers, tiers))).EnsureSuccessStatusCode();
        var unchanged = (await client.GetFromJsonAsync<List<VisitHistoryDto>>("/api/garages/1/visits", JsonOptions))!;
        Assert.Equal(2m, Assert.Single(unchanged).Cost);
    }

    [Fact]
    public async Task InvalidPricingAndHistoryPagingReturnClearErrors()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        var original = (await client.GetFromJsonAsync<PricingDto>("/api/garages/1/pricing"))!;
        var empty = await client.PutAsJsonAsync("/api/garages/1/pricing", new PricingRequest([], original.Tiers));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("INVALID_PRICING", await empty.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/garages/1/visits?skip=-1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/garages/1/visits?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/garages/999/visits")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<VisitHistoryDto>>("/api/garages/1/visits?skip=50", JsonOptions))!);
    }
}
