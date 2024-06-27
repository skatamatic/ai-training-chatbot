using CSharpTools.DefinitionAnalyzer;
using UnitTestGenerator.Model;

namespace UnitTestGenerator.Interface;

public interface IUnitTestEnhancer
{
    Task<UnitTestGenerationResult> Enhance(AnalysisResult analysis, string uutPath, string testFilePath);
}
