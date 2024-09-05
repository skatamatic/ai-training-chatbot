namespace CSharpTools.TestRunner;

public class TestRunResult
{
    public List<string> BuildErrors { get; set; } = new List<string>();
    public List<string> Errors { get; set; } = new List<string>();
    public List<TestCaseResult> PassedTests { get; set; } = new List<TestCaseResult>();
    public List<TestCaseResult> FailedTests { get; set; } = new List<TestCaseResult>();
    public bool Success => PassedTests.Count > 0 && FailedTests.Count == 0 && BuildErrors.Count == 0;
}
