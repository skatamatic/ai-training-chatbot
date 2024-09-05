using CSharpTools.TestRunner;
using Microsoft.Extensions.Hosting;
using Shared;
using SixLabors.ImageSharp.Processing;
using Sorcerer.Interface;
using Sorcerer.Model;
using Sorcerer.Services;
using Spectre.Console;
using System.Text;
using System.Text.RegularExpressions;

namespace Sorcerer.Console;

public class ConsoleOutputter : IHostedService
{
    readonly IEnumerable<IOutputter> _outputters;
    readonly UnitTestSorcererConfig _config;
    readonly Status status;
    readonly CancellationTokenSource cts = new();
    readonly SemaphoreSlim semaphore = new(1, 1);

    string lastAction;
    bool suspendSpinner = false;
    bool presentationSkipped = false;

    public ConsoleOutputter(IEnumerable<IOutputter> outputters, UnitTestSorcererConfig config)
    {
        _outputters = outputters;
        _config = config;

        if (_config.Beautify)
        {
            System.Console.OutputEncoding = Encoding.UTF8;

            status = AnsiConsole.Status()
                    .AutoRefresh(false)
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("green bold"));

            Task.Run(() => status.StartAsync("Starting...", async ctx =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (lastAction == null || suspendSpinner)
                        continue;

                    await semaphore.WaitAsync();
                    await Task.Delay(50);

                    if (ctx.Status != lastAction)
                        ctx.Status(lastAction);

                    ctx.Refresh();

                    semaphore.Release();
                }
            }));
        }
    }

    private void PauseForPresentation(string senderName)
    {
        if (senderName == lastSender)
            return;

        lastSender = senderName;

        if (!_config.PresentationMode || presentationSkipped)
            return;

        suspendSpinner = true;

        var key = System.Console.ReadKey(true);

        if (key.Key == ConsoleKey.Enter)
        {
            presentationSkipped = true;
        }

        suspendSpinner = false;
    }

    async Task ShowSplash()
    {
        var image = new CanvasImage("Resources/splash.png");
        image.NearestNeighborResampler();
        Align layout;

        for (int i = 5; i < System.Console.WindowWidth / 2.5 - 5; i += 1 + (int)(i * 0.1))
        {
            image.MaxWidth = i;
            layout = new Align(image, HorizontalAlignment.Center, VerticalAlignment.Middle);

            AnsiConsole.Write(layout);
            await Task.Delay(40);
        }

        await Task.Delay(400);
        image.MaxWidth = System.Console.WindowWidth / 3 - 5;

        for (int i = 0; i < 20; i++)
        {
            image.MaxWidth = (int)(image.MaxWidth * 0.9);

            if (image.MaxWidth < 3)
                break;

            image.Mutate(x => x.Skew(0.1f + i * 0.6f, 0.1f + i * 0.2f));

            layout = new Align(image, HorizontalAlignment.Center, VerticalAlignment.Middle);

            try
            {
                AnsiConsole.Write(layout);

                image.Mutate(x => x.Pixelate(2).Brightness(1.05f));
                layout = new Align(image, HorizontalAlignment.Center, VerticalAlignment.Middle);
                AnsiConsole.Write(layout);
            }
            catch { }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        AnsiConsole.Write(
                new FigletText("[Sorcerer v1]")
                    .Centered()
                    .Color(Color.LightSkyBlue3));

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        await Task.Delay(400);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ShowSplash();

        PauseForPresentation("StartAsync");

        foreach (var outputter in _outputters)
        {
            outputter.OnOutput += OnOutput;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var outputter in _outputters)
        {
            outputter.OnOutput -= OnOutput;
        }

        cts.Cancel();

        return Task.CompletedTask;
    }

    string nonRoboAction = "";

    private void Done(bool success)
    {
        if (success)
        {
            AnsiConsole.Write(
                new FigletText("Success :)")
                    .Centered()
                    .Color(Color.Green));
        }
        else
        {
            AnsiConsole.Write(
                new FigletText("Failed")
                    .Centered()
                    .Color(Color.Red));
        }
    }

    string lastSender = "";
    string lastMessage = "";
    private async void OnOutput(object sender, string message)
    {
        await semaphore.WaitAsync();
        try
        {
            var initialColor = System.Console.ForegroundColor;

            string color = "darkgray";
            string action = "";

            if (sender == null)
            {
                lastSender = "";
                return;
            }

            switch (sender)
            {
                case IUnitTestGenerator generator:
                    color = "magenta";
                    action = generator.IsOnlyAnalyzing ? "Gathering magic ingredients..." : "Crafting tests...";
                    nonRoboAction = generator.IsOnlyAnalyzing ? "to analyze dependencies" : "to generate tests";
                    break;
                case IUnitTestRunner runner:
                    color = "cyan";
                    switch (runner.CurrentAction)
                    {
                        case TestRunnerAction.Compiling:
                            action = "Compiling; I hope it works...";
                            nonRoboAction = "to compile tests";
                            break;
                        case TestRunnerAction.Validating:
                            action = "Making sure I can test...";
                            nonRoboAction = "to make sure the test runner works";
                            break;
                        case TestRunnerAction.Running:
                        default:
                            action = "Running the tests, fingers crossed...";
                            nonRoboAction = "to run tests";
                            break;
                    }
                    break;
                case IUnitTestFixer _:
                    color = "yellow";
                    action = "Fixing my mistakes...";
                    nonRoboAction = "to fix tests";
                    break;
                case IUnitTestEnhancer enhancer:
                    color = "blue";
                    switch (enhancer.ActiveMode)
                    {
                        case EnhancementType.General:
                            action = "Casting general enhancement spell...";
                            nonRoboAction = "for general test improvements";
                            break;
                        case EnhancementType.Clean:
                            action = "Cleaning up my crappy code...";
                            nonRoboAction = "to clean up the code a bit";
                            break;
                        case EnhancementType.Document:
                            action = "Documenting so you don't have to.  Lucky for you the sorcerer loves documenting...";
                            nonRoboAction = "for some nice comments";
                            break;
                        case EnhancementType.SquashBugs:
                            action = "Looking for bugs to zap with my epic sorcerer staff...";
                            nonRoboAction = "to hunt any bugs they find";
                            break;
                        case EnhancementType.Verify:
                            action = "Making sure everything still works...";
                            nonRoboAction = "to ensure everything still works";
                            break;
                        case EnhancementType.Coverage:
                            action = "Looking for more angles of attack...";
                            nonRoboAction = "for some more test coverage";
                            break;
                        case EnhancementType.Refactor:
                            action = "Alchemizing (refactoring) a bit...";
                            nonRoboAction = "to look for refactoring opportunities";
                            break;
                        case EnhancementType.Assess:
                            action = "Thinking deeply about the right spells to use...";
                            nonRoboAction = "how to enhance these tests";
                            break;
                        default:
                            action = "Enhancing (???)...";
                            nonRoboAction = "to do... something";
                            break;
                    }

                    break;
                case IUnitTestSorcerer _:
                    if (message == "Success!")
                    {
                        Done(true);
                        return;
                    }
                    else if (message == "Failed")
                    {
                        Done(false);
                        return;
                    }
                    color = "green";
                    action = "Doing some generic sorcery...";
                    nonRoboAction = "to do sorcery";
                    break;
                case IOpenAIAPI _:
                    color = "mediumorchid1_1";
                    action = $"Asking the sorcerer {nonRoboAction}...";
                    break;
                case FunctionOutputter _:
                    color = "darkgreen";
                    action = "Executing function...";
                    break;
            }

            string senderName = sender.GetType().Name;
            lastAction = action;

            //Don't output duplicate messages
            if (senderName == lastSender && message == lastMessage)
            {
                return;
            }

            if (!message.Trim().Contains("\n") || !_config.Beautify)
            {
                AnsiConsole.MarkupLine($"[[[{color}]{senderName}[/]]] [deepskyblue4_1]{message.Replace("[", "[[").Replace("]", "]]")}[/]");
            }
            else
            {
                var lines = message.Trim().Split('\n');
                suspendSpinner = true;
                await Task.Delay(100 + Math.Min(100, 3 * lines.Count()));

                AnsiConsole.MarkupLine($"[[[{color}]{senderName}[/]]] [deepskyblue4_1]{lines.First().Replace("[", "[[").Replace("]", "]]")}[/]");
                BeautifyCodeSections(lines.Skip(1).ToArray(), color);

                await Task.Delay(100 + Math.Min(100, 3 * lines.Count()));
                suspendSpinner = false;
            }

            PauseForPresentation(senderName);
        }
        finally
        {
            semaphore.Release();
            lastMessage = message;
        }
    }

    private void BeautifyCodeSections(string[] lines, string baseColor)
    {
        bool isCode = false;
        string startRegex = "^-{2,}.*start.*code.*-{2,}$";
        string endRegex = "^-{2,}.*end.*code.*-{2,}$";
        string lastLine = "";
        int consecutiveBlankLines = 0;

        foreach (var line in lines.Select(x => x.Trim('\r', '\n')))
        {
            bool isMatchLine = false;

            if (Regex.IsMatch(line, startRegex, RegexOptions.IgnoreCase))
            {
                isCode = true;
                consecutiveBlankLines = 0;
                isMatchLine = true;
            }
            else if (Regex.IsMatch(line, endRegex, RegexOptions.IgnoreCase))
            {
                isCode = false;
                consecutiveBlankLines = 0;
                isMatchLine = true;
            }
            else if (line.Trim() == "-----START OF ISSUES-----" || line.Trim() == "-----END OF ISSUES----")
            {
                isMatchLine = true;
                consecutiveBlankLines = 0;
            }

            if (string.IsNullOrWhiteSpace(line) || isMatchLine)
            {
                consecutiveBlankLines++;
                if (consecutiveBlankLines <= 1)
                {
                    AnsiConsole.WriteLine();
                }
            }
            else if (!isMatchLine)
            {
                if (isCode)
                {
                    SyntaxHighlighter.HighlightAndPrint(line);
                }
                else
                {
                    // Check if the line contains a colon
                    if (line.IndexOf(':') > 0)
                    {
                        var colonIndex = line.IndexOf(':');
                        if (colonIndex < 24)
                        {
                            var leftPart = line.Substring(0, colonIndex + 1);
                            var rightPart = line.Substring(colonIndex + 1);
                            AnsiConsole.MarkupLine($"[{baseColor}]{leftPart.Replace("[", "[[").Replace("]", "]]")}[/][deepskyblue4_1]{rightPart.Replace("[", "[[").Replace("]", "]]")}[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[deepskyblue4_1]{line.Replace("[", "[[").Replace("]", "]]")}[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[deepskyblue4_1]{line.Replace("[", "[[").Replace("]", "]]")}[/]");
                    }
                }
                consecutiveBlankLines = 0;
            }
            lastLine = line;
        }

        if (lines.Length > 1 && !string.IsNullOrWhiteSpace(lastLine) && consecutiveBlankLines <= 1)
        {
            AnsiConsole.WriteLine();
        }
    }

}