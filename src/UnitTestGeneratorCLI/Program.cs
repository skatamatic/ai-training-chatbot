using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAIAPI_Rystem;
using OpenAIAPI_Rystem.Functions;
using Shared;
using UnitTestGenerator.Interface;
using UnitTestGenerator.Services;

namespace UnitTestGeneratorCLI;

class Program
{
    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        // Create a scope to resolve your services
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Start the host to ensure all hosted services are started
        await host.StartAsync();

        var generator = services.GetRequiredService<IUnitTestSorcerer>();
        await generator.GenerateAsync();

        // Ensure graceful shutdown
        await host.StopAsync();
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, builder) =>
            {
                var env = context.HostingEnvironment;

                builder.SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appSettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                        .AddJsonFile("secrets.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                ConfigureServices(context.Configuration, services);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            });

    static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        var openAIConfig = configuration.GetSection("OpenAIConfig").Get<OpenAIConfig>();
        
        services.AddSingleton(openAIConfig);

        services.AddSingleton<IOpenAIAPI, RystemFunctionAPI>();

        services.AddOpenAi(settings =>
        {
            settings.ApiKey = openAIConfig.ApiKey;
        });

        services.AddSingleton<FunctionInvocationObserver>();
        services.AddSingleton<IFunctionInvocationObserver, FunctionInvocationObserver>(sp => sp.GetRequiredService<FunctionInvocationObserver>());
        services.AddSingleton<IFunctionInvocationEmitter, FunctionInvocationObserver>(sp => sp.GetRequiredService<FunctionInvocationObserver>());
        services.AddOpenAiChatFunction<NCalcFunction>();

        services.ConfigureSorcerer(configuration);
    }
}
