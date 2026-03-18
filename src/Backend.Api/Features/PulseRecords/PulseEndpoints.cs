using System.Diagnostics;
using Backend.Api.Infrastructure.Persistence;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api.Features.PulseRecords;

public static class PulseEndpoints
{
    public static RouteGroupBuilder MapPulseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pulses").WithTags("PulseRecords");

        group.MapGet("/latest", GetLatestAsync);
        group.MapPost("/ingest", IngestAsync);

        return group;
    }

    private static async Task<IResult> GetLatestAsync(
        PulseDbContext db,
        CancellationToken cancellationToken)
    {
        // SQLite provider can't translate DateTimeOffset ordering. We store/retrieve
        // in Postgres for production, but keep a dev-friendly fallback here.
        var provider = db.Database.ProviderName ?? string.Empty;

        var query = db.Pulses.AsNoTracking();
        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var rows = await query
                .ToListAsync(cancellationToken);

            var resultSqlite = rows
                .OrderByDescending(x => x.ObservedAtUtc)
                .Take(50)
                .Select(x => new PulseRecordDto(x.Id.Value, x.ObservedAtUtc, x.Bpm, x.Source))
                .ToList();

            return Results.Ok(resultSqlite);
        }

        var result = await query
            .OrderByDescending(x => x.ObservedAtUtc)
            .Take(50)
            .Select(x => new PulseRecordDto(x.Id.Value, x.ObservedAtUtc, x.Bpm, x.Source))
            .ToListAsync(cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> IngestAsync(
        [FromBody] PulseRecordDto request,
        PulseDbContext db,
        IHubContext<Hubs.PulseHub> hub,
        ActivitySource activitySource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("PulseIngest");
        using var activity = activitySource.StartActivity("pulse.ingest.http");

        var validation = Validate(request);
        if (!validation.IsSuccess)
        {
            logger.LogWarning("Pulse ingestion validation failed: {Error}", validation.Error);
            return Results.BadRequest(validation.Error);
        }

        var entity = new PulseEntity(new PulseId(request.Id), request.ObservedAtUtc, request.Bpm, request.Source);
        await db.Pulses.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await hub.Clients.All.SendAsync("pulse.received", request, cancellationToken);
        logger.LogInformation("Pulse ingested: {PulseId} {Bpm}", request.Id, request.Bpm);

        return Results.Accepted(value: request);
    }

    private static Result<PulseRecordDto> Validate(PulseRecordDto request)
    {
        if (request.Bpm is < 20 or > 260)
        {
            return Result<PulseRecordDto>.Failure("BPM out of supported range.");
        }

        return Result<PulseRecordDto>.Success(request);
    }
}

public sealed class PulseIngestionGrpcService(
    PulseDbContext db,
    IHubContext<Hubs.PulseHub> hub,
    ActivitySource activitySource,
    ILogger<PulseIngestionGrpcService> logger) : PulseIngestion.PulseIngestionBase
{
    public override async Task<IngestPulseReply> IngestPulse(IngestPulseRequest request, ServerCallContext context)
    {
        using var activity = activitySource.StartActivity("pulse.ingest.grpc");

        var dto = new PulseRecordDto(
            Guid.Parse(request.Id),
            request.ObservedAt.ToDateTimeOffset(),
            request.Bpm,
            request.Source);

        var validation = Validate(dto);
        if (!validation.IsSuccess)
        {
            logger.LogWarning("gRPC pulse rejected: {Error}", validation.Error);
            return new IngestPulseReply { Accepted = false, Error = validation.Error ?? "Validation failed." };
        }

        var entity = new PulseEntity(new PulseId(dto.Id), dto.ObservedAtUtc, dto.Bpm, dto.Source);
        await db.Pulses.AddAsync(entity, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);

        await hub.Clients.All.SendAsync("pulse.received", dto, context.CancellationToken);
        logger.LogInformation("gRPC pulse accepted {Id}", dto.Id);

        return new IngestPulseReply { Accepted = true };
    }

    private static Result<PulseRecordDto> Validate(PulseRecordDto request)
    {
        if (request.Bpm is < 20 or > 260)
        {
            return Result<PulseRecordDto>.Failure("BPM out of supported range.");
        }

        return Result<PulseRecordDto>.Success(request);
    }
}
