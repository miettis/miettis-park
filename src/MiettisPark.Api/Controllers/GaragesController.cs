using Microsoft.AspNetCore.Mvc;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Services;

namespace MiettisPark.Api.Controllers;

[ApiController]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
[Route("api/garages")]
public sealed class GaragesController(ParkingService service) : ControllerBase
{
    [HttpGet]
    public Task<List<GarageDto>> GetGarages(CancellationToken cancellationToken) => service.GetGaragesAsync(cancellationToken);

    [HttpGet("{garageId:int}/availability")]
    public Task<AvailabilityDto> GetAvailability(int garageId, CancellationToken cancellationToken) => service.GetAvailabilityAsync(garageId, cancellationToken);

    [HttpGet("{garageId:int}/visits/active")]
    public Task<List<ActiveVisitDto>> GetVisits(int garageId, CancellationToken cancellationToken) => service.GetActiveVisitsAsync(garageId, cancellationToken);

    [HttpGet("{garageId:int}/parkings/active")]
    public Task<List<ActiveParkingDto>> GetParkings(int garageId, CancellationToken cancellationToken) => service.GetActiveParkingsAsync(garageId, cancellationToken);

    [HttpPost("{garageId:int}/entries")]
    public Task<EntryDto> EnterGarage(int garageId, LicenseRequest request, CancellationToken cancellationToken) => service.EnterGarageAsync(garageId, request.LicenseNumber, cancellationToken);

    [HttpPost("{garageId:int}/lots/{lotId:int}/entries")]
    public Task<LotEntryDto> EnterLot(int garageId, int lotId, LotEntryRequest request, CancellationToken cancellationToken) => service.EnterLotAsync(garageId, lotId, request.VisitId, cancellationToken);

    [HttpPost("{garageId:int}/lots/{lotId:int}/exit")]
    public Task<LotExitDto> LeaveLot(int garageId, int lotId, CancellationToken cancellationToken) => service.LeaveLotAsync(garageId, lotId, cancellationToken);

    [HttpPost("{garageId:int}/exits")]
    public Task<ReceiptDto> LeaveGarage(int garageId, LicenseRequest request, CancellationToken cancellationToken) => service.LeaveGarageAsync(garageId, request.LicenseNumber, cancellationToken);
}
