using DeFi_Strategies.Tron.CompoundUSDD;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using ServiceStack.Text;
using TronNet;

IServiceProvider serviceProvider = RegisterServices();
Logger logger = LogManager.GetCurrentClassLogger();
logger.Info("Application started");

await serviceProvider.GetService<AutoCompounder>().AutocompoundAsync();

if (serviceProvider != null && serviceProvider is IDisposable sp)
    sp.Dispose();

logger.Info("Execution finished. Press any key to exit.");
Console.ReadKey();

static IServiceProvider RegisterServices()
{
    var services = new ServiceCollection();

    var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
    LogManager.Configuration.Variables["mydir"] = logsPath;

    var config = new ConfigurationBuilder()
        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("appsettings.json").Build();

    var section = config.GetSection(nameof(CompoundingConfig));
    CompoundingConfig compoundingConfig = section.Get<CompoundingConfig>();

    services.AddTronNet(x =>
    {
        x.Network = TronNetwork.MainNet;
        x.Channel = new GrpcChannelOption { Host = "grpc.trongrid.io", Port = 50051 };
        x.SolidityChannel = new GrpcChannelOption { Host = "grpc.trongrid.io", Port = 50052 };
        x.ApiKey = compoundingConfig.TronGridAPIKey;
    });

    services.AddLogging();
    services.AddSingleton<AutoCompounder>();
    services.AddSingleton(compoundingConfig);

    return services.BuildServiceProvider();
}