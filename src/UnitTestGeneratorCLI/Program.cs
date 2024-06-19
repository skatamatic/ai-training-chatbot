using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAIAPI_Rystem;
using Shared;
using UnityUnitTestGenerator;

namespace UnitTestGeneratorCLI;

class Program
{
    static async Task Main(string[] args)
    {
        using var scope = CreateHostBuilder(args).Build().Services.CreateScope();
        var services = scope.ServiceProvider;

        var generator = services.GetRequiredService<IUnitTestGenerator>();
        await generator.Generate();

        Console.ReadLine();
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
            });

    static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        var openAIConfig = configuration.GetSection("OpenAIConfig").Get<OpenAIConfig>();
        var generationConfig = configuration.GetSection("GenerationConfig").Get<GenerationConfig>();

        services.AddSingleton(openAIConfig);

        services.AddSingleton<IOpenAIAPI, RystemFunctionAPI>();

        services.AddOpenAi(settings =>
        {
            settings.ApiKey = openAIConfig.ApiKey;
        });

        services.AddSingleton(generationConfig);
        services.AddSingleton<FunctionInvocationObserver>();
        services.AddSingleton<IFunctionInvocationObserver, FunctionInvocationObserver>(sp => sp.GetRequiredService<FunctionInvocationObserver>());
        services.AddSingleton<IFunctionInvocationEmitter, FunctionInvocationObserver>(sp => sp.GetRequiredService<FunctionInvocationObserver>());

        services.AddSingleton<IUnitTestGenerator, UnityTestGenerator>(sp =>
        {
            var outputAction = new Action<string>(Console.WriteLine);
            return new UnityTestGenerator(generationConfig, sp.GetRequiredService<IOpenAIAPI>(), outputAction);
        });
    }
}
