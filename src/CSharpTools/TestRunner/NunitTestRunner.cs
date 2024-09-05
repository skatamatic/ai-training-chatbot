using System.Diagnostics;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Shared;

namespace CSharpTools.TestRunner;

public partial class NUnitTestRunner : IUnitTestRunner, IOutputter
{
    private Workspace _workspace;

    public bool IsPreparing => false;

    public TestRunnerAction CurrentAction { get; private set; }

    public event EventHandler<string> OnOutput;

    public NUnitTestRunner()
    {
    }

    public void Dispose()
    {
        _workspace?.Dispose();
    }

    public async Task<TestRunResult> RunTestsAsync(string projectPath, string testFilter = null)
    {
        if (string.IsNullOrEmpty(projectPath))
        {
            throw new ArgumentException("Project path cannot be null or empty.", nameof(projectPath));
        }

        // Build the project
        var buildErrors = await BuildProjectAsync(projectPath);
        if (buildErrors.Any())
        {
            return new TestRunResult
            {
                BuildErrors = buildErrors
            };
        }

        // List and count the tests to be run
        var (testCount, errors) = await GetTestCountAsync(projectPath, testFilter);
        if (testCount < 0)
        {
            return new TestRunResult
            {
                Errors = errors
            };
        }

        OnOutput?.Invoke(this, $"Running {testCount} tests");

        // Run dotnet test
        return await RunDotNetTestAsync(projectPath, testFilter);
    }

    public async Task<TestRunResult> RunFailuresAsync(string projectPath, TestRunResult previousRunResult)
    {
        if (string.IsNullOrEmpty(projectPath))
        {
            throw new ArgumentException("Project path cannot be null or empty.", nameof(projectPath));
        }

        // Rebuild the project
        var buildErrors = await BuildProjectAsync(projectPath);
        if (buildErrors.Any())
        {
            return new TestRunResult
            {
                BuildErrors = buildErrors
            };
        }

        // Run failed tests
        var failedTests = previousRunResult.FailedTests.Select(test => test.FullName).ToList();
        var testFilter = string.Join("|", failedTests);

        if (failedTests.Count > 0)
        {
            OnOutput?.Invoke(this, $"Number of failed tests to rerun: {failedTests.Count}");
            return await RunDotNetTestAsync(projectPath, testFilter);
        }
        else
        {
            OnOutput?.Invoke(this, "No failed tests to rerun.");
            return new TestRunResult();
        }
    }

    private async Task<(int count, List<string> errors)> GetTestCountAsync(string projectPath, string testFilter)
    {
        var arguments = $"test \"{projectPath}\" --list-tests";
        if (!string.IsNullOrEmpty(testFilter))
        {
            arguments += $" --filter \"FullyQualifiedName~{testFilter}\"";
        }
        
        var processResult = await RunProcessAsync("dotnet", arguments);

        if (processResult.ExitCode != 0)
        {
            OnOutput?.Invoke(this, "Failed to list tests.");
            processResult.Output.ForEach(x => OnOutput?.Invoke(null, x));
            return (-1, processResult.Output.Where(x=>x.ToLower().Contains("error")).ToList());
        }

        var testList = processResult.Output
            .Where(line => line.StartsWith("    "))
            .ToList();

        return (testList.Count, new List<string>());
    }

    async Task<Compilation> GetCompilation(string path)
    {
        var progress = new Progress(x => OnOutput?.Invoke(null, x));
        var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(path, progress, progress);
        
        return await project.GetCompilationAsync();
    }

    private async Task<List<string>> BuildProjectAsync(string projectPath)
    {
        CurrentAction = TestRunnerAction.Compiling;

        var errors = new List<string>();

        var compilation = await GetCompilation(projectPath);

        if (compilation == null)
        {
            errors.Add("Compilation failed.");
            return errors;
        }

        var diagnostics = compilation.GetDiagnostics();
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic.ToString());
            }
        }

        var outputPath = GetDllPath(projectPath);
        var emitResult = compilation.Emit(outputPath);

        if (!emitResult.Success)
        {
            foreach (var diagnostic in emitResult.Diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    errors.Add(diagnostic.ToString());
                }
            }
        }

        return errors;
    }

    private string GetDllPath(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath);
        var outputPath = Path.Combine(projectDirectory, "bin", "Debug", "net48");
        Directory.CreateDirectory(outputPath);
        var filename = Path.GetFileNameWithoutExtension(projectPath) + ".dll";
        return Path.Combine(outputPath, filename);
    }

    private async Task<TestRunResult> RunDotNetTestAsync(string projectPath, string testFilter)
    {
        CurrentAction = TestRunnerAction.Running;

        var result = new TestRunResult();

        var trxFileName = Path.Combine(Path.GetTempPath(), "TestResults.trx");
        var arguments = $"test \"{projectPath}\" --logger \"trx;LogFileName={trxFileName}\"";
        if (!string.IsNullOrEmpty(testFilter))
        {
            arguments += $" --filter \"{testFilter}\"";
        }

        var processResult = await RunProcessAsync("dotnet", arguments);

        if (processResult.ExitCode != 0)
        {
            result.Errors.AddRange(processResult.Output);
        }

        if (File.Exists(trxFileName))
        {
            var testResultsXml = new XmlDocument();
            testResultsXml.Load(trxFileName);

            var failedTests = ParseTestCases(testResultsXml, "Failed");
            var passedTests = ParseTestCases(testResultsXml, "Passed");

            result.FailedTests = failedTests;
            result.PassedTests = passedTests;
        }
        else
        {
            result.Errors.Add("Test result file not found.");
        }

        return result;
    }

    private async Task<ProcessResult> RunProcessAsync(string fileName, string arguments)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };

        var output = new List<string>();
        var error = new List<string>();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                output.Add(e.Data);
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                error.Add(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            Output = output.Concat(error).ToList()
        };
    }

    private List<TestCaseResult> ParseTestCases(XmlDocument testResultsXml, string resultType)
    {
        var results = new List<TestCaseResult>();

        var testCases = testResultsXml.GetElementsByTagName("UnitTestResult")
            .Cast<XmlNode>()
            .Where(node => node.Attributes["outcome"]?.Value == resultType);

        foreach (XmlNode testCase in testCases)
        {
            var testCaseResult = new TestCaseResult
            {
                FullName = testCase.Attributes["testName"]?.Value,
                Result = resultType,
                Message = resultType == "Passed" ? string.Empty : testCase["Output"]["ErrorInfo"]["Message"].InnerText,
                StackTrace = resultType == "Passed" ? string.Empty : testCase["Output"]["ErrorInfo"]["StackTrace"].InnerText ?? string.Empty
            };
            results.Add(testCaseResult);
        }

        return results;
    }

    public async Task<string> Prepare(string projectPath)
    {
        CurrentAction = TestRunnerAction.Validating;

        return string.Empty;
    }
}
