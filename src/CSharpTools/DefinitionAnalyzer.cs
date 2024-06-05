using static CSharpTools.ReferenceFinder;

namespace CSharpTools
{
    public class DefinitionAnalyzer
    {
        public enum TestWorthyness
        {
            Excellent,
            Okay,
            Poor
        }

        public class AnalysisResult
        {
            public int Supplements { get; set; }
            public TestWorthyness TestWorthyness { get; set; }
            public int ContextLoc { get; set; }
            public int TotalLoc { get; set; }
            public IEnumerable<Definition> Definitions { get; set; }
        }

        Action<string> output;
        const int EXCELLENT_LINES_OF_CODE = 500;
        const int OK_LINES_OF_CODE = 1000;

        static readonly Dictionary<string, (string reason, string type)> Supplmments = new()
        {
            ["ITimeProvider"] = ("Time providers should be mocked", "MockTimeProvider"),
            ["ITickProvider"] = ("Tick providers should be mocked", "MockTickProvider")
        };

        ReferenceFinder finder;

        public DefinitionAnalyzer(ReferenceFinder finder, Action<string> output)
        {
            this.finder = finder;
            this.output = output;
        }   

        public async Task<AnalysisResult> Analyze(IEnumerable<DefinitionResult> definitionResults, string uutFile)
        {
            var result = new AnalysisResult();
            result.Definitions = definitionResults.SelectMany(x => x.Definitions.Values).Distinct(new DefinitionCompararer());

            output("Analyzing definitions...");

            foreach (var definition in result.Definitions)
            {
                if (Supplmments.TryGetValue(definition.Symbol, out var supplement))
                {
                    output($"Supplementing symbol {definition.Symbol} with {supplement.type} because {supplement.reason}");

                    var supplementDef = await finder.FindSingleClassDefinitionAsync(definitionResults.First().File, supplement.type);

                    output($"Supplement {supplement.type} resolved");

                    if (supplementDef != null && supplementDef.Definitions.Count > 0)
                    {
                        result.Supplements++;
                        definition.Supplement = new DefinitionSupplement() { Definition = supplementDef.Definitions.Values.First(), ReasonForSupplementing = supplement.reason };
                    }
                }
            }

            var uutFileContentLength = File.ReadAllText(uutFile).Split('\n').Length;
            result.ContextLoc = result.Definitions.Sum(x => x.Code.Split('\n').Length);
            result.TotalLoc = result.ContextLoc + uutFileContentLength;

            if (result.TotalLoc > OK_LINES_OF_CODE)
            {
                result.TestWorthyness = TestWorthyness.Poor;
            }
            else if (result.TotalLoc > EXCELLENT_LINES_OF_CODE)
            {
                result.TestWorthyness = TestWorthyness.Okay;
            }
            else
            {
                result.TestWorthyness = TestWorthyness.Excellent;
            }

            return result;
        }
    }
}
