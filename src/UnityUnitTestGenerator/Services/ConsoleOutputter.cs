using CSharpTools.TestRunner;
using Microsoft.Extensions.Hosting;
using Shared;
using UnitTestGenerator.Interface;

namespace UnitTestGenerator.Services;

public class ConsoleOutputter : IHostedService
{
    readonly IEnumerable<IOutputter> _outputters;

    public ConsoleOutputter(IEnumerable<IOutputter> outputters)
    {
        _outputters = outputters;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var outputter in _outputters)
        {
            outputter.OnOutput += OnOutput;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var outputter in _outputters)
        {
            outputter.OnOutput -= OnOutput;
        }

        return Task.CompletedTask;
    }

    private void OnOutput(object sender, string message)
    {
        var initialColor = Console.ForegroundColor;
        switch (sender)
        {
            case IUnitTestGenerator _:
                Console.ForegroundColor = ConsoleColor.Magenta;
                break;
            case IUnitTestRunner _:
                Console.ForegroundColor = ConsoleColor.Cyan;
                break;
            case IUnitTestFixer _:
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case IUnitTestEnhancer _:
                Console.ForegroundColor = ConsoleColor.Blue;
                break;
            case IUnitTestSorcerer _:
                Console.ForegroundColor = ConsoleColor.Green;
                break;
            case FunctionOutputter _:
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                break;
        }
        Console.WriteLine($"[{sender.GetType().Name}] - {message}");
        Console.ForegroundColor = initialColor;
    }
}