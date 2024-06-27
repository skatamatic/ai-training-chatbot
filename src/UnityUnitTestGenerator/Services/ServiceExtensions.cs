using CSharpTools.SolutionTools;
using CSharpTools.TestRunner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAIAPI_Rystem.Functions;
using Shared;
using UnitTestGenerator.Interface;
using UnitTestGenerator.Model;

namespace UnitTestGenerator.Services;

public static class ServiceExtensions
{
    public static void ConfigureSorcerer(this IServiceCollection services, IConfiguration configuration)
    {
        var generationConfig = configuration.GetSection("GenerationConfig").Get<GenerationConfig>();
        var sorcererConfig = configuration.GetSection("SorcererConfig").Get<UnitTestSorcererConfig>();

        services.AddSingleton(generationConfig);
        services.AddSingleton(sorcererConfig);

        services.AddSingleton<ISolutionTools, SolutionTools>();
        services.AddSingleton<ConsoleOutputter>();

        switch (sorcererConfig.Mode)
        {
            case SorcererMode.Unity:
                services.AddSingletonAndMonitorOutput<IUnitTestRunner, UnityTestRunner>();
                services.AddSingletonAndMonitorOutput<IUnitTestGenerator, UnityTestGenerator>();
                break;
            case SorcererMode.DotNet:
                services.AddSingletonAndMonitorOutput<IUnitTestRunner, NUnitTestRunner>();
                services.AddSingletonAndMonitorOutput<IUnitTestGenerator, DotNetUnitTestGenerator>();
                break;
        }

        services.AddSingletonAndMonitorOutput<IUnitTestFixer, UnitTestFixer>();
        services.AddSingletonAndMonitorOutput<IUnitTestEnhancer, UnitTestEnhancer>();
        services.AddSingletonAndMonitorOutput<IUnitTestSorcerer, UnitTestSorcerer>();

        services.AddSingleton<IOutputter, FunctionOutputter>();

        services.AddHostedService<ConsoleOutputter>();
    }

    public static IServiceCollection AddSingletonAndMonitorOutput<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService, IOutputter
    {
        services.AddSingleton<TService, TImplementation>();
        services.AddSingleton(provider => (IOutputter)provider.GetRequiredService<TService>());
        return services;
    }
}
