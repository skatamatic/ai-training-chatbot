using CSharpTools.DefinitionAnalyzer;
using UnitTestGenerator.Model;

namespace UnitTestGenerator.Interface;

public enum EnhancementType
{
    General,
    Coverage,
    Refactor,
    Document,
    SquashBugs,
    Clean,
    Verify,
    Assess
}

public interface IUnitTestEnhancer
{
    EnhancementType ActiveMode { get; }
    Task<UnitTestGenerationResult> Enhance(AnalysisResult analysis, string uutPath, string testFilePath, EnhancementType type);
}
