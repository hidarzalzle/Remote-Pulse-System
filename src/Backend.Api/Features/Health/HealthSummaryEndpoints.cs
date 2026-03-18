using Backend.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Backend.Api.Features.Health;

public static class HealthSummaryEndpoints
{
    public static IEndpointRouteBuilder MapHealthSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/summary", GetSummaryAsync)
            .WithTags("Health")
            .WithName("GetHealthSummary");

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(
        PulseDbContext db,
        HybridCache cache,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("HealthSummary");

        var summary = await cache.GetOrCreateAsync(
            "health-summary",
            async token =>
            {
                logger.LogInformation("Rebuilding health summary cache entry.");

                var latest = await db.Pulses
                    .OrderByDescending(x => x.ObservedAtUtc)
                    .Take(100)
                    .ToListAsync(token);

                var avg = latest.Count == 0 ? 0 : latest.Average(x => x.Bpm);

                return new
                {
                    TotalRecords = await db.Pulses.CountAsync(token),
                    LastObservedAtUtc = latest.FirstOrDefault()?.ObservedAtUtc,
                    AverageBpm = Math.Round(avg, 2)
                };
            },
            cancellationToken: cancellationToken);

        return Results.Ok(summary);
    }
}
