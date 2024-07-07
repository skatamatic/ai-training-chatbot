using CSharpTools.SolutionTools;
using CSharpTools.TestRunner;
using CSharpTools.TestRunner.Unity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddSingleton<ConsoleOutputter>();

        switch (sorcererConfig.Mode)
        {
            case SorcererMode.Unity:
                services.AddSingleton<ISolutionTools, UnitySolutionTools>();
                services.AddSingletonAndMonitorOutput<IUnitTestRunner, UnityWebClientTestRunner>();
                services.AddSingletonAndMonitorOutput<IUnitTestGenerator, UnityTestGenerator>();
                break;
            case SorcererMode.DotNet:
                services.AddSingleton<ISolutionTools, BaseSolutionTools>();
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
