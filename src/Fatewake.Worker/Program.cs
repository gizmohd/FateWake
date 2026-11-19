using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis");

builder.Services.AddDbContext<FatewakeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("fatewake")));

builder.Services.Configure<WorkExecutionOptions>(builder.Configuration.GetSection("Work"));
builder.Services.Configure<RabbitMqWorkOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<ArtStorageOptions>(builder.Configuration.GetSection(ArtStorageOptions.SectionName));
builder.Services.Configure<ArtGenerationProviderOptions>(builder.Configuration.GetSection(ArtGenerationProviderOptions.SectionName));
builder.Services.Configure<LocalArtGenerationOptions>(builder.Configuration.GetSection(LocalArtGenerationOptions.SectionName));
builder.Services.Configure<ComfyUiArtGenerationOptions>(builder.Configuration.GetSection(ComfyUiArtGenerationOptions.SectionName));
builder.Services.Configure<OpenAiArtGenerationOptions>(builder.Configuration.GetSection(OpenAiArtGenerationOptions.SectionName));

var storage = builder.Configuration.GetSection(ArtStorageOptions.SectionName).Get<ArtStorageOptions>() ?? new ArtStorageOptions();
switch (storage.Provider.ToLowerInvariant())
{
    case "postgres":
        builder.Services.AddScoped<IArtBinaryStorage, PostgresArtBinaryStorage>();
        break;
    case "filesystem":
        if (string.IsNullOrWhiteSpace(storage.RootPath))
            throw new InvalidOperationException("ArtStorage:RootPath must be explicitly configured for filesystem storage.");
        builder.Services.AddScoped<IArtBinaryStorage>(_ => new FileSystemArtBinaryStorage(Path.GetFullPath(storage.RootPath)));
        break;
    default:
        throw new InvalidOperationException($"Unsupported ArtStorage provider '{storage.Provider}'.");
}
builder.Services.AddSingleton<IArtImageProcessor, SkiaArtImageProcessor>();
builder.Services.AddSingleton<IWorkSignalBus, RabbitMqWorkSignalBus>();
builder.Services.AddSingleton<WorkQueueWakeup>();
builder.Services.AddScoped<IWorkStore, WorkStore>();
builder.Services.AddScoped<IWorkArtifactStore, WorkArtifactStore>();
builder.Services.AddScoped<IArtAssetStore, ArtAssetStore>();
builder.Services.AddScoped<IArtGenerationStore, ArtGenerationStore>();
builder.Services.AddScoped<IRagIngestionService, RagIngestionService>();
builder.Services.AddScoped<IRagStagingStore, RagStagingStore>();
builder.Services.AddScoped<IArtRegenerationStore, ArtRegenerationStore>();
builder.Services.AddScoped<IWorkJobBuilder, WorkJobBuilder>();
builder.Services.AddScoped<IArtWorkJobFactory, ArtWorkJobFactory>();
builder.Services.AddScoped<Fatewake.Observability.IAiUsageRecorder, Fatewake.Infrastructure.Usage.AiUsageRecorder>();

builder.Services.AddScoped<IWorkStepHandler, ArtResolveWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler, ArtGenerateMasterWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler, ArtEncodeWebPWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler, ArtOptimizePngWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler, ArtInspectMetadataWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler, ArtValidateWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler, ArtFinalizeWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler, RagImportWorkStepHandler>();

var provider = builder.Configuration.GetSection(ArtGenerationProviderOptions.SectionName).Get<ArtGenerationProviderOptions>() ?? new ArtGenerationProviderOptions();
switch (provider.Provider.ToLowerInvariant())
{
    case "none":
        builder.Services.AddSingleton<IArtMasterGenerator, DisabledArtMasterGenerator>();
        break;
    case "openai":
        var openAi = builder.Configuration.GetSection(OpenAiArtGenerationOptions.SectionName).Get<OpenAiArtGenerationOptions>() ?? new OpenAiArtGenerationOptions();
        if (!openAi.Enabled || string.IsNullOrWhiteSpace(openAi.ApiKey))
            throw new InvalidOperationException("Enable ArtGeneration:OpenAI and configure its ApiKey when Provider is OpenAI.");
        builder.Services.AddHttpClient<IArtMasterGenerator, OpenAiArtMasterGenerator>(client => client.BaseAddress = new Uri(openAi.BaseUrl));
        break;
    case "local":
        var local = builder.Configuration.GetSection(LocalArtGenerationOptions.SectionName).Get<LocalArtGenerationOptions>() ?? new LocalArtGenerationOptions();
        if (!local.Enabled) throw new InvalidOperationException("Enable ArtGeneration:Local when Provider is Local.");
        builder.Services.AddHttpClient<IArtMasterGenerator, LocalArtMasterGenerator>(client => client.BaseAddress = new Uri(local.BaseUrl));
        break;
    case "comfyui":
        var comfy = builder.Configuration.GetSection(ComfyUiArtGenerationOptions.SectionName).Get<ComfyUiArtGenerationOptions>() ?? new ComfyUiArtGenerationOptions();
        builder.Services.AddComfyUiMasterGenerator(builder.Configuration);
        builder.Services.AddHttpClient("art.comfyui.health", client => client.BaseAddress = new Uri(comfy.BaseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        builder.Services.AddHostedService<ArtRegenerationWorker>();
        break;
    default:
        throw new InvalidOperationException($"Unsupported artwork generation provider '{provider.Provider}'.");
}

builder.Services.AddHostedService<RabbitMqWorkSignalConsumer>();
builder.Services.AddHostedService<DurableWorkWorker>();

await builder.Build().RunAsync();
