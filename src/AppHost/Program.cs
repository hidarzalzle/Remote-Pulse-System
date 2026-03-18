var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var database = postgres.AddDatabase("remotepulse");

var backend = builder.AddProject<Projects.Backend_Api>("backend-api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Worker_Ingestion>("worker-ingestion")
    .WithReference(backend)
    .WaitFor(backend);

builder.AddProject<Projects.Frontend_Host>("frontend")
    .WithReference(backend)
    .WaitFor(backend)
    .WithExternalHttpEndpoints();

builder.Build().Run();
