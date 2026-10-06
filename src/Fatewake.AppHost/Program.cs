var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var fatewake = postgres.AddDatabase("fatewake");

var rabbit = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithDataVolume();

var artStorageRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "data", "art"));
var emailsRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "emails"));

var api = builder.AddProject<Projects.Fatewake_Api>("api")
    .WithReference(fatewake)
    .WithReference(rabbit)
    .WithEnvironment("RabbitMq__ConnectionString", rabbit.Resource.ConnectionStringExpression)
    .WithEnvironment("ArtStorage__RootPath", artStorageRoot)
    .WithEnvironment("Email__Directory", emailsRoot)
    .WithHttpHealthCheck("/health")
    .WaitFor(fatewake)
    .WaitFor(rabbit);

builder.AddProject<Projects.Fatewake_Worker>("worker")
    .WithReference(fatewake)
    .WithReference(rabbit)
    .WithEnvironment("RabbitMq__ConnectionString", rabbit.Resource.ConnectionStringExpression)
    .WithEnvironment("ArtStorage__RootPath", artStorageRoot)
    .WaitFor(fatewake)
    .WaitFor(rabbit)
    .WaitFor(api);

var web = builder.AddProject<Projects.Fatewake_Web>("web")
    .WithReference(api)
    .WaitFor(api);

api.WithEnvironment("Email__PublicWebUrl", web.GetEndpoint("https"));
foreach (var provider in new[] { "Google", "Microsoft" })
{
    foreach (var setting in new[] { "Enabled", "ClientId", "ClientSecret", "TenantId" })
    {
        var key = $"Authentication:{provider}:{setting}";
        if (builder.Configuration[key] is not {} value) continue;
        api.WithEnvironment(key.Replace(":", "__"), value);
        web.WithEnvironment(key.Replace(":", "__"), value);
    }
}
foreach (var setting in new[] { "SmtpEnabled", "Host", "Port", "EnableSsl", "Username", "Password", "FromAddress", "PublicWebUrl", "Directory" })
{
    if (builder.Configuration[$"Email:{setting}"] is {} value)
        api.WithEnvironment($"Email__{setting}", value);
}
builder.Build().Run();
