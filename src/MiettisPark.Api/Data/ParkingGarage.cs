namespace MiettisPark.Api.Data;

public sealed class ParkingGarage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ParkingFeeTier> ParkingFeeConfiguration { get; set; } = [];
    public ICollection<ParkingLot> Lots { get; set; } = [];
    public ICollection<Visit> Visits { get; set; } = [];
}
