namespace MiettisPark.Api.Data;

public enum LayoutCellType
{
    Empty,
    Road,
    ParkingLot
}

public sealed record LayoutCell(int X, int Y, LayoutCellType Type, int? LotId);

public static class DefaultParkingFees
{
    public static List<ParkingFeeTier> Create() =>
    [
        new() { Amount = 0.50m, PerUnit = TimeSpan.FromMinutes(10), StartDuration = TimeSpan.Zero, EndDuration = TimeSpan.FromHours(3) },
        new() { Amount = 0.30m, PerUnit = TimeSpan.FromMinutes(10), StartDuration = TimeSpan.FromHours(3) }
    ];
}
