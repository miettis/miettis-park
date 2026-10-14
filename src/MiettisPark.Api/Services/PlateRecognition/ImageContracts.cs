namespace MiettisPark.Api.Services.PlateRecognition;

public sealed record ValidatedPlateImage(byte[] Bytes, string MediaType);

public interface ILicensePlateReader
{
    bool IsAvailable { get; }

    Task<PlateReadResult> ReadAsync(ValidatedPlateImage image, CancellationToken token);
}

public sealed record PlateReadResult(IReadOnlyList<string> Plates, bool Ambiguous);

public sealed class PlateRecognitionOptions
{
    public string? ApiKey { get; set; }

    public string Model { get; set; } = string.Empty;
}
