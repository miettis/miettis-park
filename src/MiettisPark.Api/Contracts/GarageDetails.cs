using MiettisPark.Api.Data;

namespace MiettisPark.Api.Contracts;

public sealed record VisitHistoryDto(
    int VisitId,
    string LicenseNumber,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    DateTimeOffset? ClosedAt,
    VisitStatus Status,
    string? ClosureReason,
    decimal? Cost);

public sealed record PricingDto(int GarageId, List<ParkingFeeTier> Tiers);

public sealed record PricingRequest(
    List<ParkingFeeTier> Tiers,
    List<ParkingFeeTier> ExpectedTiers);
