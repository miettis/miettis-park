using Microsoft.EntityFrameworkCore;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services.PlateRecognition;

namespace MiettisPark.Api.Services;

public sealed class ImageEntryService(ParkingDbContext database, ParkingService parking, ILicensePlateReader reader)
{
    public async Task<DateTimeOffset> PreflightAsync(int garageId, DateTimeOffset? occurredAt, CancellationToken token)
    {
        var effective = parking.ResolveOccurredAt(occurredAt);
        if (!await database.ParkingGarages.AsNoTracking().AnyAsync(x => x.Id == garageId, token))
        {
            throw ParkingError.NotFound("GARAGE_NOT_FOUND");
        }

        if (!reader.IsAvailable)
        {
            throw new ParkingError("PLATE_RECOGNITION_UNAVAILABLE", 503, "Kuvatunnistus ei ole käytössä. Käytä käsinsyöttöä.");
        }

        return effective;
    }

    public async Task<ImageEntryDto> EnterAsync(int garageId, ValidatedPlateImage image, DateTimeOffset effective, CancellationToken token)
    {
        var result = await reader.ReadAsync(image, token);
        token.ThrowIfCancellationRequested();
        var plates = result.Plates.Select(LicenseNumberNormalizer.Normalize).Distinct(StringComparer.Ordinal).ToArray();
        if (result.Ambiguous || plates.Length != 1)
        {
            var code = result.Ambiguous ? "PLATE_AMBIGUOUS" : plates.Length == 0 ? "PLATE_NOT_FOUND" : "MULTIPLE_PLATES";
            throw new ParkingError(code, 422, "Yhtä yksiselitteistä rekisteriä ei löytynyt. Lähetä uusi kuva tai käytä käsinsyöttöä.");
        }

        var entry = await parking.EnterGarageAtAsync(garageId, plates[0], effective, token);
        return new ImageEntryDto(entry.VisitId, entry.LicenseNumber, entry.StartAt, entry.Outcome, entry.EffectiveOccurredAt, entry.GateOpened, plates[0]);
    }
}

public sealed class ImageOperationLimiter
{
    private readonly SemaphoreSlim slots = new(2, 2);

    public IDisposable Acquire()
    {
        if (!slots.Wait(0))
        {
            throw new ParkingError("PLATE_RECOGNITION_BUSY", 503, "Kuvatunnistus on varattu. Yritä myöhemmin tai käytä käsinsyöttöä.");
        }

        return new Lease(slots);
    }

    private sealed class Lease(SemaphoreSlim slots) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                slots.Release();
            }
        }
    }
}