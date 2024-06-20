namespace CSharpTools.TestRunner;

public class UnityTestResults
{
    public List<UnityTestRun> TestResults { get; set; } = new();
    public bool AllPassed { get; set; }
    public int TotalTests => TestResults.Count;
    public int TotalFailed => TestResults.Count(x => !x.Success);
    public string Error { get; set; }
}