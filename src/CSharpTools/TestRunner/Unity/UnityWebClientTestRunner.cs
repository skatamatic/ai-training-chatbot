using CSharpTools.SolutionTools;
using Shared;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CSharpTools.TestRunner.Unity;

public partial class UnityWebClientTestRunner : IUnitTestRunner, IOutputter
{
    public event EventHandler<string> OnOutput;
    private const string ServerScriptPath = "Assets/Scripts/Editor/SorcererWebServer.cs";
    private const string ServerScriptResource = "TestRunner\\Unity\\Resources\\UnityTestServer.txt";
    private const string StartServerCommand = "SorcererWebServer.StartServer";
    private const string RecompileCommand = "/recompile";
    private const string RunTestsCommand = "/runTests";
    private const string ServerUrl = "http://localhost:8080";

    const bool batchMode = true;
    readonly ISolutionTools solutionTools;

    TaskCompletionSource<bool> unityClosedTcs = new TaskCompletionSource<bool>();

    public TestRunnerAction CurrentAction { get; private set; }

    public UnityWebClientTestRunner(ISolutionTools solutionTools)
    {
        this.solutionTools = solutionTools;
    }

    public async Task<TestRunResult> RunTestsAsync(string projectPath, string testFilter = null)
    {
        var unityTestResults = await SetupAndRunTests(projectPath, testFilter);
        var result = ConvertToTestRunResult(unityTestResults);
        if (result?.Success == true)
        {
            OnOutput?.Invoke(this, $"All {result.PassedTests.Count} tests passed");
        }
        return result;
    }

    public async Task<TestRunResult> RunFailuresAsync(string projectPath, TestRunResult previousRunResult)
    {
        var failedTests = previousRunResult.FailedTests.Select(test => test.FullName).ToArray();
        var filter = BuildMultiTestFilter(failedTests);
        var unityTestResults = await SetupAndRunTests(projectPath, filter);
        var result = ConvertToTestRunResult(unityTestResults);
        if (result?.Success == true)
        {
            OnOutput?.Invoke(this, $"All {result.PassedTests.Count} tests passed");
        }
        return result;
    }

    private TestRunResult ConvertToTestRunResult(UnityTestResults unityTestResults)
    {
        var testRunResult = new TestRunResult();

        if (!string.IsNullOrEmpty(unityTestResults.Error))
        {
            foreach (var error in unityTestResults.Error.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim('|')))
            {
                testRunResult.BuildErrors.Add(error);
            }
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

    public async Task<string> Prepare(string projectPath)
    {
        CurrentAction = TestRunnerAction.Validating;

        string unityPath = GetUnityPath(projectPath);
        if (unityPath == null)
        {
            OnOutput?.Invoke(this, "Unable to find Unity installation.");
            return "Unity installation not found.";
        }

        if (!await EnsureServerScriptExists(projectPath))
        {
            return "Failed to copy the server script.";
        }

        if (!await IsServerRunning())
        {
            OnOutput?.Invoke(this, $"Server not running - starting Unity{(batchMode ? " in batch mode" : "")} with web server started");

            if (!await StartUnityAndServer(unityPath, projectPath))
            {
                return "Failed to start server :(";
            }
        }

        return string.Empty;
    }

    private async Task<UnityTestResults> SetupAndRunTests(string projectPath, string filter = null)
    {
        var result = new UnityTestResults();

        var prepareResult = await Prepare(projectPath);

        if (!string.IsNullOrEmpty(prepareResult))
        {
            result.Error = prepareResult;
            return result;
        }

        var compileResult = await CompileScripts();

        if (!compileResult.success)
        {
            result.Error = $"{compileResult.errors}";
            return result;
        }

        return await RunTests(filter);
    }

    private async Task<bool> EnsureServerScriptExists(string projectPath)
    {
        try
        {
            if (!Directory.Exists(projectPath))
            {
                projectPath = Path.GetDirectoryName(projectPath);
            }

            var unityProjectRoot = FindUnityProjectRoot(projectPath);
            string serverScriptFullPath = Path.Combine(unityProjectRoot, ServerScriptPath);

            if (File.Exists(serverScriptFullPath))
            {
                string existingScriptContent = await File.ReadAllTextAsync(serverScriptFullPath);
                string newScriptContent = await File.ReadAllTextAsync(ServerScriptResource);

                if (existingScriptContent == newScriptContent)
                {
                    return true;
                }
            }

            File.Copy(ServerScriptResource, serverScriptFullPath, true);
            OnOutput?.Invoke(this, $"Server script copied to {serverScriptFullPath}");
            return true;
        }
        catch (Exception ex)
        {
            OnOutput?.Invoke(this, $"Error copying server script: {ex.Message}");
            return false;
        }
    }

    bool startedUnityManually = false;
    private async Task<bool> StartUnityAndServer(string unityPath, string projectPath)
    {
        if (!Directory.Exists(projectPath))
        {
            projectPath = Path.GetDirectoryName(projectPath);
        }
        string projectRootPath = FindUnityProjectRoot(projectPath);

        string startServerCommand = $"{(batchMode ? "-batchmode -nographics ‑ignorecompilererrors " : "")}-executeMethod {StartServerCommand} -projectPath \"{projectRootPath}\"";

        RunUnityCommand(unityPath, startServerCommand);
        if (!await WaitForServer(true))
        {
            return false;
        }

        OnOutput?.Invoke(this, "Connected to Unity");
        startedUnityManually = true;

        return true;
    }

    private async Task<bool> WaitForServer(bool log)
    {
        if (log)
            OnOutput?.Invoke(this, "Waiting for test server to come online");

        CurrentAction = TestRunnerAction.Validating;
        DateTime startTime = DateTime.Now;
        while (!await IsServerRunning())
        {
            if (unityClosedTcs.Task.IsCompleted)
            {
                OnOutput?.Invoke(this, "Unity closed unexpectedly");
                return false;
            }

            if (DateTime.Now - startTime > TimeSpan.FromSeconds(120))
            {
                OnOutput?.Invoke(this, "Server did not start within 30 seconds");
                return false;
            }
        }

        return true;
    }

    private async Task<(bool success, string errors)> CompileScripts()
    {
        CurrentAction = TestRunnerAction.Compiling;

        var recompileUri = new Uri(ServerUrl + RecompileCommand);

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(10);

        OnOutput?.Invoke(this, "Compiling scripts");

        var response = await client.GetAsync(recompileUri);

        if (!response.IsSuccessStatusCode)
        {
            OnOutput?.Invoke(this, "Failed to compile scripts");
            return (false, $"Got status code: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync();

        if (!content.Contains("No compilation errors"))
        {
            OnOutput?.Invoke(this, "Compilation failed (see results)");
            return (false, content);
        }

        await WaitForServer(false);

        return (true, "");
    }

    private async Task<UnityTestResults> RunTests(string filter = null)
    {
        CurrentAction = TestRunnerAction.Running;

        var result = new UnityTestResults();

        OnOutput?.Invoke(this, "Running tests");

        var runTestsUri = new Uri($"{ServerUrl}{RunTestsCommand}?filter={filter ?? string.Empty}");

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);

            var response = await client.GetAsync(runTestsUri);

            if (!response.IsSuccessStatusCode)
            {
                result.Error = $"Failed to run tests, got status code {response.StatusCode}";
                return result;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            ParseTestResultsFromServerResponse(content, result);

            return result;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            return result;
        }
    }

    private void ParseTestResultsFromServerResponse(string content, UnityTestResults results)
    {
        try
        {
            if (content.Trim() == "No tests found")
            {
                results.Error = "No tests found.  Did you get the filter right?";
                return;
            }

            // Assume content is plain text or XML based on implementation
            var lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim().Replace("\r", " "));
            UnityTestRun currentTestRun = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("Test: "))
                {
                    if (currentTestRun != null)
                    {
                        results.TestResults.Add(currentTestRun);
                    }

                    currentTestRun = new UnityTestRun
                    {
                        TestName = line.Substring(6),
                        Success = false
                    };
                }
                else if (line.StartsWith("Result: "))
                {
                    if (currentTestRun != null)
                    {
                        currentTestRun.Success = line.Substring(8).Equals("Passed", StringComparison.OrdinalIgnoreCase);
                    }
                }
                else if (line.StartsWith("Message: "))
                {
                    if (currentTestRun != null)
                    {
                        currentTestRun.Failure = line.Substring(9);
                    }
                }
                else if (line.StartsWith("Stack Trace: "))
                {
                    if (currentTestRun != null)
                    {
                        currentTestRun.Log = line.Substring(13);
                    }
                }
            }

            if (currentTestRun != null)
            {
                results.TestResults.Add(currentTestRun);
            }

            results.AllPassed = results.TestResults.All(x => x.Success);
        }
        catch (Exception ex)
        {
            results.Error = ex.Message;
        }
    }

    private async Task<bool> IsServerRunning()
    {
        try
        {
            using var client = new HttpClient();
            
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync(ServerUrl + "/status");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    string GetUnityPath(string filePath)
    {
        if (!Directory.Exists(filePath))
        {
            filePath = Path.GetDirectoryName(filePath);
        }
        string projectRootPath = FindUnityProjectRoot(filePath);
        if (projectRootPath == null)
        {
            OnOutput?.Invoke(this, "Unity project root not found.");
            return null;
        }

        string versionFilePath = Path.Combine(projectRootPath, "ProjectSettings", "ProjectVersion.txt");
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

    string FindUnityProjectRoot(string directory)
    {
        var currentDirectory = new DirectoryInfo(directory);
        while (currentDirectory != null && currentDirectory.Exists)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "ProjectSettings", "ProjectVersion.txt")))
            {
                return currentDirectory.FullName;
            }
            currentDirectory = currentDirectory.Parent;
        }
        return null;
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

    void RunUnityCommand(string unityPath, string command)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = unityPath,
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        unityClosedTcs = new TaskCompletionSource<bool>();

        process.EnableRaisingEvents = true;
        process.Exited += (sender, e) =>
        {
            unityClosedTcs.SetResult(process.ExitCode == 0);
            process.Dispose();
        };

        process.Start();
    }

    public void Dispose()
    {
        if (!startedUnityManually)
        {
            return;
        }

        // Stop the server
        using var client = new HttpClient();
        client.GetAsync(ServerUrl + "/shutdown");

        ForceKillUnity();
    }

    private void ForceKillUnity()
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
    }
}
