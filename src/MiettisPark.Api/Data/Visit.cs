namespace MiettisPark.Api.Data;

public sealed class Visit
{
    public int Id { get; set; }
    public int ParkingGarageId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Active;
    public string? ClosureReason { get; set; }
    public decimal? Cost { get; set; }
    public ParkingGarage ParkingGarage { get; set; } = null!;
    public ICollection<Parking> Parkings { get; set; } = [];
}

public enum VisitStatus
{
    Active,
    Completed,
    Interrupted
}
