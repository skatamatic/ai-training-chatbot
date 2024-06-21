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
        UnitTestGenerationResult genResult = null;
        string testFilePath;

        if (!_config.SkipToEnhanceIfTestsExist || !_solutionTools.HasTestsAlready(_config.FileToTest, out string existingTestPath))
        {

            try
            {
                _output("Generating tests...");
                genResult = await _generator.Generate(_config.FileToTest);
                testFilePath = await _solutionTools.SaveTestFile(_config.FileToTest, genResult.AIResponse.TestFileContent);
                _output($"Tests written to {testFilePath}");
            }
            catch (Exception ex)
            {
                _output($"Failed to generate tests: {ex.Message} AI Output: {genResult?.AIResponse?.TestFileContent}");
                return false;
            }

            var testFileProject = _solutionTools.FindProjectFile(testFilePath);
            TestRunResult runResult;
            
            try
            {
                _output("Running tests...");
                runResult = await _runner.RunTestsAsync(testFileProject, Path.GetFileNameWithoutExtension(testFilePath));
            }
            catch (Exception ex)
            {
                _output($"Failed to run tests: {ex.Message}");
                return false;
            }

            if (runResult?.Success != true)
            {
                bool testsFixed = await TryFixTestsAsync(genResult, testFilePath, testFileProject, _config.FileToTest, runResult);
                if (!testsFixed)
                {
                    return false;
                }
            }
        }
        else
        {
            _output($"Skipping generation - found existing tests at '{existingTestPath}'");
            genResult = await _generator.AnalyzeOnly(_config.FileToTest);
            testFilePath = existingTestPath;
        }

        if (_config.EnhancementPasses > 0)
        {
            bool enhanced = await TryEnhanceTestsAsync(genResult.Analysis, _config.FileToTest, testFilePath);
            if (!enhanced)
            {
                _output("Enhancements failed");
                return false;
            }
        }

        _output("Done");
        return true;
    }

    private async Task<bool> TryEnhanceTestsAsync(AnalysisResult analysis, string uutPath, string testFilePath)
    {
        for (int i = 0; i < _config.EnhancementPasses; i++)
        {
            _output($"Enhancing tests (pass {i + 1}/{_config.EnhancementPasses})");
            UnitTestGenerationResult result = null;

            try
            {
                result = await _enhancer.Enhance(analysis, uutPath, testFilePath);
                await _solutionTools.WriteSourceFile(testFilePath, result?.AIResponse?.TestFileContent);
            }
            catch (Exception ex)
            {
                _output($"Failed to enhance: {ex.Message}");
                continue;
            }

            string testFileProject = _solutionTools.FindProjectFile(testFilePath);
            TestRunResult runResult;

            try
            {
                runResult = await _runner.RunTestsAsync(testFileProject, Path.GetFileNameWithoutExtension(testFilePath));
            }
            catch (Exception ex)
            {
                _output($"Failed to run tests: {ex.Message}");
                continue;
            }
            

            if (runResult.Success)
            {
                _output("Tests enhanced");
                return true;
            }

            if (!runResult.Success)
            {
                bool testsFixed = await TryFixTestsAsync(result, testFilePath, testFileProject, uutPath, runResult);
                if (!testsFixed)
                {
                    return false;
                }
            }
        }

        return true;
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
            
            try
            {
                _output("Running fixed tests...");
                runResult = await _runner.RunTestsAsync(testFileProject, Path.GetFileNameWithoutExtension(testFilePath));
            }
            catch (Exception ex)
            {
                _output($"Failed to run tests: {ex.Message}");
                continue;
            }

            if (runResult.Success)
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
