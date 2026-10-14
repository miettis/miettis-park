using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiettisPark.Api.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MiettisPark.Api.Data;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class ApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    [Fact]
    public async Task HttpPipelineRunsNormalFlowAndReloadWithNumericReceipt()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        Assert.Single((await client.GetFromJsonAsync<List<GarageDto>>("/api/garages"))!);
        var entryResponse = await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = " api-123 ", startAt = "1900-01-01T00:00:00Z" });
        entryResponse.EnsureSuccessStatusCode();
        var entry = (await entryResponse.Content.ReadFromJsonAsync<EntryDto>(JsonOptions))!;
        Assert.Equal(factory.Clock.Now, entry.StartAt);
        Assert.Equal("API-123", entry.LicenseNumber);
        (await client.PostAsJsonAsync("/api/garages/1/lots/1/entries", new { visitId = entry.VisitId })).EnsureSuccessStatusCode();
        using var reloaded = factory.CreateClient();
        Assert.Equal(entry.VisitId, (await reloaded.GetFromJsonAsync<List<ActiveVisitDto>>("/api/garages/1/visits/active"))!.Single().VisitId);
        Assert.Equal(1, (await reloaded.GetFromJsonAsync<List<ActiveParkingDto>>("/api/garages/1/parkings/active"))!.Single().LotId);
        (await reloaded.PostAsync("/api/garages/1/lots/1/exit", null)).EnsureSuccessStatusCode();
        (await reloaded.PostAsJsonAsync("/api/garages/1/lots/2/entries", new { visitId = entry.VisitId })).EnsureSuccessStatusCode();
        factory.Clock.Now += TimeSpan.FromMinutes(10) + TimeSpan.FromTicks(1);
        var exit = await reloaded.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = " api-123 " });
        exit.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await exit.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Number, json.RootElement.GetProperty("cost").ValueKind);
        Assert.Equal(1m, json.RootElement.GetProperty("cost").GetDecimal());
        Assert.Equal("EUR", json.RootElement.GetProperty("currency").GetString());
        var repeated = await reloaded.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = "API-123" });
        await AssertProblem(repeated, HttpStatusCode.Conflict, "ACTIVE_VISIT_NOT_FOUND", true);
        Assert.Single((await reloaded.GetFromJsonAsync<List<ActiveParkingDto>>("/api/garages/1/parkings/active"))!);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"visitId\":null}")]
    public async Task EmptyAndNullVisitBodiesAllowIndependentParking(string body)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        foreach (var id in new[] { 1, 2 })
        {
            var response = await client.PostAsync($"/api/garages/1/lots/{id}/entries", new StringContent(body, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Null((await response.Content.ReadFromJsonAsync<LotEntryDto>())!.VisitId);
        }

        Assert.Equal(2, (await client.GetFromJsonAsync<List<ActiveParkingDto>>("/api/garages/1/parkings/active"))!.Count);
    }

    [Theory]
    [InlineData("entries", "{}", "INVALID_LICENSE_NUMBER")]
    [InlineData("exits", "{\"licenseNumber\":null}", "INVALID_LICENSE_NUMBER")]
    [InlineData("entries", "{\"licenseNumber\":\"   \"}", "INVALID_LICENSE_NUMBER")]
    [InlineData("exits", "{\"licenseNumber\":\"123456789012345678901\"}", "INVALID_LICENSE_NUMBER")]
    [InlineData("entries", "{broken", "INVALID_REQUEST")]
    [InlineData("entries", "null", "INVALID_REQUEST")]
    [InlineData("lots/1/entries", "{\"visitId\":\"bad\"}", "INVALID_REQUEST")]
    public async Task InvalidInputHasSafeUniformProblemDetails(string path, string body, string code)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        var response = await client.PostAsync($"/api/garages/1/{path}", new StringContent(body, Encoding.UTF8, "application/json"));
        await AssertProblem(response, HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task ApiReturnsNotFoundAndConflictWithClosedGate()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        await AssertProblem(await client.GetAsync("/api/garages/999/availability"), HttpStatusCode.NotFound, "GARAGE_NOT_FOUND");
        await AssertProblem(await client.PostAsJsonAsync("/api/garages/1/lots/999/entries", new { }), HttpStatusCode.NotFound, "LOT_NOT_FOUND");
        await AssertProblem(await client.PostAsJsonAsync("/api/garages/1/lots/1/entries", new { visitId = 999 }), HttpStatusCode.NotFound, "VISIT_NOT_FOUND");
        (await client.PostAsJsonAsync("/api/garages/1/lots/1/entries", new { })).EnsureSuccessStatusCode();
        await AssertProblem(await client.PostAsJsonAsync("/api/garages/1/lots/1/entries", new { }), HttpStatusCode.Conflict, "LOT_UNAVAILABLE");
    }

    [Fact]
    public async Task UnexpectedFailureReturnsGenericProblemWithoutInternalDetails()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        (await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "FAIL" })).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var garage = await database.ParkingGarages.SingleAsync();
            garage.ParkingFeeConfiguration.Clear();
            await database.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/garages/1/exits", new { licenseNumber = "FAIL" });
        await AssertProblem(response, HttpStatusCode.InternalServerError, "INTERNAL_ERROR");
        Assert.DoesNotContain("configuration", await response.Content.ReadAsStringAsync());
        Assert.Single((await client.GetFromJsonAsync<List<ActiveVisitDto>>("/api/garages/1/visits/active"))!);
    }
    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code, bool requiresService = false)
    {
        Assert.Equal(status, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(json.RootElement.GetProperty("gateOpened").GetBoolean());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        if (requiresService)
        {
            Assert.True(json.RootElement.GetProperty("requiresCustomerService").GetBoolean());
        }

        Assert.DoesNotContain("SELECT", text);
        Assert.DoesNotContain("StackTrace", text);
        Assert.DoesNotContain("Npgsql", text);
        Assert.DoesNotContain("JsonException", text);
    }
}
