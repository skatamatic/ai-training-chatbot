using CSharpTools.DefinitionAnalyzer;

namespace UnitTestGenerator.Model;

public class UnitTestGenerationResult
{
    public UnitTestAIResponse AIResponse { get; set; }
    public AnalysisResult Analysis { get; set; }
    public string ChatSession { get; set; }
}
