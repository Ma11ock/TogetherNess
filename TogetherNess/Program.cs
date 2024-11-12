using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using TogetherNess;
using TogetherNess.Settings;
using ILogger = Microsoft.Extensions.Logging.ILogger;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<AppSettings>()
    .BindConfiguration(nameof(AppSettings))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var configuration = builder.Configuration;

builder.Services.AddSerilog(new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger());

builder.Services.AddSingleton<Game1>();

IHost host = builder.Build();

ILogger logger = host.Services.GetRequiredService<ILogger<Program>>();

AppSettings settings = host.Services.GetRequiredService<IOptions<AppSettings>>().Value;

logger.LogInformation("Starting TogetherNess...");
logger.LogInformation("Version: {0}", Assembly.GetExecutingAssembly().GetName().Version);
logger.LogInformation("With appsettings:\nMappers directory: {0}", settings.MappersDirectory);

using Game1 game = host.Services.GetRequiredService<Game1>();
game.Run();