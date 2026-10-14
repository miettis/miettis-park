namespace MiettisPark.Api.Data;

public sealed class ParkingLot
{
    public int Id { get; set; }
    public int ParkingGarageId { get; set; }
    public int Number { get; set; }
    public ParkingLotStatus CurrentStatus { get; set; } = ParkingLotStatus.Free;
    public ParkingGarage ParkingGarage { get; set; } = null!;
    public ICollection<Parking> Parkings { get; set; } = [];
}

public enum ParkingLotStatus
{
    Free,
    Occupied,
    Unknown
}
