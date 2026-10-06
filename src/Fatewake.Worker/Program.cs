using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Microsoft.EntityFrameworkCore;

var builder=Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddDbContext<FatewakeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("fatewake")));

builder.Services.Configure<WorkExecutionOptions>(builder.Configuration.GetSection("Work"));
builder.Services.Configure<RabbitMqWorkOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<ArtStorageOptions>(builder.Configuration.GetSection(ArtStorageOptions.SectionName));

var storage=builder.Configuration.GetSection(ArtStorageOptions.SectionName).Get<ArtStorageOptions>()??new ArtStorageOptions();
if(!string.Equals(storage.Provider,"FileSystem",StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException($"Unsupported ArtStorage provider '{storage.Provider}'.");
if(string.IsNullOrWhiteSpace(storage.RootPath))
    throw new InvalidOperationException("ArtStorage:RootPath must be explicitly configured. Use a shared persistent volume when running multiple Kubernetes pods.");

builder.Services.AddSingleton<IArtBinaryStorage>(_=>new FileSystemArtBinaryStorage(Path.GetFullPath(storage.RootPath)));
builder.Services.AddSingleton<IArtImageProcessor,SkiaArtImageProcessor>();
builder.Services.AddSingleton<IWorkSignalBus,RabbitMqWorkSignalBus>();
builder.Services.AddScoped<IWorkStore,WorkStore>();
builder.Services.AddScoped<IWorkArtifactStore,WorkArtifactStore>();
builder.Services.AddScoped<IArtAssetStore,ArtAssetStore>();

builder.Services.AddScoped<IWorkStepHandler,ArtResolveWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler,ArtEncodeWebPWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler,ArtOptimizePngWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler,ArtInspectMetadataWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler,ArtValidateWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler,ArtFinalizeWorkStepHandler>();

// GenerateMaster is registered only when a real IArtMasterGenerator provider is configured.
builder.Services.AddHostedService<DurableWorkWorker>();

await builder.Build().RunAsync();
