using CSharpTools.DefinitionAnalyzer;
using CSharpTools.SolutionTools;
using CSharpTools.TestRunner;
using Shared;
using Sorcerer.Interface;
using Sorcerer.Model;

namespace Sorcerer;

public class Sorcerer : IUnitTestSorcerer, IOutputter
{
    private readonly IUnitTestFixer _fixer;
    private readonly IUnitTestGenerator _generator;
    private readonly IUnitTestRunner _runner;
    private readonly IUnitTestEnhancer _enhancer;
    private readonly ISolutionTools _solutionTools;
    private readonly UnitTestSorcererConfig _config;

    public event EventHandler<string> OnOutput;

    public Sorcerer(UnitTestSorcererConfig config, IUnitTestFixer fixer, IUnitTestGenerator generator, IUnitTestRunner runner, IUnitTestEnhancer enhancer, ISolutionTools solutionTools)
    {
        _fixer = fixer;
        _generator = generator;
        _runner = runner;
        _solutionTools = solutionTools;
        _enhancer = enhancer;
        _config = config;
    }

    public async Task<bool> GenerateAsync()
    {
        OnOutput?.Invoke(this, $"Targetting {_config.FileToTest}");

        var prepareResult = await _runner.Prepare(_config.FileToTest);
        if (!string.IsNullOrEmpty(prepareResult))
        {
            OnOutput?.Invoke(this, prepareResult);
            return false;
        }

        if (_config.SkipToEnhanceIfTestsExist && _solutionTools.HasTestsAlready(_config.FileToTest, out string existingTestPath))
        {
            OnOutput?.Invoke(this, $"Skipping generation - found existing tests at '{existingTestPath}'");
            var enhanced = await EnhanceExistingTestsAsync(existingTestPath);
            if (enhanced)
            {
                OnOutput?.Invoke(this, "Success!");
                return true;
            }
            else
            {
                OnOutput?.Invoke(this, "Failed");
                return false;
            }
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

        if (_config.Enhancements.Length != 0 && !await EnhanceTestsAsync(genResult.Analysis, _config.FileToTest, testFilePath))
        {
            OnOutput?.Invoke(this, "Enhancements failed");
            return false;
        }

        OnOutput?.Invoke(this, "Success!");
        return true;
    }

    private async Task<UnitTestGenerationResult> GenerateTestsAsync(string fileToTest)
    {
        try
        {
            OnOutput?.Invoke(this, "Generating tests...");
            var genResult = await _generator.Generate(fileToTest);
            OnOutput?.Invoke(this, "Tests generated successfully");
            return genResult;
        }
        catch (Exception ex)
        {
            OnOutput?.Invoke(this, $"Failed to generate tests: {ex.Message}");
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
            var result = await _runner.RunTestsAsync(testFileProject, Path.GetFileNameWithoutExtension(testFilePath));
            return result;
        }
        catch (Exception ex)
        {
            OnOutput?.Invoke(this, $"Failed to run tests: {ex.Message}");
            return null;
        }
    }

    private async Task<bool> EnhanceTestsAsync(AnalysisResult analysis, string uutPath, string testFilePath)
    {
        UnitTestGenerationResult result = null;
        foreach (var enhancement in _config.Enhancements)
        {
            if (enhancement is EnhancementType.Verify)
            {
                if (result == null)
                    throw new InvalidOperationException("Cannot verify without first enhancing");

                var testFileProject = _solutionTools.FindProjectFile(testFilePath);
                var runResult = await RunTestsAsync(testFileProject, testFilePath);

                if (runResult?.Success == false && !await TryFixTestsAsync(result, testFilePath, testFileProject, uutPath, runResult))
                {
                    OnOutput?.Invoke(this, "Failed to verify");
                    return false;
                }
            }
            else
            {
                bool enhanced = false;
                for (int i = 0; i < _config.MaxFixAttempts; i++)
                {
                    (enhanced, result) = await EnhanceAsync(analysis, uutPath, testFilePath, enhancement);
                    if (enhanced)
                    {
                        break;
                    }
                }

                if (!enhanced)
                {
                    OnOutput?.Invoke(this, "Enhancement failed after too many attempts");
                    return false;
                }
            }
        }

        OnOutput?.Invoke(this, "Tests enhanced");

        return true;
    }

    private async Task<(bool success, UnitTestGenerationResult result)> EnhanceAsync(AnalysisResult analysis, string uutPath, string testFilePath, EnhancementType type)
    {
        UnitTestGenerationResult result;
        try
        {
            result = await _enhancer.Enhance(analysis, uutPath, testFilePath, type);
            await _solutionTools.WriteSourceFile(testFilePath, result?.AIResponse?.TestFileContent);
            return (true, result);
        }
        catch
        {
            return (false, null);
        }
    }

    private async Task<bool> TryFixTestsAsync(UnitTestGenerationResult genResult, string testFilePath, string testFileProject, string uutPath, TestRunResult runResult)
    {
        UnitTestGenerationResult lastResult = genResult;

        for (int i = 0; i < _config.MaxFixAttempts; i++)
        {
            OnOutput?.Invoke(this, $"Failed... Attempting to fix ({i + 1}/{_config.MaxFixAttempts})");
            try
            {
                string rootPath = _solutionTools.FindSolutionRoot(testFilePath);
                lastResult = await _fixer.Fix(new FixContext() { Attempt = i, LastGenerationResult = lastResult, LastTestRunResults = runResult, ProjectRootPath = rootPath }, testFilePath, uutPath);
                await _solutionTools.WriteSourceFile(testFilePath, lastResult?.AIResponse?.TestFileContent);
            }
            catch (Exception ex)
            {
                OnOutput?.Invoke(this, $"Failed to fix tests: {ex.Message}");
                continue;
            }

            runResult = await RunTestsAsync(testFileProject, testFilePath) ?? runResult;
            if (runResult?.Success == true)
            {
                return true;
            }
            else
            {
                OnOutput?.Invoke(this, $"Test run failed: Errors: {runResult?.Errors?.Count} BuildIssues: {runResult?.BuildErrors?.Count} Failures: {runResult?.FailedTests?.Count} Passed: {runResult?.PassedTests?.Count}");
            }
        }

        OnOutput?.Invoke(this, "Failed to fix tests after max attempts.");
        return false;
    }
}
