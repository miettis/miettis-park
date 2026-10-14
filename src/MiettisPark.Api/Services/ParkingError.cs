namespace MiettisPark.Api.Services;

public sealed class ParkingError(string code, int statusCode, string message, bool requiresCustomerService = false) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public bool RequiresCustomerService { get; } = requiresCustomerService;

    public static ParkingError NotFound(string code) => new(code, 404, "Pyydettyä hallia, paikkaa tai käyntiä ei löytynyt.");
    public static ParkingError Conflict(string code) => new(code, 409, "Toiminto ei onnistu nykyisessä tilanteessa. Päivitä tiedot.");
}

public static class LicenseNumberNormalizer
{
    public static string Normalize(string? licenseNumber)
    {
        var normalized = licenseNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 20)
        {
            throw new ParkingError("INVALID_LICENSE_NUMBER", 400, "Anna rekisteritunnus, jossa on 1–20 merkkiä.");
        }

        return normalized;
    }
}
