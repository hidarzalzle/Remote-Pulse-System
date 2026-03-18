using System.Diagnostics;
using Backend.Api.Features.Health;
using Backend.Api.Features.PulseRecords;
using Backend.Api.Hubs;
using Backend.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1;
});

var pgConn = builder.Configuration.GetConnectionString("remotepulse");
if (!string.IsNullOrEmpty(pgConn))
{
    builder.Services.AddDbContext<PulseDbContext>(options =>
        options.UseNpgsql(pgConn, npgsql =>
            npgsql.MigrationsAssembly(typeof(PulseDbContext).Assembly.FullName)));
}
else
{
    // Fall back to a local Sqlite database for development if Postgres is not available.
    builder.Services.AddDbContext<PulseDbContext>(options =>
        options.UseSqlite("Data Source=remotepulse.db"));
}

var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? [
        "http://localhost:5003",
        "https://localhost:5003",
        "http://localhost:62304",
        "https://localhost:62303"
    ];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHybridCache();
builder.Services.AddSignalR();
builder.Services.AddGrpc();
builder.Services.AddSingleton<ActivitySource>(_ => PulseTelemetry.Activity);

var app = builder.Build();

app.MapOpenApi();
app.UseCors("Frontend");
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Backend API v1");
});
app.MapDefaultEndpoints();
app.MapPulseEndpoints();
app.MapHealthSummaryEndpoints();
app.MapHub<PulseHub>("/hubs/pulse");
app.MapGrpcService<PulseIngestionGrpcService>();

await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<PulseDbContext>();
await db.Database.MigrateAsync(CancellationToken.None);

await app.RunAsync();

public static class PulseTelemetry
{
    public static readonly ActivitySource Activity = new("RemotePulse.Backend.Api");
}
