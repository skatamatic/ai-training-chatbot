using CSharpTools.DefinitionAnalyzer;

namespace Sorcerer.Model;

public class UnitTestGenerationResult
{
    public UnitTestAIResponse AIResponse { get; set; }
    public AnalysisResult Analysis { get; set; }
    public string ChatSession { get; set; }
}
