namespace MiettisPark.Api.Data;

public sealed class ParkingGarage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ParkingFeeTier> ParkingFeeConfiguration { get; set; } = [];
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int LayoutWidth { get; set; } = 1;
    public int LayoutHeight { get; set; } = 1;
    public int LayoutVersion { get; set; } = 1;
    public List<LayoutCell> LayoutCells { get; set; } = [new(0, 0, LayoutCellType.Empty, null)];
    public ICollection<ParkingLot> Lots { get; set; } = [];
    public ICollection<Visit> Visits { get; set; } = [];
}
