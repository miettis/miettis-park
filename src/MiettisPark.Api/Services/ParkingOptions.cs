namespace MiettisPark.Api.Services;

public sealed class ParkingOptions
{
    public TimeSpan DuplicateEntryWindow { get; set; } = TimeSpan.FromMinutes(2);
}
