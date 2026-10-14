using ImageMagick;

namespace MiettisPark.Api.Services.PlateRecognition;

public sealed class ImageValidator
{
    public const int MinimumBytes = 1;
    public const int MaximumBytes = 8 * 1024 * 1024;
    public const int MaximumDimension = 8192;
    public const long MaximumPixels = 16_000_000;

    static ImageValidator()
    {
        ResourceLimits.Memory = 256UL * 1024 * 1024;
        ResourceLimits.MaxMemoryRequest = 256UL * 1024 * 1024;
        ResourceLimits.Disk = 0;
        ResourceLimits.Area = MaximumPixels;
        ResourceLimits.Width = MaximumDimension;
        ResourceLimits.Height = MaximumDimension;
        ResourceLimits.ListLength = 8;
        ResourceLimits.Thread = 1;
    }

    public Task<ValidatedPlateImage> ValidateAsync(byte[] bytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (bytes.Length > MaximumBytes)
        {
            throw new ParkingError("IMAGE_TOO_LARGE", 413, "Kuvan enimmäiskoko on 8 MiB.");
        }

        var png = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        if (!png && !jpeg || jpeg && (bytes[^2] != 255 || bytes[^1] != 217))
        {
            throw InvalidImage();
        }

        if (png)
        {
            ValidatePngChunks(bytes);
        }

        try
        {
            var format = png ? MagickFormat.Png : MagickFormat.Jpeg;
            var settings = new MagickReadSettings { Format = format };
            var info = new MagickImageInfo(bytes);
            if (info.Width == 0 || info.Height == 0 || info.Width > MaximumDimension || info.Height > MaximumDimension || (long)info.Width * info.Height > MaximumPixels)
            {
                throw InvalidImage();
            }

            using var decoded = new MagickImageCollection();
            decoded.Warning += (_, _) => throw InvalidImage();
            decoded.Read(bytes, settings);
            if (decoded.Count != 1)
            {
                throw InvalidImage();
            }
            var image = decoded[0];
            token.ThrowIfCancellationRequested();
            if (image.Width != info.Width || image.Height != info.Height || image.Format != format)
            {
                throw InvalidImage();
            }

            return Task.FromResult(new ValidatedPlateImage(bytes, png ? "image/png" : "image/jpeg"));
        }
        catch (MagickException)
        {
            throw InvalidImage();
        }
    }

    private static void ValidatePngChunks(byte[] bytes)
    {
        var offset = 8;
        while (offset <= bytes.Length - 12)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length > bytes.Length - offset - 12 || bytes.AsSpan(offset + 4, 4).SequenceEqual("acTL"u8))
            {
                throw InvalidImage();
            }

            if (bytes.AsSpan(offset + 4, 4).SequenceEqual("IEND"u8))
            {
                if (length != 0 || offset + 12 != bytes.Length)
                {
                    throw InvalidImage();
                }

                return;
            }

            offset += (int)length + 12;
        }

        throw InvalidImage();
    }

    private static ParkingError InvalidImage() => new("INVALID_IMAGE", 400, "Anna ehjä yhden kehyksen JPEG- tai PNG-kuva sallituissa mitoissa.");
}