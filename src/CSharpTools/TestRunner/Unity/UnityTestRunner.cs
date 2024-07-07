using Shared;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CSharpTools.TestRunner.Unity;

public partial class UnityTestRunner : IUnitTestRunner, IOutputter
{
    public TestRunnerAction CurrentAction { get; private set; }

    public event EventHandler<string> OnOutput;

    public UnityTestRunner()
    {
    }

    public async Task<TestRunResult> RunTestsAsync(string projectPath, string testFilter = null)
    {
        var unityTestResults = await RunTests(projectPath, testFilter);
        return ConvertToTestRunResult(unityTestResults);
    }

    public async Task<TestRunResult> RunFailuresAsync(string projectPath, TestRunResult previousRunResult)
    {
        var failedTests = previousRunResult.FailedTests.Select(test => test.FullName).ToArray();
        var filter = BuildMultiTestFilter(failedTests);
        var unityTestResults = await RunTests(projectPath, filter);
        return ConvertToTestRunResult(unityTestResults);
    }

    private TestRunResult ConvertToTestRunResult(UnityTestResults unityTestResults)
    {
        var testRunResult = new TestRunResult();

        if (!string.IsNullOrEmpty(unityTestResults.Error))
        {
            testRunResult.Errors.Add(unityTestResults.Error);
        }

        foreach (var unityTest in unityTestResults.TestResults)
        {
            var testCaseResult = new TestCaseResult
            {
                FullName = unityTest.TestName,
                Result = unityTest.Success ? "Passed" : "Failed",
                Message = unityTest?.Failure,
                StackTrace = unityTest?.Log
            };

            if (unityTest.Success)
            {
                testRunResult.PassedTests.Add(testCaseResult);
            }
            else
            {
                testRunResult.FailedTests.Add(testCaseResult);
            }
        }

        return testRunResult;
    }

    public static string BuildMultiTestFilter(string[] tests)
    {
        return $"\"{string.Join(";", tests)}\"";
    }

    public static string BuildSuiteFilter(string testFixture)
    {
        return $".*{testFixture}.*";
    }

    async Task<UnityTestResults> RunTests(string projectPath, string filter = null)
    {
        var result = new UnityTestResults();

        string unityPath = GetUnityPath(projectPath);
        if (unityPath == null)
        {
            OnOutput?.Invoke(this, "Unable to find Unity installation.");
            result.Error = "Unity installation not found.";
            return result;
        }

        string logFilePath = Path.Combine(projectPath, "Logs/unity_test_log.txt");

        // Ensure the log directory exists
        Directory.CreateDirectory(Path.Combine(projectPath, "Logs"));

        string tempUnityLogPath = Path.GetTempFileName();
        string tempRedirectedLogPath = Path.GetTempFileName();
        string testResultsPath = Path.GetTempFileName();

        string runTestsCommand = $"-runTests -batchmode -logFile \"{tempUnityLogPath}\" -testResults {testResultsPath}";

        // If there are specific tests to run, append the testFilter argument
        if (!string.IsNullOrEmpty(filter))
        {
            runTestsCommand += $" -testFilter {filter}";
        }

        CurrentAction = TestRunnerAction.Running;
        OnOutput?.Invoke(this, "Running Unity tests (this can take a while)...");
        bool success = await RunUnityCommandAsync(unityPath, runTestsCommand, tempRedirectedLogPath);

        if (File.Exists(testResultsPath))
        {
            try
            {
                var testResultContent = await File.ReadAllTextAsync(testResultsPath);
                ParseTestResults(testResultContent, result);
            }
            catch (Exception ex)
            {
                result.Error = (result.Error ?? "") + ex.Message;
            }
        }
        else
        {
            //Only gather log data if we can't parse the logs (could be a compiler error!)
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var logContent = await File.ReadAllTextAsync(tempUnityLogPath);
                    var stdOut = await File.ReadAllBytesAsync(tempRedirectedLogPath);
                    result.Error = logContent + stdOut;
                }
                catch (Exception)
                {
                    //Try to get around unity holding locks too aggressively
                    await ForceKillUnity();
                    await Task.Delay(500);
                }
            }
        }

        return result;
    }

    private void ParseTestResults(string xmlContent, UnityTestResults results)
    {
        var document = XDocument.Parse(xmlContent);
        var testCases = document.Descendants("test-case");

        foreach (var testCase in testCases)
        {
            var testName = testCase.Attribute("fullname")?.Value;
            var result = testCase.Attribute("result")?.Value;
            var className = testCase.Attribute("classname")?.Value;
            var info = testCase.Attribute("label")?.Value;
            var log = testCase.Element("output")?.Value;
            var failure = testCase.Element("failure")?.Value;

            results.TestResults.Add(new UnityTestRun
            {
                TestName = testName,
                Success = result != "Failed",
                Info = info ?? string.Empty,
                Log = log ?? string.Empty,
                Failure = failure ?? string.Empty,
                ClassName = className ?? string.Empty
            });
        }

        results.AllPassed = results.TestResults.All(x => x.Success);
    }

    string GetUnityPath(string projectPath)
    {
        string versionFilePath = Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(versionFilePath))
        {
            OnOutput?.Invoke(this, "ProjectVersion.txt not found.");
            return null;
        }

        string versionText = File.ReadAllText(versionFilePath);
        string unityVersion = ParseUnityVersion(versionText);

        if (unityVersion == null)
        {
            OnOutput?.Invoke(this, "Failed to parse Unity version.");
            return null;
        }

        string unityHubPath = GetUnityHubEditorPath(unityVersion);
        if (!string.IsNullOrEmpty(unityHubPath))
        {
            return unityHubPath;
        }

        // Fallback to common Unity installation paths
        string[] commonPaths = new[]
        {
            $"/Program Files/Unity/Hub/Editor/{unityVersion}/Editor/Unity.exe",
            $"/Program Files (x86)/Unity/Hub/Editor/{unityVersion}/Editor/Unity.exe"
        };

        return commonPaths.FirstOrDefault(File.Exists);
    }

    static string ParseUnityVersion(string versionText)
    {
        var match = Regex.Match(versionText, @"m_EditorVersion:\s*(\d+\.\d+\.\d+[a-z]\d*)");
        return match.Success ? match.Groups[1].Value : null;
    }

    static string GetUnityHubEditorPath(string unityVersion)
    {
        string unityExecutable = null;

        // Check environment variable
        string unityHubPath = Environment.GetEnvironmentVariable("UNITY_HUB_PATH");
        if (!string.IsNullOrEmpty(unityHubPath))
        {
            unityExecutable = Path.Combine(unityHubPath, unityVersion, "Editor", "Unity.exe");
            if (File.Exists(unityExecutable))
            {
                return unityExecutable;
            }
        }

        // Check LocalApplicationData path
        unityHubPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnityHub", "Editor", unityVersion);
        unityExecutable = Path.Combine(unityHubPath, "Editor", "Unity.exe");
        if (File.Exists(unityExecutable))
        {
            return unityExecutable;
        }

        // Check Program Files (x86)
        unityHubPath = Path.Combine("C:", "Program Files (x86)", "Unity", "Hub", "Editor", unityVersion);
        unityExecutable = Path.Combine(unityHubPath, "Editor", "Unity.exe");
        if (File.Exists(unityExecutable))
        {
            return unityExecutable;
        }

        // Check Program Files
        unityHubPath = Path.Combine("C:", "Program Files", "Unity", "Hub", "Editor", unityVersion);
        unityExecutable = Path.Combine(unityHubPath, "Editor", "Unity.exe");
        if (File.Exists(unityExecutable))
        {
            return unityExecutable;
        }

        // Check default installation path for Unity Hub (if UNITY_HUB_PATH is not set)
        unityHubPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Programs", "Unity", "Hub", "Editor", unityVersion);
        unityExecutable = Path.Combine(unityHubPath, "Editor", "Unity.exe");
        if (File.Exists(unityExecutable))
        {
            return unityExecutable;
        }

        return null;
    }

    async Task<bool> RunUnityCommandAsync(string unityPath, string command, string redirectedLogFilePath)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = unityPath,
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    using (var writer = new StreamWriter(redirectedLogFilePath, false))
                    {
                        var outputTask = ReadStreamAsync(process.StandardOutput, writer);
                        var errorTask = ReadStreamAsync(process.StandardError, writer);

                        await Task.WhenAll(outputTask, errorTask);
                    }
                }
                catch (Exception)
                {
                    //Try to get around unity holding locks too aggressively
                    await Task.Delay(500);
                }
            }

            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
        finally
        {
            // Forcefully close the Unity Editor using taskkill
            await ForceKillUnity();
        }
    }

    private async Task ForceKillUnity()
    {
        var taskkillProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/f /im Unity.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        taskkillProcess.Start();
        await taskkillProcess.WaitForExitAsync();
    }

    async Task ReadStreamAsync(StreamReader reader, StreamWriter writer)
    {
        string line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            await writer.WriteLineAsync(line);
        }
    }

    public async Task<string> Prepare(string projectPath)
    {
        return string.Empty;
    }

    public void Dispose()
    {
        _ = ForceKillUnity();
    }
}
