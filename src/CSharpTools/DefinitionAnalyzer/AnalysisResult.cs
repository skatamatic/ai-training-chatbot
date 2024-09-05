using CSharpTools.ReferenceFinder;

namespace CSharpTools.DefinitionAnalyzer;

public class AnalysisResult
{
    public int Supplements { get; set; }
    public TestWorthyness TestWorthyness { get; set; }
    public int ContextLoc { get; set; }
    public int TotalLoc { get; set; }
    public IEnumerable<Definition> Definitions { get; set; }

    public string TargetFileContent { get; set; }
}