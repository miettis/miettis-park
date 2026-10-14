using MiettisPark.Api.Data;

namespace MiettisPark.Api.Contracts;

public sealed record LicenseRequest(string? LicenseNumber);
public sealed record LotEntryRequest(int? VisitId);
public sealed record GarageDto(int Id, string Name, string? Address, double? Latitude, double? Longitude,
    int Capacity, int FreeLots, int OccupiedLots, int UnknownLots, bool IsFull);
public sealed record GarageRequest(string? Name, string? Address, double? Latitude, double? Longitude);
public sealed record LayoutLotDto(int Id, int Number, ParkingLotStatus CurrentStatus, bool CanRemove);
public sealed record LayoutDto(int GarageId, int Width, int Height, int Version, IReadOnlyList<LayoutCell> Cells, IReadOnlyList<LayoutLotDto> Lots);
public sealed record LayoutCellRequest(int X, int Y, LayoutCellType Type, int? LotId, int? Number);
public sealed record LayoutRequest(int ExpectedVersion, int Width, int Height, List<LayoutCellRequest>? Cells, List<int>? RemovedLotIds);
public sealed record LotDto(int Id, int Number, ParkingLotStatus CurrentStatus);
public sealed record AvailabilityDto(int Capacity, int ActiveVisits, int FreeLots, int OccupiedLots, int UnknownLots, bool IsFull, IReadOnlyList<LotDto> Lots);
public sealed record ActiveVisitDto(int VisitId, string LicenseNumber, DateTimeOffset StartAt, int? LotId, int? LotNumber);
public sealed record ActiveParkingDto(int ParkingId, int LotId, int LotNumber, int? VisitId, string? LicenseNumber, DateTimeOffset StartAt);
public enum EntryOutcome
{
    Entry,
    Duplicate,
    RecoveredMissingExit
}
public sealed record EntryDto(int VisitId, string LicenseNumber, DateTimeOffset StartAt, EntryOutcome Outcome, bool GateOpened = true);
public sealed record LotEntryDto(int ParkingId, int LotId, int? VisitId, DateTimeOffset StartAt);
public sealed record LotExitDto(int LotId, int? ParkingId, bool MissingParkingObservation);
public sealed record ReceiptDto(int VisitId, string LicenseNumber, DateTimeOffset StartAt, DateTimeOffset EndAt, DateTimeOffset ClosedAt, TimeSpan Duration, decimal Cost, string Currency = "EUR", bool GateOpened = true);
