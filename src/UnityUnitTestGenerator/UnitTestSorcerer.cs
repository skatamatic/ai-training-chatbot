using CSharpTools.DefinitionAnalyzer;
using CSharpTools.SolutionTools;
using CSharpTools.TestRunner;

namespace UnitTestGenerator;

public class UnitTestSorcererConfig
{
    public int MaxFixAttempts { get; set; } = 3;
    public int EnhancementPasses { get; set; } = 2;
    public string FileToTest { get; set; }
    public bool SkipToEnhanceIfTestsExist { get; set; } = false;
}

public interface IUnitTestSorcerer
{
    Task<bool> GenerateAsync();
}

public class UnitTestSorcerer : IUnitTestSorcerer
{
    private readonly IUnitTestFixer _fixer;
    private readonly IUnitTestGenerator _generator;
    private readonly IUnitTestRunner _runner;
    private readonly IUnitTestEnhancer _enhancer;
    private readonly ISolutionTools _solutionTools;
    private readonly Action<string> _output;
    private readonly UnitTestSorcererConfig _config;

    public UnitTestSorcerer(UnitTestSorcererConfig config, IUnitTestFixer fixer, IUnitTestGenerator generator, IUnitTestRunner runner, ISolutionTools solutionTools, IUnitTestEnhancer enhancer, Action<string> output)
    {
        _fixer = fixer;
        _generator = generator;
        _runner = runner;
        _solutionTools = solutionTools;
        _enhancer = enhancer;
        _output = output;
        _config = config;
    }

    public async Task<bool> GenerateAsync()
    {
        if (_config.SkipToEnhanceIfTestsExist && _solutionTools.HasTestsAlready(_config.FileToTest, out string existingTestPath))
        {
            _output($"Skipping generation - found existing tests at '{existingTestPath}'");
            return await EnhanceExistingTestsAsync(existingTestPath);
        }

        var genResult = await GenerateTestsAsync(_config.FileToTest);
        if (genResult == null)
        {
            return false;
        }

        var testFilePath = await _solutionTools.SaveTestFile(_config.FileToTest, genResult.AIResponse.TestFileContent);
        var testFileProject = _solutionTools.FindProjectFile(testFilePath);

        if (!await EnsureTestsPassAsync(genResult, testFilePath, testFileProject, _config.FileToTest))
        {
            return false;
        }

        if (_config.EnhancementPasses > 0 && !await EnhanceTestsAsync(genResult.Analysis, _config.FileToTest, testFilePath))
        {
            _output("Enhancements failed");
            return false;
        }

        _output("Done");
        return true;
    }

    private async Task<UnitTestGenerationResult> GenerateTestsAsync(string fileToTest)
    {
        try
        {
            _output("Generating tests...");
            var genResult = await _generator.Generate(fileToTest);
            _output("Tests generated successfully");
            return genResult;
        }
        catch (Exception ex)
        {
            _output($"Failed to generate tests: {ex.Message}");
            return null;
        }
    }

    private async Task<bool> EnhanceExistingTestsAsync(string existingTestPath)
    {
        var analysisResult = await _generator.AnalyzeOnly(_config.FileToTest);
        return await EnhanceTestsAsync(analysisResult.Analysis, _config.FileToTest, existingTestPath);
    }

    private async Task<bool> EnsureTestsPassAsync(UnitTestGenerationResult genResult, string testFilePath, string testFileProject, string uutPath)
    {
        var runResult = await RunTestsAsync(testFileProject, testFilePath);
        if (runResult?.Success != true)
        {
            return await TryFixTestsAsync(genResult, testFilePath, testFileProject, uutPath, runResult);
        }
        return true;
    }

    private async Task<TestRunResult> RunTestsAsync(string testFileProject, string testFilePath)
    {
        try
        {
            _output("Running tests...");
            return await _runner.RunTestsAsync(testFileProject, Path.GetFileNameWithoutExtension(testFilePath));
        }
        catch (Exception ex)
        {
            _output($"Failed to run tests: {ex.Message}");
            return null;
        }
    }

    private async Task<bool> EnhanceTestsAsync(AnalysisResult analysis, string uutPath, string testFilePath)
    {
        for (int i = 0; i < _config.EnhancementPasses; i++)
        {
            _output($"Enhancing tests (pass {i + 1}/{_config.EnhancementPasses})");
            if (await EnhanceAndRunTestsAsync(analysis, uutPath, testFilePath))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<bool> EnhanceAndRunTestsAsync(AnalysisResult analysis, string uutPath, string testFilePath)
    {
        UnitTestGenerationResult result;
        try
        {
            result = await _enhancer.Enhance(analysis, uutPath, testFilePath);
            await _solutionTools.WriteSourceFile(testFilePath, result?.AIResponse?.TestFileContent);
        }
        catch (Exception ex)
        {
            _output($"Failed to enhance: {ex.Message}");
            return false;
        }

        var testFileProject = _solutionTools.FindProjectFile(testFilePath);
        var runResult = await RunTestsAsync(testFileProject, testFilePath);
        if (runResult?.Success == true)
        {
            _output("Tests enhanced");
            return true;
        }

        return runResult?.Success == true || await TryFixTestsAsync(result, testFilePath, testFileProject, uutPath, runResult);
    }

    private async Task<bool> TryFixTestsAsync(UnitTestGenerationResult genResult, string testFilePath, string testFileProject, string uutPath, TestRunResult runResult)
    {
        UnitTestGenerationResult lastResult = genResult;

        for (int i = 0; i < _config.MaxFixAttempts; i++)
        {
            _output($"Failed... Attempting to fix ({i + 1}/{_config.MaxFixAttempts})");
            try
            {
                lastResult = await _fixer.Fix(new FixContext() { Attempt = i, LastGenerationResult = lastResult, LastTestRunResults = runResult }, testFilePath, uutPath);
                await _solutionTools.WriteSourceFile(testFilePath, lastResult?.AIResponse?.TestFileContent);
                _output($"Fixed tests written to {testFilePath}");
            }
            catch (Exception ex)
            {
                _output($"Failed to fix tests: {ex.Message}");
                continue;
            }

            runResult = await RunTestsAsync(testFileProject, testFilePath);
            if (runResult?.Success == true)
            {
                _output("Success!");
                return true;
            }
            else
            {
                _output($"Test run failed: Errors: {runResult?.Errors?.Count} BuildIssues: {runResult?.BuildErrors?.Count} Failures: {runResult?.FailedTests?.Count} Passed: {runResult?.PassedTests?.Count}");
            }
        }

        _output("Failed to fix tests after max attempts.");
        return false;
    }
}
