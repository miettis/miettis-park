using Microsoft.EntityFrameworkCore;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using Npgsql;
using Microsoft.Extensions.Options;

namespace MiettisPark.Api.Services;

public sealed class ParkingService(ParkingDbContext database, TimeProvider clock, ParkingFeeCalculator fees, IParkingWriteTransactionFactory transactions, IOptions<ParkingOptions> options)
{
    public async Task<List<GarageDto>> GetGaragesAsync(CancellationToken cancellationToken)
    {
        return await database.ParkingGarages.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new GarageDto(x.Id, x.Name)).ToListAsync(cancellationToken);
    }

    public async Task<AvailabilityDto> GetAvailabilityAsync(int garageId, CancellationToken cancellationToken)
    {
        await RequireGarageAsync(garageId, cancellationToken);
        var lots = await database.ParkingLots.AsNoTracking().Where(x => x.ParkingGarageId == garageId)
            .OrderBy(x => x.Number).Select(x => new LotDto(x.Id, x.Number, x.CurrentStatus)).ToListAsync(cancellationToken);
        var activeVisits = await database.Visits.CountAsync(x => x.ParkingGarageId == garageId && x.Status == VisitStatus.Active, cancellationToken);
        var free = lots.Count(x => x.CurrentStatus == ParkingLotStatus.Free);
        return new AvailabilityDto(lots.Count, activeVisits, free, lots.Count(x => x.CurrentStatus == ParkingLotStatus.Occupied),
            lots.Count(x => x.CurrentStatus == ParkingLotStatus.Unknown), free == 0, lots);
    }

    public async Task<List<ActiveVisitDto>> GetActiveVisitsAsync(int garageId, CancellationToken cancellationToken)
    {
        await RequireGarageAsync(garageId, cancellationToken);
        return await database.Visits.AsNoTracking().Where(x => x.ParkingGarageId == garageId && x.Status == VisitStatus.Active)
            .OrderBy(x => x.Id).Select(x => new ActiveVisitDto(x.Id, x.LicenseNumber, x.StartAt,
                x.Parkings.Where(p => p.Status == ParkingStatus.Active).Select(p => (int?)p.ParkingLotId).FirstOrDefault(),
                x.Parkings.Where(p => p.Status == ParkingStatus.Active).Select(p => (int?)p.ParkingLot.Number).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ActiveParkingDto>> GetActiveParkingsAsync(int garageId, CancellationToken cancellationToken)
    {
        await RequireGarageAsync(garageId, cancellationToken);
        return await database.Parkings.AsNoTracking().Where(x => x.ParkingLot.ParkingGarageId == garageId && x.Status == ParkingStatus.Active)
            .OrderBy(x => x.ParkingLot.Number).Select(x => new ActiveParkingDto(x.Id, x.ParkingLotId, x.ParkingLot.Number,
                x.VisitId, x.Visit == null ? null : x.Visit.LicenseNumber, x.StartAt)).ToListAsync(cancellationToken);
    }

    public Task<EntryDto> EnterGarageAsync(int garageId, string? licenseNumber, CancellationToken cancellationToken)
    {
        var normalized = LicenseNumberNormalizer.Normalize(licenseNumber);
        return WriteAsync(garageId, async (_, _, token) =>
        {
            var active = await database.Visits.SingleOrDefaultAsync(x => x.ParkingGarageId == garageId
                && x.LicenseNumber == normalized && x.Status == VisitStatus.Active, token);
            var now = clock.GetUtcNow();
            if (active != null && now - active.StartAt <= options.Value.DuplicateEntryWindow)
            {
                return new EntryDto(active.Id, active.LicenseNumber, active.StartAt, EntryOutcome.Duplicate);
            }

            if (active != null)
            {
                active.Status = VisitStatus.Interrupted;
                active.ClosureReason = "MissingExitBeforeNewEntry";
                active.ClosedAt = now;
                active.EndAt = null;
                active.Cost = null;
                // Release the partial Active index before inserting its replacement, in the same transaction.
                await database.SaveChangesAsync(token);
            }

            var visit = new Visit { ParkingGarageId = garageId, LicenseNumber = normalized, StartAt = now };
            database.Visits.Add(visit);
            await database.SaveChangesAsync(token);
            return new EntryDto(visit.Id, visit.LicenseNumber, visit.StartAt, active == null ? EntryOutcome.Entry : EntryOutcome.RecoveredMissingExit);
        }, cancellationToken);
    }

    public Task<LotEntryDto> EnterLotAsync(int garageId, int lotId, int? visitId, CancellationToken cancellationToken)
    {
        return WriteAsync(garageId, async (_, transaction, token) =>
        {
            var lot = await transaction.LockLotAsync(garageId, lotId, token) ?? throw ParkingError.NotFound("LOT_NOT_FOUND");
            if (lot.CurrentStatus != ParkingLotStatus.Free)
            {
                throw ParkingError.Conflict("LOT_UNAVAILABLE");
            }

            if (visitId is { } id)
            {
                var visit = await database.Visits.SingleOrDefaultAsync(x => x.Id == id && x.ParkingGarageId == garageId, token)
                    ?? throw ParkingError.NotFound("VISIT_NOT_FOUND");
                if (visit.Status != VisitStatus.Active)
                {
                    throw ParkingError.Conflict("VISIT_NOT_ACTIVE");
                }

                if (await database.Parkings.AnyAsync(x => x.VisitId == id && x.Status == ParkingStatus.Active, token))
                {
                    throw ParkingError.Conflict("VISIT_ALREADY_PARKED");
                }
            }

            var parking = new Parking { ParkingLotId = lotId, VisitId = visitId, StartAt = clock.GetUtcNow() };
            database.Parkings.Add(parking);
            lot.CurrentStatus = ParkingLotStatus.Occupied;
            await database.SaveChangesAsync(token);
            return new LotEntryDto(parking.Id, lotId, visitId, parking.StartAt);
        }, cancellationToken);
    }

    public Task<LotExitDto> LeaveLotAsync(int garageId, int lotId, CancellationToken cancellationToken)
    {
        return WriteAsync(garageId, async (_, transaction, token) =>
        {
            var lot = await transaction.LockLotAsync(garageId, lotId, token) ?? throw ParkingError.NotFound("LOT_NOT_FOUND");
            var parking = await database.Parkings.SingleOrDefaultAsync(x => x.ParkingLotId == lotId && x.Status == ParkingStatus.Active, token);
            var missingObservation = parking == null && lot.CurrentStatus != ParkingLotStatus.Free;
            if (parking != null)
            {
                parking.EndAt = clock.GetUtcNow();
                parking.ClosedAt = parking.EndAt;
                parking.Status = ParkingStatus.Completed;
            }

            lot.CurrentStatus = ParkingLotStatus.Free;
            await database.SaveChangesAsync(token);
            return new LotExitDto(lotId, parking?.Id, missingObservation);
        }, cancellationToken);
    }

    public Task<ReceiptDto> LeaveGarageAsync(int garageId, string? licenseNumber, CancellationToken cancellationToken)
    {
        var normalized = LicenseNumberNormalizer.Normalize(licenseNumber);
        return WriteAsync(garageId, async (garage, _, token) =>
        {
            var visit = await database.Visits.SingleOrDefaultAsync(x => x.ParkingGarageId == garageId
                && x.LicenseNumber == normalized && x.Status == VisitStatus.Active, token);
            if (visit == null)
            {
                throw new ParkingError("ACTIVE_VISIT_NOT_FOUND", 409, "Aktiivista käyntiä ei löytynyt. Ota yhteyttä asiakaspalveluun.", true);
            }

            var now = clock.GetUtcNow();
            var cost = fees.Calculate(now - visit.StartAt, garage.ParkingFeeConfiguration);
            visit.EndAt = now;
            visit.ClosedAt = now;
            visit.Cost = cost;
            visit.Status = VisitStatus.Completed;
            await database.SaveChangesAsync(token);
            return new ReceiptDto(visit.Id, visit.LicenseNumber, visit.StartAt, visit.EndAt.Value, visit.ClosedAt.Value,
                visit.EndAt.Value - visit.StartAt, visit.Cost.Value);
        }, cancellationToken);
    }

    private async Task RequireGarageAsync(int garageId, CancellationToken cancellationToken)
    {
        if (!await database.ParkingGarages.AnyAsync(x => x.Id == garageId, cancellationToken))
        {
            throw ParkingError.NotFound("GARAGE_NOT_FOUND");
        }
    }

    private async Task<T> WriteAsync<T>(int garageId, Func<ParkingGarage, IParkingWriteTransaction, CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await transactions.BeginAsync(cancellationToken);
            var garage = await transaction.LockGarageAsync(garageId, cancellationToken) ?? throw ParkingError.NotFound("GARAGE_NOT_FOUND");
            var result = await action(garage, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
            && postgres.ConstraintName is "IX_visits_parking_garage_id_license_number" or "IX_parkings_parking_lot_id" or "IX_parkings_visit_id")
        {
            database.ChangeTracker.Clear();
            var constraint = ((PostgresException)exception.InnerException!).ConstraintName;
            var code = constraint switch
            {
                "IX_parkings_parking_lot_id" => "LOT_UNAVAILABLE",
                "IX_parkings_visit_id" => "VISIT_ALREADY_PARKED",
                _ => "ACTIVE_VISIT_CONFLICT"
            };
            throw ParkingError.Conflict(code);
        }
    }
}
