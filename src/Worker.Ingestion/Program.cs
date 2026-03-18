using System.Diagnostics;
using System.Net.Http.Json;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Backend.Api.Features.PulseRecords;
using ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddSingleton(_ => new ActivitySource("RemotePulse.Worker.Ingestion"));
builder.Services.AddSingleton(_ =>
    builder.Configuration["services:backend-api:https:0"]
    ?? builder.Configuration["services:backend-api:http:0"]
    ?? "http://localhost:5001");
builder.Services.AddSingleton(sp =>
{
    var endpoint = sp.GetRequiredService<string>();
    return GrpcChannel.ForAddress(endpoint);
});
builder.Services.AddSingleton(sp => new PulseIngestion.PulseIngestionClient(sp.GetRequiredService<GrpcChannel>()));
builder.Services.AddHttpClient<BackendIngestionHttpClient>((sp, client) =>
{
    var endpoint = sp.GetRequiredService<string>();
    client.BaseAddress = new Uri(endpoint);
});
builder.Services.AddHostedService<IngestionWorker>();

await builder.Build().RunAsync();

public sealed class IngestionWorker(
    ILogger<IngestionWorker> logger,
    PulseIngestion.PulseIngestionClient grpcClient,
    BackendIngestionHttpClient httpClient,
    ActivitySource activitySource) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Ingestion worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = activitySource.StartActivity("ingestion.push");
            var pulse = new PulseSubmission(
                Guid.CreateVersion7(),
                DateTimeOffset.UtcNow,
                Random.Shared.Next(55, 125),
                "worker-simulator");

            try
            {
                var grpcReply = await grpcClient.IngestPulseAsync(
                    new IngestPulseRequest
                    {
                        Id = pulse.Id.ToString(),
                        ObservedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(pulse.ObservedAtUtc),
                        Bpm = pulse.Bpm,
                        Source = pulse.Source
                    },
                    cancellationToken: stoppingToken);

                logger.LogInformation("Pulse pushed via gRPC accepted={Accepted}", grpcReply.Accepted);
            }
            catch (Grpc.Core.RpcException ex) when (
                ex.StatusCode is Grpc.Core.StatusCode.Unavailable
                or Grpc.Core.StatusCode.Internal
                or Grpc.Core.StatusCode.DeadlineExceeded)
            {
                logger.LogWarning(ex, "gRPC ingestion unavailable; falling back to HTTP.");
                await httpClient.IngestAsync(pulse, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected ingestion failure; will retry.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}

public sealed class BackendIngestionHttpClient(HttpClient httpClient, ILogger<BackendIngestionHttpClient> logger)
{
    public async Task IngestAsync(PulseSubmission pulse, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/pulses/ingest", pulse, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Pulse pushed via HTTP accepted={StatusCode}", (int)response.StatusCode);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"HTTP ingestion failed with status {(int)response.StatusCode}: {body}");
    }
}

public sealed record PulseSubmission(Guid Id, DateTimeOffset ObservedAtUtc, int Bpm, string Source);
