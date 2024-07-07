namespace CSharpTools.TestRunner;

public enum TestRunnerAction
{
    Validating,
    Compiling,
    Running
}

public interface IUnitTestRunner : IDisposable
{
    TestRunnerAction CurrentAction { get; }
    Task<string> Prepare(string projectPath);
    Task<TestRunResult> RunTestsAsync(string projectPath, string testFilter = null);
    Task<TestRunResult> RunFailuresAsync(string projectPath, TestRunResult previousRunResult);
}
