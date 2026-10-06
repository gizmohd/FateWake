var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var fatewake = postgres.AddDatabase("fatewake");

builder.AddProject<Projects.Fatewake_Api>("api")
    .WithReference(fatewake)
    .WaitFor(fatewake);

builder.Build().Run();
