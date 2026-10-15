using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using MiettisPark.Api.Services;
using MiettisPark.Api.Services.PlateRecognition;
using Microsoft.Extensions.Options;
using ImageMagick;


using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class PlateRecognitionTests
{
    [Fact]
    public async Task ValidatorFullyDecodesSupportedJpegAndRejectsBytesOutsideLimits()
    {
        var bytes = CreateJpeg();
        var validator = new ImageValidator();

        var result = await validator.ValidateAsync(bytes, CancellationToken.None);

        Assert.Equal("image/jpeg", result.MediaType);
        Assert.Equal(bytes, result.Bytes);
        await Assert.ThrowsAsync<ParkingError>(() => validator.ValidateAsync(Array.Empty<byte>(), CancellationToken.None));
        await Assert.ThrowsAsync<ParkingError>(() => validator.ValidateAsync(new byte[ImageValidator.MaximumBytes + 1], CancellationToken.None));
        await Assert.ThrowsAsync<ParkingError>(() => validator.ValidateAsync(new byte[ImageValidator.MinimumBytes], CancellationToken.None));
    }

    [Fact]
    public async Task UploadReaderStreamsOneImageAndOptionalOccurredAtWithoutUsingAFile()
    {
        using var multipart = new MultipartFormDataContent("image-boundary");
        var imageContent = new ByteArrayContent(new byte[1024]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        multipart.Add(imageContent, "image", "plate.jpg");
        multipart.Add(new StringContent("2026-10-14T13:30:45+03:00"), "occurredAt");
        using var body = new MemoryStream();
        await multipart.CopyToAsync(body);
        var context = new DefaultHttpContext();
        context.Request.ContentType = multipart.Headers.ContentType!.ToString();
        context.Request.ContentLength = body.Length;
        context.Request.Body = new MemoryStream(body.ToArray());

        var result = await new ImageUploadReader().ReadAsync(context.Request, CancellationToken.None);

        Assert.Equal(1024, result.Bytes.Length);
        Assert.Equal(DateTimeOffset.Parse("2026-10-14T13:30:45+03:00"), result.OccurredAt);
    }

    [Fact]
    public async Task UploadReaderRejectsMissingFileAndOversizedRequest()
    {
        var reader = new ImageUploadReader();
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        await Assert.ThrowsAsync<ParkingError>(() => reader.ReadAsync(context.Request, CancellationToken.None));
        context.Request.ContentType = "multipart/form-data; boundary=x";
        context.Request.ContentLength = ImageUploadReader.MaximumRequestBytes + 1;
        await Assert.ThrowsAsync<ParkingError>(() => reader.ReadAsync(context.Request, CancellationToken.None));
    }

    [Fact]
    public async Task OpenRouterReaderUsesStrictSchemaImageDataAndNormalizesEveryCandidate()
    {
        var handler = new StubHandler(_ => JsonResponse("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"{\\\"plates\\\":[\\\" abc-123 \\\"],\\\"ambiguous\\\":false}\"}}]}"));
        using var client = new HttpClient(handler);
        var reader = new OpenRouterLicensePlateReader(client, Options.Create(new PlateRecognitionOptions { ApiKey = "unit-test-key", Model = "provider/model" }));
        var image = new ValidatedPlateImage([1, 2, 3], "image/png");

        var result = await reader.ReadAsync(image, CancellationToken.None);

        Assert.Equal(new[] { "ABC-123" }, result.Plates);
        Assert.False(result.Ambiguous);
        using var sent = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("provider/model", sent.RootElement.GetProperty("model").GetString());
        Assert.Equal("json_schema", sent.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        Assert.Contains("data:image/png;base64,AQID", handler.RequestBody);
        Assert.Equal("Bearer", handler.Authorization!.Scheme);
        Assert.Equal("unit-test-key", handler.Authorization.Parameter);
    }

    [Theory]
    [InlineData("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"{}\"}}]}")]
    [InlineData("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"{\\\"plates\\\":[\\\"\\\"],\\\"ambiguous\\\":false}\"}}]}")]
    public async Task OpenRouterReaderFailsClosedOnIncompleteOrInvalidModelOutput(string response)
    {
        using var client = new HttpClient(new StubHandler(_ => JsonResponse(response)));
        var reader = new OpenRouterLicensePlateReader(client, Options.Create(new PlateRecognitionOptions { ApiKey = "test", Model = "provider/model" }));

        await Assert.ThrowsAsync<ParkingError>(() => reader.ReadAsync(new ValidatedPlateImage([0], "image/jpeg"), CancellationToken.None));
    }

    [Fact]
    public async Task OpenRouterReaderIsUnavailableWithoutCredentialsAndBoundsResponse()
    {
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[64 * 1024 + 1]) }));
        var unavailable = new OpenRouterLicensePlateReader(client, Options.Create(new PlateRecognitionOptions()));
        Assert.False(unavailable.IsAvailable);
        await Assert.ThrowsAsync<ParkingError>(() => unavailable.ReadAsync(new ValidatedPlateImage([0], "image/jpeg"), CancellationToken.None));

        var configured = new OpenRouterLicensePlateReader(client, Options.Create(new PlateRecognitionOptions { ApiKey = "test", Model = "provider/model" }));
        await Assert.ThrowsAsync<ParkingError>(() => configured.ReadAsync(new ValidatedPlateImage([0], "image/jpeg"), CancellationToken.None));
    }

    private static byte[] CreateJpeg()
    {
        using var image = new MagickImage(MagickColors.White, 2, 2);
        return image.ToByteArray(MagickFormat.Jpeg);
    }
    private static HttpResponseMessage JsonResponse(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization;
            return response(request);
        }
    }
}
