using Microsoft.AspNetCore.Mvc;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Services;

namespace MiettisPark.Api.Controllers;

[ApiController]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
[Route("api/garages")]
public sealed class GaragesController(ParkingService service, GarageManagementService management) : ControllerBase
{
    [HttpGet]
    public Task<List<GarageDto>> GetGarages(CancellationToken cancellationToken) => service.GetGaragesAsync(cancellationToken);

    [HttpPost]
    [ProducesResponseType(typeof(GarageDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<GarageDto>> Create(GarageRequest request, CancellationToken cancellationToken)
    {
        var garage = await management.CreateAsync(request, cancellationToken);
        return Created($"/api/garages/{garage.Id}/layout", garage);
    }

    [HttpPut("{garageId:int}")]
    public Task<GarageDto> Update(int garageId, GarageRequest request, CancellationToken cancellationToken) => management.UpdateAsync(garageId, request, cancellationToken);

    [HttpGet("{garageId:int}/layout")]
    public Task<LayoutDto> GetLayout(int garageId, CancellationToken cancellationToken) => management.GetLayoutAsync(garageId, cancellationToken);

    [HttpPut("{garageId:int}/layout")]
    public Task<LayoutDto> SaveLayout(int garageId, LayoutRequest request, CancellationToken cancellationToken) => management.SaveLayoutAsync(garageId, request, cancellationToken);

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
