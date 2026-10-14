namespace MiettisPark.Api.Data;

public sealed class Parking
{
    public int Id { get; set; }
    public int? VisitId { get; set; }
    public int ParkingLotId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public ParkingStatus Status { get; set; } = ParkingStatus.Active;
    public Visit? Visit { get; set; }
    public ParkingLot ParkingLot { get; set; } = null!;
}

public enum ParkingStatus
{
    Active,
    Completed,
    Interrupted
}
