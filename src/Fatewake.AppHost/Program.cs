using Aspire.Hosting.Dotnet;
using Microsoft.Extensions.Hosting;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
builder.Services.AddFatewakeObservability(builder.Configuration, "Fatewake.AppHost");

IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
IResourceBuilder<PostgresDatabaseResource> fatewake = postgres.AddDatabase("fatewake");

IResourceBuilder<ParameterResource> rabbitUserName = builder.AddParameter("rabbitmq-username", "admin");
IResourceBuilder<ParameterResource> rabbitPassword = builder.AddParameter("rabbitmq-password", "admin", secret: true);

IResourceBuilder<RabbitMQServerResource> rabbit = builder.AddRabbitMQ("rabbitmq", userName: rabbitUserName, password: rabbitPassword)
    .WithManagementPlugin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

string artStorageRoot = Path.GetFullPath(builder.Configuration["ArtStorage:RootPath"] ??
    Path.Combine("data", "art"), Path.Combine(builder.AppHostDirectory, "..", ".."));
string repoRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", ".."));
string emailsRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "emails"));

IResourceBuilder<DotnetProjectResource> api = builder.AddDotnetProject("api", Path.Combine(repoRoot, "src", "Fatewake.Api", "Fatewake.Api.csproj"))
    .WithReference(fatewake)
    .WithReference(rabbit)
    .WithEnvironment("RabbitMq__ConnectionString", rabbit.Resource.ConnectionStringExpression)
    .WithEnvironment("ArtStorage__RootPath", artStorageRoot)
    .WithEnvironment("Email__Directory", emailsRoot)
    .WithHttpHealthCheck("/health")
    .WaitFor(fatewake)
    .WaitFor(rabbit);

IResourceBuilder<DotnetProjectResource> worker = builder.AddDotnetProject("worker", Path.Combine(repoRoot, "src", "Fatewake.Worker", "Fatewake.Worker.csproj"))
    .WithReference(fatewake)
    .WithReference(rabbit)
    .WithEnvironment("RabbitMq__ConnectionString", rabbit.Resource.ConnectionStringExpression)
    .WithEnvironment("ArtStorage__RootPath", artStorageRoot)
    .WaitFor(fatewake)
    .WaitFor(rabbit)
    .WaitFor(api);

api.WithForwardedSection(builder, "ArtGeneration");
api.WithForwardedSection(builder, "ArtStorage");

worker.WithForwardedSection(builder, "ArtGeneration");
worker.WithForwardedSection(builder, "ArtStorage");

IResourceBuilder<DotnetProjectResource> web = builder.AddDotnetProject("web", Path.Combine(repoRoot, "src", "Fatewake.Web", "Fatewake.Web.csproj"))
    .WithReference(api)
    .WaitFor(api);

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

