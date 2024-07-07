using UnitTestGenerator.Model;

namespace UnitTestGenerator.Interface;

public interface IUnitTestGenerator
{
    bool IsOnlyAnalyzing { get; }

    Task<UnitTestGenerationResult> AnalyzeOnly(string fileToTest);
    Task<UnitTestGenerationResult> Generate(string fileToTest);
}
