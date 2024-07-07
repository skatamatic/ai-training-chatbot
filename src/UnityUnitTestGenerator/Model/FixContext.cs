using CSharpTools.TestRunner;

namespace UnitTestGenerator.Model;

public class FixContext
{
    public int Attempt { get; set; }
    public TestRunResult LastTestRunResults { get; set; }
    public UnitTestGenerationResult LastGenerationResult { get; set; }

    public string ProjectRootPath { get; set; }
}
