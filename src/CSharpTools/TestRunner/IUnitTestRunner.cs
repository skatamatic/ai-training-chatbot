namespace CSharpTools.TestRunner;

public interface IUnitTestRunner
{
    Task<TestRunResult> RunTestsAsync(string projectPath, string testFilter = null);
    Task<TestRunResult> RunFailuresAsync(string projectPath, TestRunResult previousRunResult);
}
