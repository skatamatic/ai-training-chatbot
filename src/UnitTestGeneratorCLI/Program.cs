using CSharpTools.SolutionTools;
using CSharpTools.TestRunner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAIAPI_Rystem;
using Shared;
using UnitTestGenerator;

namespace UnitTestGeneratorCLI;

class Program
{
    static async Task Main(string[] args)
    {
        using var scope = CreateHostBuilder(args).Build().Services.CreateScope();
        var services = scope.ServiceProvider;

        var generator = services.GetRequiredService<IUnitTestSorcerer>();
        await generator.GenerateAsync();

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
        var sorcererConfig = configuration.GetSection("SorcererConfig").Get<UnitTestSorcererConfig>();

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

        services.AddSingleton<ISolutionTools, SolutionTools>();

        services.AddSingleton<IUnitTestGenerator, DotNetUnitTestGenerator>(sp =>
        {
            var outputAction = new Action<string>(x => {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(x);
                Console.ForegroundColor = ConsoleColor.Gray;
            });

            return new DotNetUnitTestGenerator(generationConfig, sp.GetRequiredService<IOpenAIAPI>(), outputAction);
        });

        services.AddSingleton<IUnitTestRunner, NUnitTestRunner>(sp =>
        {
            var outputAction = new Action<string>(x => {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(x);
                Console.ForegroundColor = ConsoleColor.Gray;
            });
            return new NUnitTestRunner(outputAction);
        });

        services.AddSingleton<IUnitTestFixer, UnitTestFixer>(sp =>
        {
            var outputAction = new Action<string>(x => {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(x);
                Console.ForegroundColor = ConsoleColor.Gray;
            });
            return new UnitTestFixer(sp.GetRequiredService<IOpenAIAPI>(), outputAction);
        });


        services.AddSingleton<IUnitTestSorcerer, UnitTestSorcerer>(sp =>
        {
            var outputAction = new Action<string>(x => {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(x);
                Console.ForegroundColor = ConsoleColor.Gray;
            });
            return new UnitTestSorcerer(sorcererConfig, sp.GetRequiredService<IUnitTestFixer>(), sp.GetRequiredService<IUnitTestGenerator>(), sp.GetRequiredService<IUnitTestRunner>(), sp.GetRequiredService<ISolutionTools>(), outputAction);
        });
    }
}
