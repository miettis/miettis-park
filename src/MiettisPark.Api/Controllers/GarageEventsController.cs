using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MiettisPark.Api.Services;

namespace MiettisPark.Api.Controllers;

[ApiController]
[Route("api/garages/events")]
public sealed class GarageEventsController(GarageEventPublisher publisher, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    [Produces("text/event-stream")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task Get(CancellationToken cancellationToken)
    {
        using var subscription = publisher.Subscribe();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, subscription.Stopped);
        var token = lifetime.Token;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";
        try
        {
            await WriteAsync(": connected\n\n", token);
            while (!token.IsCancellationRequested)
            {
                using var iteration = CancellationTokenSource.CreateLinkedTokenSource(token);
                var ready = subscription.Reader.WaitToReadAsync(iteration.Token).AsTask();
                var heartbeat = Task.Delay(TimeSpan.FromSeconds(15), clock, iteration.Token);
                try
                {
                    var winner = await Task.WhenAny(ready, heartbeat);
                    if (winner == ready && await ready)
                    {
                        while (subscription.Reader.TryRead(out var change))
                        {
                            var json = JsonSerializer.Serialize(change, JsonSerializerOptions.Web);
                            await WriteAsync($"event: garage-changed\ndata: {json}\n\n", token);
                        }
                    }
                    else
                    {
                        await WriteAsync(": heartbeat\n\n", token);
                    }

                }
                finally
                {
                    iteration.Cancel();
                    try
                    {
                        await Task.WhenAll(ready, heartbeat);
                    }
                    catch (OperationCanceledException) when (iteration.IsCancellationRequested)
                    {
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            HttpContext.Abort();
        }
        catch (IOException)
        {
            HttpContext.Abort();
        }
    }

    private async Task WriteAsync(string frame, CancellationToken token)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
        await Response.WriteAsync(frame, linked.Token);
        await Response.Body.FlushAsync(linked.Token);
    }
}
