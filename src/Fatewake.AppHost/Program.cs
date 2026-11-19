using Microsoft.Extensions.Hosting;

Environment.SetEnvironmentVariable("ASPIRE_DASHBOARD_TELEMETRY_OPTOUT", "true");

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
builder.Services.AddFatewakeObservability(builder.Configuration, "Fatewake.AppHost");

IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "0.8.7-pg18-trixie")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
IResourceBuilder<PostgresDatabaseResource> fatewake = postgres.AddDatabase("fatewake");

IResourceBuilder<RedisResource> redis = builder.AddRedis("redis")
    .WithImage("redis", "8.10.3-trixie")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

IResourceBuilder<ParameterResource> rabbitUserName = builder.AddParameter("rabbitmq-username", "admin");
IResourceBuilder<ParameterResource> rabbitPassword = builder.AddParameter("rabbitmq-password", "admin", secret: true);

IResourceBuilder<RabbitMQServerResource> rabbit = builder.AddRabbitMQ("rabbitmq", userName: rabbitUserName, password: rabbitPassword)
    .WithImage("rabbitmq", "4.4.1-management")
    .WithEndpoint(targetPort: 15672, port: 15672, scheme: "http", name: "management")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

string artStorageRoot = Path.GetFullPath(builder.Configuration["ArtStorage:RootPath"] ??
    Path.Combine("data", "art"), Path.Combine(builder.AppHostDirectory, "..", ".."));
string repoRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", ".."));
string emailsRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "emails"));

IResourceBuilder<ProjectResource> api = builder.AddProject("api", Path.Combine(repoRoot, "src", "Fatewake.Api", "Fatewake.Api.csproj"))
    .WithReference(fatewake)
    .WithReference(rabbit)
    .WithReference(redis)
    .WithEnvironment("RabbitMq__ConnectionString", rabbit.Resource.ConnectionStringExpression)
    .WithEnvironment("ArtStorage__RootPath", artStorageRoot)
    .WithEnvironment("Email__Directory", emailsRoot)
    .WithHttpHealthCheck("/health")
    .WaitFor(fatewake)
    .WaitFor(rabbit)
    .WaitFor(redis);

IResourceBuilder<ProjectResource> worker = builder.AddProject("worker", Path.Combine(repoRoot, "src", "Fatewake.Worker", "Fatewake.Worker.csproj"))
    .WithReference(fatewake)
    .WithReference(rabbit)
    .WithReference(redis)
    .WithEnvironment("RabbitMq__ConnectionString", rabbit.Resource.ConnectionStringExpression)
    .WithEnvironment("ArtStorage__RootPath", artStorageRoot)
    .WaitFor(fatewake)
    .WaitFor(rabbit)
    .WaitFor(api)
    .WaitFor(redis);

api.WithForwardedSection(builder, "ArtGeneration");
api.WithForwardedSection(builder, "ArtStorage");

worker.WithForwardedSection(builder, "ArtGeneration");
worker.WithForwardedSection(builder, "ArtStorage");

IResourceBuilder<ProjectResource> web = builder.AddProject("web", Path.Combine(repoRoot, "src", "Fatewake.Web", "Fatewake.Web.csproj"))
    .WithReference(api)
    .WithReference(redis)
    .WaitFor(api)
    .WaitFor(redis);

api.WithEnvironment("Email__PublicWebUrl", web.GetEndpoint("https"));
foreach (string? provider in new[] { "Google", "Microsoft" })
{
    foreach (string? setting in new[] { "Enabled", "ClientId", "ClientSecret", "TenantId" })
    {
        string key = $"Authentication:{provider}:{setting}";
        if (builder.Configuration[key] is not { } value) continue;
        api.WithEnvironment(key.Replace(":", "__"), value);
        web.WithEnvironment(key.Replace(":", "__"), value);
    }
}
foreach (string? setting in new[] { "SmtpEnabled", "Host", "Port", "EnableSsl", "Username", "Password", "FromAddress", "PublicWebUrl", "Directory" })
{
    if (builder.Configuration[$"Email:{setting}"] is { } value)
        api.WithEnvironment($"Email__{setting}", value);
}
builder.Build().Run();

