using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAIAPI_Rystem;
using OpenAIAPI_Rystem.Functions;
using Shared;
using System;
using System.IO;
using System.Windows;

namespace AI_Training_API;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
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
            .Build();
    }

    private void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        var openAIConfig = configuration.GetSection("OpenAIConfig").Get<OpenAIConfig>();

        services.AddSingleton(openAIConfig);

        services.AddSingleton<IOpenAIAPI, RystemFunctionAPI>();

        services.AddOpenAi(settings =>
        {
            settings.ApiKey = openAIConfig.ApiKey;
        });

        var openWeatherConfig = configuration.GetSection("OpenWeatherMapConfig").Get<OpenWeatherConfig>();

        services.AddSingleton(openWeatherConfig);
        services.AddSingleton<IWeatherService, WeatherService>();
        services.AddOpenAiChatFunction<WeatherAPIFunction>();

        services.AddOpenAiChatFunction<FunctionChainFunction>();

        services.AddSingleton<ISystemService, SystemService>();
        services.AddOpenAiChatFunction<EnumerateFileSystemFunction>();
        services.AddOpenAiChatFunction<GetFileContentsSystemFunction>();
        services.AddOpenAiChatFunction<WriteFileContentsSystemFunction>();
        services.AddOpenAiChatFunction<ExecutePowerShellScriptSystemFunction>();

        services.AddSingleton<IMySqlService, MySqlService>();
        services.AddOpenAiChatFunction<MySqlQueryAPIFunction>();
        services.AddOpenAiChatFunction<MySqlNonQueryAPIFunction>();

        services.AddSingleton<ICSharpService, CSharpService>();
        services.AddOpenAiChatFunction<CSharpDefinitionsFunction>();

        services.AddSingleton<ITestRunnerService, UnityTestRunnerService>();
        services.AddOpenAiChatFunction<TestRunnerFunction>();

        services.AddSingleton<FunctionInvocationObserver>();
        services.AddSingleton<IFunctionInvocationObserver, FunctionInvocationObserver>(sp => sp.GetRequiredService<FunctionInvocationObserver>());
        services.AddSingleton<IFunctionInvocationEmitter, FunctionInvocationObserver>(sp => sp.GetRequiredService<FunctionInvocationObserver>());

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton(provider =>
        {
            var viewModel = provider.GetService<MainWindowViewModel>();
            var mainWindow = new MainWindow
            {
                DataContext = viewModel
            };
            return mainWindow;
        });
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var mainWindow = _host.Services.GetService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.Dispose();
        base.OnExit(e);
    }
}
