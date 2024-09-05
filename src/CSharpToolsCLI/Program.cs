using CSharpTools;
using CSharpTools.DefinitionAnalyzer;
using CSharpTools.ReferenceFinder;
using CSharpTools.SolutionTools;
using CSharpTools.TestRunner;
using CSharpTools.TestRunner.Unity;
using Microsoft.CodeAnalysis;

var startCol = Console.ForegroundColor;
var startRow = Console.CursorTop;

Action<string> output = x =>
{
    //ClearConsoleRow(startRow);
    var col = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write(x);
    if (!x.EndsWith('\n'))
        Console.WriteLine();
    Console.ForegroundColor = col;
};

await Test_TestRunner();
Console.ReadLine();

async Task Test_TestRunner()
{
    const string TEST_FILE = "D:\\Repos\\worklink-app";

    using var runner = new UnityWebClientTestRunner(new UnitySolutionTools());
    runner.OnOutput += (_, x) => output(x);
    var result = await runner.RunTestsAsync(TEST_FILE, "WebViewTextVariablesServiceTests");

    IEnumerable<TestCaseResult> filteredResult;

    if (result.Success)
    {
        filteredResult = result.PassedTests;
    }
    else
    {
        filteredResult = result.FailedTests.Concat(result.PassedTests);
    }

    
    foreach (var item in filteredResult)
    {
        Console.WriteLine($"Test: {item.FullName}");
        Console.WriteLine($"Result: {item.Result}");
        if (!string.IsNullOrEmpty(item.Message))
        {
            Console.WriteLine($"Message: {item.Message.Trim()}");
        }
        if (!string.IsNullOrEmpty(item.StackTrace))
        {
            Console.WriteLine($"StackTrace: {item.StackTrace.Trim()}");
        }
        Console.WriteLine();
    }

    if (result.BuildErrors.Any())
    {
        Console.WriteLine($"Build failed (s):\n{string.Join('\n', result.BuildErrors)}");
    }
    else if (result.Errors.Any())
    {
        Console.WriteLine($"Finished with error(s):\n{string.Join('\n', result.Errors)}");
    }
    else
    {
        Console.WriteLine($"Passed: {result.PassedTests.Count} Failed: {result.FailedTests.Count}");
    }
}

async Task Test_UnityTestRunner()
{
    const string TEST_PROJECT = "E:\\repos\\MobileHMI\\MDT.Framework\\DataServices.Tests\\DataServices.NUnit.Tests.csproj";

    var runner = new UnityTestRunner();
    runner.OnOutput += (_, x) => output(x);
    var result = await runner.RunTestsAsync(TEST_PROJECT, "FracAuto");

    Console.WriteLine();

    while (true)
    {
        if (result.BuildErrors.Any())
        {
            Console.WriteLine($"Build failed (s):\n{string.Join('\n', result.BuildErrors)}");
        }
        else if (result.Errors.Any())
        {
            Console.WriteLine($"Finished with error(s):\n{string.Join('\n', result.Errors)}");
        }
        else
        {
            Console.WriteLine($"Passed: {result.PassedTests.Count} Failed: {result.FailedTests.Count}");
        }

        if (!result.FailedTests.Any())
            break;

        Console.WriteLine("Re-running failed tests");
        result = await runner.RunFailuresAsync(TEST_PROJECT, result);
    }
}

async Task Test_Defs()
{
    const string FILENAME = "D:\\Repos\\worklink-app\\Assets\\ScopeAR\\Core\\Logging\\DefaultAirbrakeFilters.cs";
    const int MAX_DEPTH = 2;

    Console.WriteLine($"Loading file '{FILENAME}', max depth: {MAX_DEPTH}");

    var finder = new ReferenceFinderService(output);

    Console.CursorVisible = false;
    var defResult = await finder.FindDefinitions(FILENAME, MAX_DEPTH);
    var analyzer = new DefinitionAnalyzerService(finder, output);
    var analyisResult = await analyzer.Analyze(defResult, FILENAME);
    Console.CursorVisible = true;

    ClearConsoleRow(startRow);

    foreach (var def in analyisResult.Definitions)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"------------------------");
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"{def.FullName}");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"------------------------");
        if (def.Supplement != null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Supplement: {def.Supplement.Definition.FullName}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"{def.Supplement.ReasonForSupplementing}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"------------------------");
            SyntaxHighlighter.HighlightAndPrint(def.Supplement.Definition.Code);
            Console.WriteLine($"------------------------");
            Console.ForegroundColor = ConsoleColor.White;
        }
        SyntaxHighlighter.HighlightAndPrint(def.Code);
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
    }

    Console.ForegroundColor = analyisResult.TestWorthyness switch
    {
        TestWorthyness.Excellent => ConsoleColor.Green,
        TestWorthyness.Okay => ConsoleColor.Yellow,
        _ => ConsoleColor.Red
    };

    Console.WriteLine($"\n------------------------\nDone! Found {analyisResult.Definitions.Count()} definitions\nAnticipated test quality: {analyisResult.TestWorthyness}\nContext LoC: {analyisResult.ContextLoc}\nTotal Loc: {analyisResult.TotalLoc}\n------------------------");

    Console.ForegroundColor = ConsoleColor.Gray;
}

void ClearConsoleRow(int row)
{
    Console.CursorLeft = 0;
    Console.CursorTop = row;
    Console.Write(new string(' ', 1000));
    Console.CursorLeft = 0;
    Console.CursorTop = row;
}