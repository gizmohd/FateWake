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
builder.Services.Configure<ArtGenerationProviderOptions>(builder.Configuration.GetSection(ArtGenerationProviderOptions.SectionName));
builder.Services.Configure<LocalArtGenerationOptions>(builder.Configuration.GetSection(LocalArtGenerationOptions.SectionName));
builder.Services.Configure<ComfyUiArtGenerationOptions>(builder.Configuration.GetSection(ComfyUiArtGenerationOptions.SectionName));
builder.Services.Configure<OpenAiArtGenerationOptions>(builder.Configuration.GetSection(OpenAiArtGenerationOptions.SectionName));

var storage=builder.Configuration.GetSection(ArtStorageOptions.SectionName).Get<ArtStorageOptions>()??new ArtStorageOptions();
if(!string.Equals(storage.Provider,"FileSystem",StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException($"Unsupported ArtStorage provider '{storage.Provider}'.");
if(string.IsNullOrWhiteSpace(storage.RootPath))
    throw new InvalidOperationException("ArtStorage:RootPath must be explicitly configured. Use a shared persistent volume when running multiple Kubernetes pods.");

builder.Services.AddSingleton<IArtBinaryStorage>(_=>new FileSystemArtBinaryStorage(Path.GetFullPath(storage.RootPath)));
builder.Services.AddSingleton<IArtImageProcessor,SkiaArtImageProcessor>();
builder.Services.AddSingleton<IWorkSignalBus,RabbitMqWorkSignalBus>();
builder.Services.AddSingleton<WorkQueueWakeup>();
builder.Services.AddScoped<IWorkStore,WorkStore>();
builder.Services.AddScoped<IWorkArtifactStore,WorkArtifactStore>();
builder.Services.AddScoped<IArtAssetStore,ArtAssetStore>();
builder.Services.AddScoped<IArtGenerationStore,ArtGenerationStore>();

builder.Services.AddScoped<IWorkStepHandler,ArtResolveWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler,ArtGenerateMasterWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler,ArtEncodeWebPWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler,ArtOptimizePngWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler,ArtInspectMetadataWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler,ArtValidateWorkStepHandler>();
builder.Services.AddScoped<IWorkStepHandler,ArtFinalizeWorkStepHandler>();

var provider=builder.Configuration.GetSection(ArtGenerationProviderOptions.SectionName).Get<ArtGenerationProviderOptions>()??new ArtGenerationProviderOptions();
switch(provider.Provider.ToLowerInvariant())
{
    case "none":
        builder.Services.AddSingleton<IArtMasterGenerator,DisabledArtMasterGenerator>();
        break;
    case "openai":
        var openAi=builder.Configuration.GetSection(OpenAiArtGenerationOptions.SectionName).Get<OpenAiArtGenerationOptions>()??new OpenAiArtGenerationOptions();
        if(!openAi.Enabled||string.IsNullOrWhiteSpace(openAi.ApiKey))
            throw new InvalidOperationException("Enable ArtGeneration:OpenAI and configure its ApiKey when Provider is OpenAI.");
        builder.Services.AddHttpClient<IArtMasterGenerator,OpenAiArtMasterGenerator>(client=>client.BaseAddress=new Uri(openAi.BaseUrl));
        break;
    case "local":
        var local=builder.Configuration.GetSection(LocalArtGenerationOptions.SectionName).Get<LocalArtGenerationOptions>()??new LocalArtGenerationOptions();
        if(!local.Enabled)throw new InvalidOperationException("Enable ArtGeneration:Local when Provider is Local.");
        builder.Services.AddHttpClient<IArtMasterGenerator,LocalArtMasterGenerator>(client=>client.BaseAddress=new Uri(local.BaseUrl));
        break;
    case "comfyui":
        var comfy=builder.Configuration.GetSection(ComfyUiArtGenerationOptions.SectionName).Get<ComfyUiArtGenerationOptions>()??new ComfyUiArtGenerationOptions();
        if(!comfy.Enabled)throw new InvalidOperationException("Enable ArtGeneration:ComfyUI when Provider is ComfyUI.");
        builder.Services.AddHttpClient<IArtMasterGenerator,ComfyUiArtMasterGenerator>(client=>client.BaseAddress=new Uri(comfy.BaseUrl));
        break;
    default:
        throw new InvalidOperationException($"Unsupported artwork generation provider '{provider.Provider}'.");
}

builder.Services.AddHostedService<RabbitMqWorkSignalConsumer>();
builder.Services.AddHostedService<DurableWorkWorker>();

await builder.Build().RunAsync();
