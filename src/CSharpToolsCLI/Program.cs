using CSharpTools;
using Microsoft.CodeAnalysis;

const string FILENAME = "D:\\Repos\\worklink-app\\Assets\\Scripts\\ARTracking\\ARTrackingPlatformServices\\MultiPoseGizmoController.cs";
const int MAX_DEPTH = 3;

var startCol = Console.ForegroundColor;
var startRow = Console.CursorTop;

Action<string> output = x =>
{
    ClearConsoleRow(startRow);
    var col = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write(x);
    Console.ForegroundColor = col;
};

/*var runner = new UnityTestRunner(output);
var result = await runner.RunTests("D:\\Repos\\worklink-app", UnityTestRunner.BuildSuiteFilter("TrackingCoordinator"));

if (!string.IsNullOrEmpty(result.Error))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: {result.Error}");
    Console.ForegroundColor = startCol;
    return;
}

Console.WriteLine($"\nAll Passed: {result.AllPassed}");
Console.WriteLine($"Total: {result.TestResults.Count}");
Console.WriteLine($"Failures: {result.TestResults.Count(x=> !x.Success)}");

foreach (var failure in result.TestResults.Where(x => !x.Success))
{
    Console.WriteLine($"Test: {failure.TestName} Class: {failure.ClassName} Info: {failure.Info}");
    Console.WriteLine(failure.Failure);
    Console.WriteLine(failure.Log);
    Console.WriteLine();
}*/



Console.WriteLine($"Loading file '{FILENAME}', max depth: {MAX_DEPTH}");

var finder = new ReferenceFinder(output);

Console.CursorVisible = false;
var defResult = await finder.FindDefinitions(FILENAME, MAX_DEPTH);
var analyzer = new DefinitionAnalyzer(finder, output);
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
    DefinitionAnalyzer.TestWorthyness.Excellent => ConsoleColor.Green,
    DefinitionAnalyzer.TestWorthyness.Okay => ConsoleColor.Yellow,
    _ => ConsoleColor.Red
};

Console.WriteLine($"\n------------------------\nDone! Found {analyisResult.Definitions.Count()} definitions\nAnticipated test quality: {analyisResult.TestWorthyness}\nContext LoC: {analyisResult.ContextLoc}\nTotal Loc: {analyisResult.TotalLoc}\n------------------------");

Console.ForegroundColor = ConsoleColor.Gray;

void ClearConsoleRow(int row)
{
    Console.CursorLeft = 0;
    Console.CursorTop = row;
    Console.Write("                                                                                                                                                                                         ");
    Console.CursorLeft = 0;
    Console.CursorTop = row;
}