using CSharpTools.ReferenceFinder;

namespace CSharpTools.DefinitionAnalyzer;

public class DefinitionAnalyzerService
{
    private readonly Action<string> _output;
    private const int ExcellentLinesOfCode = 500;
    private const int OkLinesOfCode = 1000;

    private static readonly Dictionary<string, (string Reason, string Type)> Supplements = new()
    {
        ["ITimeProvider"] = ("Time providers should be mocked", "MockTimeProvider"),
        ["ITickProvider"] = ("Tick providers should be mocked", "MockTickProvider")
    };

    private readonly ReferenceFinderService _finder;

    public DefinitionAnalyzerService(ReferenceFinderService finder, Action<string> output)
    {
        _finder = finder;
        _output = output;
    }

    public async Task<AnalysisResult> Analyze(IEnumerable<DefinitionResult> definitionResults, string uutFile)
    {
        var result = InitializeAnalysisResult(definitionResults);
        _output("Analyzing definitions...");

        foreach (var definition in result.Definitions)
        {
            await ProcessDefinitionAsync(definition, definitionResults, result);
        }

        var uutFileContentLength = GetFileLinesCount(uutFile);
        result.ContextLoc = CalculateContextLoc(result.Definitions);
        result.TotalLoc = result.ContextLoc + uutFileContentLength;

        result.TestWorthyness = DetermineTestWorthyness(result.TotalLoc);

        return result;
    }

    private AnalysisResult InitializeAnalysisResult(IEnumerable<DefinitionResult> definitionResults)
    {
        Dictionary<string, Definition> combined = new();

        foreach (var kvp in definitionResults.SelectMany(x=>x.Definitions))
        {
            if (!combined.ContainsKey(kvp.Key))
            {
                combined[kvp.Key] = kvp.Value;
            }
        }

        var result = new AnalysisResult
        {
            Definitions = combined.Select(x=>x.Value)
        };

        return result;
    }

    private async Task ProcessDefinitionAsync(Definition definition, IEnumerable<DefinitionResult> definitionResults, AnalysisResult result)
    {
        if (Supplements.TryGetValue(definition.Symbol, out var supplement))
        {
            _output($"Supplementing symbol {definition.Symbol} with {supplement.Type} because {supplement.Reason}");

            var supplementDef = await _finder.FindSingleClassDefinitionAsync(definitionResults.First().File, supplement.Type);

            _output($"Supplement {supplement.Type} resolved");

            if (supplementDef != null && supplementDef.Definitions.Count > 0)
            {
                result.Supplements++;
                definition.Supplement = new DefinitionSupplement
                {
                    Definition = supplementDef.Definitions.Values.First(),
                    ReasonForSupplementing = supplement.Reason
                };
            }
        }
    }

    private int GetFileLinesCount(string filePath)
    {
        return File.ReadAllText(filePath).Split('\n').Length;
    }

    private int CalculateContextLoc(IEnumerable<Definition> definitions)
    {
        return definitions.Sum(x => x.Code.Split('\n').Length);
    }

    private TestWorthyness DetermineTestWorthyness(int totalLoc)
    {
        if (totalLoc > OkLinesOfCode)
        {
            return TestWorthyness.Poor;
        }
        else if (totalLoc > ExcellentLinesOfCode)
        {
            return TestWorthyness.Okay;
        }
        else
        {
            return TestWorthyness.Excellent;
        }
    }
}
