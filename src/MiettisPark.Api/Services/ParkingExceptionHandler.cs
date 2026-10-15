using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MiettisPark.Api.Services;

public sealed class ParkingExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var error = exception as ParkingError;
        context.Response.StatusCode = error?.StatusCode ?? StatusCodes.Status500InternalServerError;
        var details = CreateProblem(error?.Code ?? "INTERNAL_ERROR", context.Response.StatusCode,
            error?.Message ?? "Toiminto epäonnistui. Yritä myöhemmin uudelleen.", error?.RequiresCustomerService == true);
        if (error?.MinimumOccurredAt is { } minimum)
        {
            details.Extensions["minimumOccurredAt"] = minimum.ToUniversalTime();
        }

        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = details });
    }

    public static ProblemDetails CreateProblem(string code, int status, string detail, bool requiresCustomerService = false)
    {
        var problem = new ProblemDetails { Status = status, Title = "Pysäköintitoiminto epäonnistui", Detail = detail };
        problem.Extensions["code"] = code;
        problem.Extensions["gateOpened"] = false;
        if (requiresCustomerService)
        {
            problem.Extensions["requiresCustomerService"] = true;
        }

        return problem;
    }
}
