using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Microsoft.EntityFrameworkCore;

var builder=Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddDbContext<FatewakeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("fatewake")));

builder.Services.Configure<WorkExecutionOptions>(builder.Configuration.GetSection("Work"));
builder.Services.Configure<RabbitMqWorkOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IWorkSignalBus,RabbitMqWorkSignalBus>();
builder.Services.AddScoped<IWorkStore,WorkStore>();
builder.Services.AddHostedService<DurableWorkWorker>();

await builder.Build().RunAsync();
