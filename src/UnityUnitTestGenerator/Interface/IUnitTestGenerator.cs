using UnitTestGenerator.Model;

namespace UnitTestGenerator.Interface;

public interface IUnitTestGenerator
{
    Task<UnitTestGenerationResult> AnalyzeOnly(string fileToTest);
    Task<UnitTestGenerationResult> Generate(string fileToTest);
}
