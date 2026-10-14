using Microsoft.EntityFrameworkCore;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using Npgsql;

namespace MiettisPark.Api.Services;

public sealed class GarageManagementService(ParkingDbContext database, IParkingWriteTransactionFactory transactions)
{
    public Task<GarageDto> CreateAsync(GarageRequest request, CancellationToken token) => WriteAsync(async transaction =>
    {
        var garage = new ParkingGarage { ParkingFeeConfiguration = DefaultParkingFees.Create() };
        ApplyDetails(garage, request, true);
        database.ParkingGarages.Add(garage);
        await database.SaveChangesAsync(token);
        return Details(garage, []);
    }, token);

    public Task<GarageDto> UpdateAsync(int id, GarageRequest request, CancellationToken token) => WriteAsync(async transaction =>
    {
        var garage = await transaction.LockGarageAsync(id, token) ?? throw ParkingError.NotFound("GARAGE_NOT_FOUND");
        ApplyDetails(garage, request, false);
        await database.SaveChangesAsync(token);
        var lots = await database.ParkingLots.AsNoTracking().Where(lot => lot.ParkingGarageId == id && lot.IsActive).ToListAsync(token);
        return Details(garage, lots);
    }, token);

    public async Task<LayoutDto> GetLayoutAsync(int id, CancellationToken token)
    {
        // One SQL statement keeps the version, JSON and live lot data in the same snapshot.
        return await database.ParkingGarages.AsNoTracking().AsSingleQuery().Where(garage => garage.Id == id)
            .Select(garage => new LayoutDto(garage.Id, garage.LayoutWidth, garage.LayoutHeight, garage.LayoutVersion,
                garage.LayoutCells, garage.Lots.Where(lot => lot.IsActive).OrderBy(lot => lot.Number)
                    .Select(lot => new LayoutLotDto(lot.Id, lot.Number, lot.CurrentStatus,
                        lot.CurrentStatus == ParkingLotStatus.Free && !lot.Parkings.Any(parking => parking.Status == ParkingStatus.Active))).ToList()))
            .SingleOrDefaultAsync(token) ?? throw ParkingError.NotFound("GARAGE_NOT_FOUND");
    }

    public Task<LayoutDto> SaveLayoutAsync(int id, LayoutRequest request, CancellationToken token) => WriteAsync(async transaction =>
    {
        var garage = await transaction.LockGarageAsync(id, token) ?? throw ParkingError.NotFound("GARAGE_NOT_FOUND");
        if (request.ExpectedVersion != garage.LayoutVersion)
        {
            throw ParkingError.Conflict("LAYOUT_VERSION_CONFLICT");
        }

        ValidateShape(request);
        var ids = await database.ParkingLots.AsNoTracking().Where(lot => lot.ParkingGarageId == id && lot.IsActive)
            .OrderBy(lot => lot.Id).Select(lot => lot.Id).ToListAsync(token);
        var lots = new Dictionary<int, ParkingLot>();
        foreach (var lotId in ids)
        {
            var lot = await transaction.LockLotAsync(id, lotId, token) ?? throw ParkingError.NotFound("LOT_NOT_FOUND");
            lots.Add(lotId, lot);
        }

        var cells = request.Cells!.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToList();
        var parkingCells = cells.Where(cell => cell.Type == LayoutCellType.ParkingLot).ToList();
        var retained = new HashSet<int>();
        foreach (var cell in parkingCells.Where(cell => cell.LotId.HasValue))
        {
            if (!lots.ContainsKey(cell.LotId!.Value))
            {
                throw ParkingError.NotFound("LOT_NOT_FOUND");
            }

            if (!retained.Add(cell.LotId.Value))
            {
                throw InvalidLayout();
            }
        }

        if (request.RemovedLotIds!.Any(lotId => !lots.ContainsKey(lotId)))
        {
            throw ParkingError.NotFound("LOT_NOT_FOUND");
        }

        var removed = lots.Keys.Except(retained).ToHashSet();
        if (request.RemovedLotIds!.Distinct().Count() != request.RemovedLotIds!.Count
            || !removed.SetEquals(request.RemovedLotIds))
        {
            throw InvalidLayout();
        }

        var activeParkingIds = await database.Parkings.Where(parking => ids.Contains(parking.ParkingLotId) && parking.Status == ParkingStatus.Active)
            .Select(parking => parking.ParkingLotId).ToListAsync(token);
        foreach (var lotId in removed)
        {
            if (lots[lotId].CurrentStatus != ParkingLotStatus.Free || activeParkingIds.Contains(lotId))
            {
                throw ParkingError.Conflict("LOT_REMOVAL_CONFLICT");
            }
        }

        var numbers = new HashSet<int>();
        foreach (var cell in parkingCells.Where(cell => cell.Number.HasValue))
        {
            if (!numbers.Add(cell.Number!.Value))
            {
                throw new ParkingError("LOT_NUMBER_CONFLICT", 400, "Aktiivisten paikkojen numeroiden on oltava yksikäsitteisiä.");
            }
        }

        var numbered = new List<(LayoutCellRequest Cell, int Number)>();
        var candidate = 1;
        foreach (var cell in parkingCells)
        {
            var number = cell.Number;
            if (number == null)
            {
                while (numbers.Contains(candidate))
                {
                    candidate++;
                }

                number = candidate;
                numbers.Add(candidate);
            }

            numbered.Add((cell, number.Value));
        }

        // Immediate PostgreSQL uniqueness also applies within a transaction. Move changed
        // numbers to unused positive values before applying swaps and number reuse.
        var unavailable = lots.Values.Select(lot => lot.Number).Concat(numbers).ToHashSet();
        var temporary = 1;
        foreach (var pair in numbered.Where(pair => pair.Cell.LotId.HasValue))
        {
            var lot = lots[pair.Cell.LotId!.Value];
            if (lot.Number != pair.Number)
            {
                while (unavailable.Contains(temporary))
                {
                    temporary++;
                }

                lot.Number = temporary;
                unavailable.Add(temporary);
            }
        }

        foreach (var lotId in removed)
        {
            lots[lotId].IsActive = false;
        }

        await database.SaveChangesAsync(token);
        var assigned = new Dictionary<(int X, int Y), ParkingLot>();
        foreach (var pair in numbered)
        {
            ParkingLot lot;
            if (pair.Cell.LotId is { } lotId)
            {
                lot = lots[lotId];
                lot.Number = pair.Number;
            }
            else
            {
                lot = new ParkingLot { ParkingGarageId = id, Number = pair.Number };
                database.ParkingLots.Add(lot);
            }

            assigned.Add((pair.Cell.X, pair.Cell.Y), lot);
        }

        await database.SaveChangesAsync(token);
        garage.LayoutCells = cells.Select(cell => new LayoutCell(cell.X, cell.Y, cell.Type,
            assigned.TryGetValue((cell.X, cell.Y), out var lot) ? lot.Id : null)).ToList();
        garage.LayoutWidth = request.Width;
        garage.LayoutHeight = request.Height;
        garage.LayoutVersion = checked(garage.LayoutVersion + 1);
        await database.SaveChangesAsync(token);
        return await GetLayoutAsync(id, token);
    }, token);

    private static void ValidateShape(LayoutRequest request)
    {
        if (request.Width is < 1 or > 50 || request.Height is < 1 or > 50 || request.Cells == null
            || request.Cells.Count != request.Width * request.Height || request.RemovedLotIds == null
            || request.RemovedLotIds.Any(id => id <= 0))
        {
            throw InvalidLayout();
        }

        var coordinates = new HashSet<(int, int)>();
        foreach (var cell in request.Cells)
        {
            if (cell == null || cell.X < 0 || cell.X >= request.Width || cell.Y < 0 || cell.Y >= request.Height
                || !coordinates.Add((cell.X, cell.Y)) || !Enum.IsDefined(cell.Type))
            {
                throw InvalidLayout();
            }

            if (cell.Type != LayoutCellType.ParkingLot && (cell.LotId != null || cell.Number != null)
                || cell.Type == LayoutCellType.ParkingLot && (cell.LotId is <= 0 || cell.Number is <= 0 || cell.LotId != null && cell.Number == null))
            {
                throw InvalidLayout();
            }
        }
    }

    private static ParkingError InvalidLayout() => new("INVALID_LAYOUT", 400, "Tarkista pohjan mitat, ruudut, numerot ja nimenomaisesti poistettavat paikat.");

    private static void ApplyDetails(ParkingGarage garage, GarageRequest request, bool creating)
    {
        var name = request.Name?.Trim();
        var address = request.Address?.Trim();
        if (name == null || name.Length is < 1 or > 120 || address?.Length > 300
            || (creating || garage.Address != null) && string.IsNullOrWhiteSpace(address)
            || request.Latitude.HasValue != request.Longitude.HasValue
            || (creating || garage.Latitude.HasValue) && !request.Latitude.HasValue
            || request.Latitude is { } latitude && (!double.IsFinite(latitude) || latitude is < -90 or > 90)
            || request.Longitude is { } longitude && (!double.IsFinite(longitude) || longitude is < -180 or > 180))
        {
            throw new ParkingError("INVALID_GARAGE", 400, "Tarkista nimi, osoite ja karttapisteen koordinaatit.");
        }

        garage.Name = name;
        garage.Address = string.IsNullOrWhiteSpace(address) ? null : address;
        garage.Latitude = request.Latitude;
        garage.Longitude = request.Longitude;
    }

    private static GarageDto Details(ParkingGarage garage, IReadOnlyCollection<ParkingLot> lots) => new(garage.Id, garage.Name,
        garage.Address, garage.Latitude, garage.Longitude, lots.Count,
        lots.Count(lot => lot.CurrentStatus == ParkingLotStatus.Free), lots.Count(lot => lot.CurrentStatus == ParkingLotStatus.Occupied),
        lots.Count(lot => lot.CurrentStatus == ParkingLotStatus.Unknown), !lots.Any(lot => lot.CurrentStatus == ParkingLotStatus.Free));

    private async Task<T> WriteAsync<T>(Func<IParkingWriteTransaction, Task<T>> action, CancellationToken token)
    {
        try
        {
            await using var transaction = await transactions.BeginAsync(token);
            var result = await action(transaction);
            await transaction.CommitAsync(token);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            throw ParkingError.Conflict("LAYOUT_VERSION_CONFLICT");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_parking_lots_parking_garage_id_number" })
        {
            database.ChangeTracker.Clear();
            throw new ParkingError("LOT_NUMBER_CONFLICT", 400, "Paikkanumero on jo käytössä.");
        }
        catch
        {
            database.ChangeTracker.Clear();
            throw;
        }
    }
}
