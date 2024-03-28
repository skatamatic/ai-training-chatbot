﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAIAPI_BasicRest;
using OpenAIAPI_Rystem;
using OpenAIAPI_Rystem.Functions;
using Rystem.OpenAi.Chat;
using ServiceInterface;
using System;
using System.Collections.Generic;
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
        //services.AddSingleton<IOpenAIAPI, RestOpenAIAPI>();
        //services.AddSingleton<IOpenAIAPI, OpenAIRSystemAPI>();

        //This is the most robust API implementation.  It includes a bug fix for multiple functions and handles FunctionChain functions
        services.AddSingleton<IOpenAIAPI, OpenAIFunctionChainerAPI>();

        services.AddOpenAi(settings =>
        {
            settings.ApiKey = openAIConfig.ApiKey;
        });

        var openWeatherConfig = configuration.GetSection("OpenWeatherMapConfig").Get<OpenWeatherConfig>();

        services.AddSingleton(openWeatherConfig);
        services.AddSingleton<IWeatherService, WeatherService>();
        services.AddOpenAiChatFunction<WeatherAPIFunction>();

        //services.AddSingleton<ISystemService, SystemService>();
        //services.AddOpenAiChatFunction<EnumerateFileSystemFunction>();
        //services.AddOpenAiChatFunction<GetFileContentsSystemFunction>();
        //services.AddOpenAiChatFunction<FunctionChainFunction>();
        //services.AddOpenAiChatFunction<WriteFileContentsSystemFunction>();
        //services.AddOpenAiChatFunction<ExecutePowerShellScriptSystemFunction>();

        services.AddSingleton<IInfluxService, InfluxService>();
        services.AddOpenAiChatFunction<InfluxQueryAPIFunction>();
        services.AddOpenAiChatFunction<InfluxWriteAPIFunction>();

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