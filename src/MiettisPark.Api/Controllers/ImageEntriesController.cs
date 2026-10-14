using Microsoft.AspNetCore.Mvc;
using MiettisPark.Api.Contracts;
using MiettisPark.Api.Services;
using MiettisPark.Api.Services.PlateRecognition;

namespace MiettisPark.Api.Controllers;

[ApiController]
[Route("api/garages/{garageId:int}/entries/from-image")]
public sealed class ImageEntriesController(ImageEntryService service, ImageUploadReader uploads, ImageValidator images, ImageOperationLimiter limiter) : ControllerBase
{
    [DisableImageFormBinding]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageUploadReader.MaximumRequestBytes)]
    [ProducesResponseType(typeof(ImageEntryDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    [ProducesResponseType(typeof(ProblemDetails), 413)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    [ProducesResponseType(typeof(ProblemDetails), 502)]
    [ProducesResponseType(typeof(ProblemDetails), 503)]
    [ProducesResponseType(typeof(ProblemDetails), 504)]
    public async Task<ImageEntryDto> Enter(int garageId, CancellationToken token)
    {
        using var slot = limiter.Acquire();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            var upload = await uploads.ReadAsync(Request, budget.Token);
            var effective = await service.PreflightAsync(garageId, upload.OccurredAt, budget.Token);
            var image = await images.ValidateAsync(upload.Bytes, budget.Token);
            return await service.EnterAsync(garageId, image, effective, budget.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new ParkingError("PLATE_PROVIDER_TIMEOUT", 504, "Kuvan käsittelyn aikaraja ylittyi.");
        }
    }
}
[AttributeUsage(AttributeTargets.Method)]
public sealed class DisableImageFormBindingAttribute : Attribute, Microsoft.AspNetCore.Mvc.Filters.IResourceFilter
{
    public void OnResourceExecuting(Microsoft.AspNetCore.Mvc.Filters.ResourceExecutingContext context)
    {
        for (var i = context.ValueProviderFactories.Count - 1; i >= 0; i--)
        {
            if (context.ValueProviderFactories[i] is Microsoft.AspNetCore.Mvc.ModelBinding.FormValueProviderFactory
                or Microsoft.AspNetCore.Mvc.ModelBinding.FormFileValueProviderFactory
                or Microsoft.AspNetCore.Mvc.ModelBinding.JQueryFormValueProviderFactory)
            {
                context.ValueProviderFactories.RemoveAt(i);
            }
        }
    }

    public void OnResourceExecuted(Microsoft.AspNetCore.Mvc.Filters.ResourceExecutedContext context)
    {
    }
}