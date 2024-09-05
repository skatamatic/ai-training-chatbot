using CSharpTools.DefinitionAnalyzer;
using Sorcerer.Model;

namespace Sorcerer.Interface;

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
