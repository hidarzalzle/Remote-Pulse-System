var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var database = postgres.AddDatabase("remotepulse");

var backend = builder.AddProject<Projects.Backend_Api>(
        "backend-api",
        launchProfileName: null)
    .WithHttpEndpoint(
        port: 5001,
        targetPort: 5001,
        name: "http",
        isProxied: false)
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health", endpointName: "http");

builder.AddProject<Projects.Worker_Ingestion>("worker-ingestion")
    .WithReference(backend)
    .WaitFor(backend);

builder.AddProject<Projects.Frontend_Host>(
        "frontend",
        launchProfileName: null)
    .WithHttpEndpoint(
        port: 5003,
        targetPort: 5003,
        name: "http",
        isProxied: false)
    .WithReference(backend)
    .WaitFor(backend)
    .WithExternalHttpEndpoints();

builder.Build().Run();