using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace MiettisPark.Api.Services.PlateRecognition;

public sealed record ImageUpload(byte[] Bytes, DateTimeOffset? OccurredAt);

public sealed class ImageUploadReader
{
    public const int MaximumImageBytes = 8 * 1024 * 1024;
    public const int MaximumRequestBytes = MaximumImageBytes + 64 * 1024;

    public async Task<ImageUpload> ReadAsync(HttpRequest request, CancellationToken token)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) || !string.Equals(contentType.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidUpload();
        }

        var boundary = HeaderUtilities.RemoveQuotes(contentType.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Name.Value, "boundary", StringComparison.OrdinalIgnoreCase))?.Value ?? default).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > 128)
        {
            throw InvalidUpload();
        }

        if (request.ContentLength > MaximumRequestBytes)
        {
            throw TooLarge();
        }

        var boundedBody = new BoundedReadStream(request.Body, MaximumRequestBytes);
        var reader = new MultipartReader(boundary, boundedBody) { HeadersCountLimit = 8, HeadersLengthLimit = 4096, BodyLengthLimit = MaximumRequestBytes };
        byte[]? image = null;
        DateTimeOffset? occurredAt = null;
        var occurredAtSeen = false;

        try
        {
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(token)) is not null)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) || !string.Equals(disposition.DispositionType.Value, "form-data", StringComparison.OrdinalIgnoreCase))
                {
                    throw InvalidUpload();
                }

                var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;
                if (string.Equals(name, "image", StringComparison.Ordinal))
                {
                    if (image is not null || !disposition.FileName.HasValue && !disposition.FileNameStar.HasValue)
                    {
                        throw InvalidUpload();
                    }

                    image = await ReadBoundedAsync(section.Body, MaximumImageBytes, token);
                    if (image.Length == 0)
                    {
                        throw InvalidUpload();
                    }
                }
                else if (string.Equals(name, "occurredAt", StringComparison.Ordinal))
                {
                    if (occurredAtSeen)
                    {
                        throw InvalidUpload();
                    }

                    occurredAtSeen = true;
                    var value = await ReadTextBoundedAsync(section.Body, 128, token);
                    if (!MiettisPark.Api.Contracts.OccurredAtJsonConverter.TryParse(value, out var parsed))
                    {
                        throw new ParkingError("INVALID_REQUEST", 400, "Tapahtuma-ajan on oltava ISO 8601 -aika offsetilla.");
                    }

                    occurredAt = parsed;
                }
                else
                {
                    throw InvalidUpload();
                }
            }

            var drainBuffer = new byte[8192];
            while (await boundedBody.ReadAsync(drainBuffer, token) > 0)
            {
            }
        }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            throw TooLarge();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException)
        {
            throw InvalidUpload();
        }

        return image is null ? throw InvalidUpload() : new ImageUpload(image, occurredAt);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, CancellationToken token)
    {
        using var output = new MemoryStream(Math.Min(limit, 64 * 1024));
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, token);
            if (count == 0)
            {
                break;
            }

            if (output.Length + count > limit)
            {
                throw limit == MaximumImageBytes ? TooLarge() : InvalidUpload();
            }

            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }

        return output.ToArray();
    }

    private static async Task<string> ReadTextBoundedAsync(Stream stream, int limit, CancellationToken token)
    {
        var bytes = await ReadBoundedAsync(stream, limit, token);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static ParkingError TooLarge() => new("IMAGE_TOO_LARGE", 413, "Kuvan enimmäiskoko on 8 MiB.");

    private static ParkingError InvalidUpload() => new("INVALID_REQUEST", 400, "Lähetä yksi multipart-kuva ja kelvollinen tapahtuma-aika.");

    private sealed class BoundedReadStream(Stream inner, long maximum) : Stream
    {
        private long read;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => Account(inner.Read(buffer, offset, count));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => Account(await inner.ReadAsync(buffer, cancellationToken));
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAndAccountAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();


        private int Account(int count)
        {
            read += count;
            if (read > maximum)
            {
                throw TooLarge();
            }

            return count;
        }

        private async Task<int> ReadAndAccountAsync(byte[] buffer, int offset, int count, CancellationToken token) => Account(await inner.ReadAsync(buffer.AsMemory(offset, count), token));
    }
}
