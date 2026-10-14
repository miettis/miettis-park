namespace MiettisPark.Api.Data;

public sealed class ParkingFeeTier
{
    public decimal Amount { get; set; }
    public TimeSpan PerUnit { get; set; }
    public TimeSpan StartDuration { get; set; }
    public TimeSpan? EndDuration { get; set; }
}
