using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// The built-in build-time generator invokes the entry point to discover endpoint metadata.
// It must never run migrations, seed a database, or start external integrations.
var generatingOpenApi = Assembly.GetEntryAssembly()?.GetName().Name?
    .StartsWith("GetDocument", StringComparison.OrdinalIgnoreCase) == true;

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(
        ParkingExceptionHandler.CreateProblem("INVALID_REQUEST", 400, "Virheellinen pyyntö. Tarkista kentät ja JSON-muoto."));
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ParkingExceptionHandler>();
builder.Services.AddSingleton<ParkingFeeCalculator>();
builder.Services.AddScoped<IParkingWriteTransactionFactory, ParkingWriteTransactionFactory>();
builder.Services.AddScoped<ParkingService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<ParkingDbContext>(options =>
{
    var connectionString = generatingOpenApi
        ? ParkingDbContextFactory.DesignTimeConnectionString
        : builder.Configuration.GetConnectionString("Parking");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Set ConnectionStrings__Parking before using the database.");
    }

    options.UseNpgsql(connectionString);
});
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Miettis Park API";
        document.Info.Version = "v1";
        document.Info.Description = "Parking garage simulation: independent gate and parking lot observations.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapOpenApi();
app.MapControllers();
app.MapGet("/api/health", () => TypedResults.Ok(new HealthResponse("ok")))
    .WithName("GetHealth")
    .WithSummary("Process liveness; does not query PostgreSQL.");

app.UseDefaultFiles();
app.UseStaticFiles();

// SPA fallback must not turn a missing API route or missing asset into a successful HTML response.
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api")
        || context.Request.Path.StartsWithSegments("/openapi")
        || Path.HasExtension(context.Request.Path.Value)
        || !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var index = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
    if (!index.Exists)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    if (!HttpMethods.IsHead(context.Request.Method))
    {
        await context.Response.SendFileAsync(index, context.RequestAborted);
    }
});

if (!generatingOpenApi && builder.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
    await database.Database.MigrateAsync();
}

app.Run();

public sealed record HealthResponse(string Status);

public partial class Program
{
}
