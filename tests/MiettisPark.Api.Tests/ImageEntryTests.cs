using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;
using MiettisPark.Api.Services.PlateRecognition;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class ImageEntryTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    private static byte[] ImageBytes(MagickFormat format = MagickFormat.Png)
    {
        using var image = new MagickImage(MagickColors.White, 2, 2);
        return image.ToByteArray(format);
    }

    private static MultipartFormDataContent Upload(byte[]? bytes = null, string? time = null)
    {
        var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent(bytes ?? ImageBytes()), "image", "misleading.gif");
        if (time != null)
        {
            body.Add(new StringContent(time), "occurredAt");
        }

        return body;
    }

    private sealed class Reader : ILicensePlateReader
    {
        public bool IsAvailable { get; set; } = true;
        public int Calls;
        public Func<CancellationToken, Task<PlateReadResult>> Action { get; set; } = _ => Task.FromResult(new PlateReadResult([" abc-123 "], false));
        public Task<PlateReadResult> ReadAsync(ValidatedPlateImage image, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            return Action(token);
        }
    }

    [Fact]
    public async Task ApiAutomaticallyEntersDuplicatesAndRecoversWithoutChangingParking()
    {
        var reader = new Reader();
        using var factory = new ApiFactory { PlateReader = reader };
        using var client = factory.CreateSeededClient();
        using var first = Upload(time: "2026-10-14T13:00:00+03:00");
        var response = await client.PostAsync("/api/garages/1/entries/from-image", first);
        response.EnsureSuccessStatusCode();
        var entry = (await response.Content.ReadFromJsonAsync<ImageEntryDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }))!;
        Assert.Equal("ABC-123", entry.RecognizedLicenseNumber);
        Assert.Equal(Start, entry.EffectiveOccurredAt);
        Assert.Equal(EntryOutcome.Entry, entry.Outcome);
        (await client.PostAsJsonAsync("/api/garages/1/lots/1/entries", new { visitId = entry.VisitId, occurredAt = Start })).EnsureSuccessStatusCode();
        using var duplicate = Upload(time: Start.AddMinutes(2).ToString("O"));
        var repeated = await client.PostAsync("/api/garages/1/entries/from-image", duplicate);
        repeated.EnsureSuccessStatusCode();
        var dup = (await repeated.Content.ReadFromJsonAsync<ImageEntryDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }))!;
        Assert.Equal(EntryOutcome.Duplicate, dup.Outcome);
        Assert.Equal(entry.VisitId, dup.VisitId);
        using var recovery = Upload(time: Start.AddMinutes(2).AddTicks(1).ToString("O"));
        var recovered = await client.PostAsync("/api/garages/1/entries/from-image", recovery);
        recovered.EnsureSuccessStatusCode();
        Assert.Equal(EntryOutcome.RecoveredMissingExit, (await recovered.Content.ReadFromJsonAsync<ImageEntryDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }))!.Outcome);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Single(db.Visits.Where(x => x.Status == VisitStatus.Active));
        Assert.Single(db.Parkings.Where(x => x.VisitId == entry.VisitId && x.Status == ParkingStatus.Active));
        Assert.Equal(ParkingLotStatus.Occupied, db.ParkingLots.Single(x => x.Id == 1).CurrentStatus);
        Assert.Equal(3, reader.Calls);
    }

    [Fact]
    public async Task ServiceFreezesTimeBeforeReaderWithoutTransactionAndRevalidatesAfterDelay()
    {
        using var store = new TestStore();
        var reader = new Reader();
        var service = new ImageEntryService(store.Database, store.Service, reader);
        store.Clock.Now = Start;
        var effective = await service.PreflightAsync(1, null, default);
        reader.Action = _ =>
        {
            Assert.Empty(store.Transactions.Events);
            store.Clock.Now = Start.AddHours(3);
            return Task.FromResult(new PlateReadResult(["abc-123"], false));
        };
        var entry = await service.EnterAsync(1, new ValidatedPlateImage([1], "image/png"), effective, default);
        Assert.Equal(Start, entry.StartAt);
        Assert.Equal(Start, entry.EffectiveOccurredAt);
        Assert.Equal(1, store.Clock.Reads);
        reader.Action = async _ =>
        {
            await store.Service.EnterGarageAsync(1, "ABC-123", Start.AddHours(1), default);
            return new PlateReadResult(["ABC-123"], false);
        };
        var error = await Assert.ThrowsAsync<ParkingError>(() => service.EnterAsync(1, new ValidatedPlateImage([1], "image/png"), effective, default));
        Assert.Equal("EVENT_TIME_CONFLICT", error.Code);
        Assert.Equal(Start.AddHours(1), error.MinimumOccurredAt);
        Assert.Single(store.Database.Visits.Where(x => x.Status == VisitStatus.Active));
    }

    [Theory]
    [InlineData("", 1, true, "INVALID_REQUEST")]
    [InlineData("2026-10-14T10:00:00", 1, true, "INVALID_REQUEST")]
    [InlineData("bad", 1, true, "INVALID_REQUEST")]
    [InlineData("2026-10-14T10:00:00Z", 1, false, "SIMULATION_TIME_DISABLED")]
    [InlineData(null, 999, true, "GARAGE_NOT_FOUND")]
    public async Task PreflightErrorsNeverInvokeReader(string? time, int garage, bool enabled, string code)
    {
        var reader = new Reader();
        using var factory = new ApiFactory { PlateReader = reader, SimulationEnabled = enabled };
        using var client = factory.CreateSeededClient();
        using var body = Upload(time: time);
        await Problem(await client.PostAsync($"/api/garages/{garage}/entries/from-image", body), code);
        Assert.Equal(0, reader.Calls);
        Empty(factory);
    }

    [Fact]
    public async Task MissingConfigurationDoesNotBlockStartupOpenApiOrManualEntry()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        using var body = Upload();
        await Problem(await client.PostAsync("/api/garages/1/entries/from-image", body), "PLATE_RECOGNITION_UNAVAILABLE", 503);
        (await client.GetAsync("/openapi/v1.json")).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "manual" })).EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData(0, false, "PLATE_NOT_FOUND")]
    [InlineData(1, true, "PLATE_AMBIGUOUS")]
    [InlineData(2, false, "MULTIPLE_PLATES")]
    public async Task UnreadableNeverWrites(int count, bool ambiguous, string code)
    {
        var reader = new Reader { Action = _ => Task.FromResult(new PlateReadResult(new[] { "ABC", "DEF" }.Take(count).ToArray(), ambiguous)) };
        using var factory = new ApiFactory { PlateReader = reader };
        using var client = factory.CreateSeededClient();
        using var body = Upload();
        await Problem(await client.PostAsync("/api/garages/1/entries/from-image", body), code, 422);
        Empty(factory);
    }

    [Theory]
    [InlineData("PLATE_PROVIDER_INVALID_RESPONSE", 502)]
    [InlineData("PLATE_PROVIDER_PAYMENT_REQUIRED", 502)]
    [InlineData("PLATE_PROVIDER_RATE_LIMITED", 502)]
    [InlineData("PLATE_PROVIDER_ERROR", 502)]
    [InlineData("PLATE_PROVIDER_TIMEOUT", 504)]
    public async Task ProviderFailuresNeverWrite(string code, int status)
    {
        var reader = new Reader { Action = _ => throw new ParkingError(code, status, "Tunnistus epäonnistui.") };
        using var factory = new ApiFactory { PlateReader = reader };
        using var client = factory.CreateSeededClient();
        using var body = Upload();
        await Problem(await client.PostAsync("/api/garages/1/entries/from-image", body), code, status);
        Assert.Equal(1, reader.Calls);
        Empty(factory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoordinatedImagesAndManualRequestKeepOneActive(bool manual)
    {
        using var store = new TestStore();
        store.Coordinator.ArmPair();
        using var db1 = store.NewContext();
        using var db2 = store.NewContext();
        var reader = new Reader();
        var service1 = new ImageEntryService(db1, store.NewService(db1), reader);
        var service2 = new ImageEntryService(db2, store.NewService(db2), reader);
        var effective1 = await service1.PreflightAsync(1, Start, default);
        var effective2 = await service2.PreflightAsync(1, Start, default);
        var image = new ValidatedPlateImage([1], "image/png");
        var first = service1.EnterAsync(1, image, effective1, default);
        Task second = manual ? store.NewService(db2).EnterGarageAsync(1, "ABC-123", effective2, default) : service2.EnterAsync(1, image, effective2, default);
        await Task.WhenAll(first, second);
        Assert.Single(store.Database.Visits.Where(x => x.Status == VisitStatus.Active));
    }

    [Fact]
    public async Task CancellationRollbackAndBusyAdmissionDoNotWrite()
    {
        using var store = new TestStore();
        var reader = new Reader();
        var service = new ImageEntryService(store.Database, store.Service, reader);
        using var cancellation = new CancellationTokenSource();
        reader.Action = _ => { cancellation.Cancel(); return Task.FromResult(new PlateReadResult(["abc"], false)); };
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.EnterAsync(1, new ValidatedPlateImage([1], "image/png"), Start, cancellation.Token));
        Assert.Empty(store.Database.Visits);
        reader.Action = _ => Task.FromResult(new PlateReadResult(["abc"], false));
        store.Transactions.CommitFailure = new InvalidOperationException("test");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnterAsync(1, new ValidatedPlateImage([1], "image/png"), Start, default));
        Assert.Empty(store.Database.Visits);
        var limiter = new ImageOperationLimiter();
        using var one = limiter.Acquire();
        using (var two = limiter.Acquire())
        {
            Assert.Equal("PLATE_RECOGNITION_BUSY", Assert.Throws<ParkingError>(() => limiter.Acquire()).Code);
        }
        using var replacement = limiter.Acquire();
    }

    [Fact]
    public async Task DecoderRejectsTruncatedInvalidOversizedAndAnimatedImages()
    {
        var validator = new ImageValidator();
        foreach (var format in new[] { MagickFormat.Png, MagickFormat.Jpeg })
        {
            var bytes = ImageBytes(format);
            Assert.Equal(format == MagickFormat.Png ? "image/png" : "image/jpeg", (await validator.ValidateAsync(bytes, default)).MediaType);
            await Assert.ThrowsAsync<ParkingError>(() => validator.ValidateAsync(bytes[..^8], default));
        }

        await Assert.ThrowsAsync<ParkingError>(() => validator.ValidateAsync(new byte[8 * 1024 * 1024 + 1], default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoordinatedApiImageAndManualWritesShareExistingAtomicEntry(bool manual)
    {
        using var factory = new ApiFactory { PlateReader = new Reader() };
        using var client = factory.CreateSeededClient();
        factory.Services.GetRequiredService<TestCoordinator>().ArmPair();
        using var first = Upload(time: Start.ToString("O"));
        using var second = Upload(time: Start.ToString("O"));
        var one = client.PostAsync("/api/garages/1/entries/from-image", first);
        var two = manual ? client.PostAsJsonAsync("/api/garages/1/entries", new { licenseNumber = "ABC-123", occurredAt = Start }) : client.PostAsync("/api/garages/1/entries/from-image", second);
        var responses = await Task.WhenAll(one, two);
        Assert.All(responses, response => response.EnsureSuccessStatusCode());
        using var scope = factory.Services.CreateScope();
        Assert.Single(scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Visits.Where(x => x.Status == VisitStatus.Active));
    }

    [Fact]
    public async Task DecoderChecksPixelsFramesAndCorruptContentNotMimeOrExtension()
    {
        var validator = new ImageValidator();
        var png = ImageBytes();
        var animated = png.ToArray();
        "acTL"u8.CopyTo(animated.AsSpan(12, 4));
        await Assert.ThrowsAsync<ParkingError>(() => validator.ValidateAsync(animated, default));
        foreach (var (width, height) in new[] { (8193, 1), (4096, 4096) })
        {
            var huge = png.ToArray();
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(huge.AsSpan(16, 4), width);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(huge.AsSpan(20, 4), height);
            uint crc = uint.MaxValue;
            foreach (var b in huge.AsSpan(12, 17))
            {
                crc ^= b;
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xEDB88320u : 0);
                }
            }
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(huge.AsSpan(29, 4), ~crc);
            await Assert.ThrowsAsync<ParkingError>(() => validator.ValidateAsync(huge, default));
        }
        await Assert.ThrowsAsync<ParkingError>(() => validator.ValidateAsync([255, 216, 255, 217], default));
        await Assert.ThrowsAsync<ParkingError>(() => validator.ValidateAsync("GIF89a"u8.ToArray(), default));
        await Assert.ThrowsAsync<ParkingError>(() => validator.ValidateAsync([], default));
    }

    [Fact]
    public async Task OpenApiExposesMultipartAndAllImageResponseContractsWithoutProvider()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSeededClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var operation = json.RootElement.GetProperty("paths").GetProperty("/api/garages/{garageId}/entries/from-image").GetProperty("post");
        var schema = operation.GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema");
        Assert.Equal("binary", schema.GetProperty("properties").GetProperty("image").GetProperty("format").GetString());
        Assert.Equal("date-time", schema.GetProperty("properties").GetProperty("occurredAt").GetProperty("format").GetString());
        foreach (var status in new[] { "200", "400", "404", "409", "413", "422", "502", "503", "504" })
        {
            Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        }
        Assert.True(json.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ImageEntryDto").GetProperty("properties").TryGetProperty("recognizedLicenseNumber", out _));
    }
    private static void Empty(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Empty(db.Visits);
        Assert.Empty(db.Parkings);
    }

    private static async Task Problem(HttpResponseMessage response, string code, int? status = null)
    {
        if (status != null)
        {
            Assert.Equal(status, (int)response.StatusCode);
        }
        Assert.False(response.IsSuccessStatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(json.RootElement.GetProperty("gateOpened").GetBoolean());
    }
}