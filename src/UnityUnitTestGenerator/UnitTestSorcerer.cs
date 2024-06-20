using CSharpTools.SolutionTools;
using CSharpTools.TestRunner;

namespace UnitTestGenerator;

public class UnitTestSorcererConfig
{
    public int MaxFixAttempts { get; set; } = 3;
}

public interface IUnitTestSorcerer
{
    Task<bool> GenerateAsync();
}

public class UnitTestSorcerer : IUnitTestSorcerer
{
    IUnitTestFixer fixer;
    IUnitTestGenerator generator;
    IUnitTestRunner runner;
    ISolutionTools solutionTools;

    Action<string> output;
    UnitTestSorcererConfig config;

    public UnitTestSorcerer(UnitTestSorcererConfig config, IUnitTestFixer fixer, IUnitTestGenerator generator, IUnitTestRunner runner, ISolutionTools solutionTools, Action<string> output)
    {
        this.fixer = fixer;
        this.generator = generator;
        this.runner = runner;
        this.output = output;
        this.solutionTools = solutionTools;
        this.config = config;
    }

    public async Task<bool> GenerateAsync()
    {
        output("Generating tests...");
        var genResult = await generator.Generate();

        var testFilePath = await solutionTools.SaveTestFile(genResult.Config.FileToTest, genResult.AIResponse.TestFileContent);
        output($"Tests written to {testFilePath}");

        output("Running tests...");

        string testFileProject = solutionTools.FindProjectFile(testFilePath);
        var runResult = await runner.RunTestsAsync(testFileProject);

        if (runResult.Success)
        {
            output("Success!");
            return true;
        }

        UnitTestGenerationResult lastResult = genResult;

        for (int i = 0; i < config.MaxFixAttempts; i++)
        {
            output($"Failed...  Attempting to fix ({i+1}/{config.MaxFixAttempts})");
            lastResult = await fixer.Fix(new FixContext() { Attempt = i, LastGenerationResult = lastResult, LastTestRunResults = runResult, InitialConfig = genResult.Config }, testFilePath);

            await solutionTools.WriteSourceFile(testFilePath, lastResult.AIResponse.TestFileContent);
            output($"Fixed tests written to {testFilePath}");

            output("Running fixed tests...");
            runResult = await runner.RunTestsAsync(testFileProject);

            if (runResult.Success)
            {
                output("Success!");
                return true;
            }
        }

        output("Failed to fix tests after max attempts.");
        return false;
    }
}
