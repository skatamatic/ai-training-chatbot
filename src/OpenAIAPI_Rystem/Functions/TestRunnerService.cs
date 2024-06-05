using CSharpTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Rystem.OpenAi;

namespace OpenAIAPI_Rystem.Functions;

public interface ITestRunnerService
{
    event EventHandler<string> OnOutput;
    Task<TestRunnerResponse> RunTests(TestRunnerRequest request);
}

public class UnityTestRunnerService : ITestRunnerService
{
    readonly UnityTestRunner _runner;

    public event EventHandler<string> OnOutput;

    public UnityTestRunnerService()
    {
        _runner = new UnityTestRunner(Emit);
    }

    private void Emit(string output)
    {
        OnOutput?.Invoke(this, output);
    }

    public async Task<TestRunnerResponse> RunTests(TestRunnerRequest request)
    {
        try
        {
            string filter = null;
            
            if (request.IndividualTestFilter != null && request.IndividualTestFilter.Length > 0)
            {
                filter = UnityTestRunner.BuildMultiTestFilter(request.IndividualTestFilter);
            }
            else if (!string.IsNullOrWhiteSpace(request.TestSuiteFilter))
            {
                filter = UnityTestRunner.BuildSuiteFilter(request.TestSuiteFilter);
            }

            var result = await _runner.RunTests(request.ProjectPath, filter);

            if (result.AllPassed)
            {
                result.TestResults.Clear();
            }
            else
            {
                result.TestResults = result.TestResults.Where(r => !r.Success).ToList();
            }

            var settings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new StringEnumConverter() }
            };

            return new TestRunnerResponse() { Content = JsonConvert.SerializeObject(result, settings) };
        }
        catch (Exception ex)
        {
            return new TestRunnerResponse() { Error = ex.Message };
        }
    }
}

public class TestRunnerRequest
{
    public string ProjectPath { get; set; }
    public string[] IndividualTestFilter { get; set; }
    public string TestSuiteFilter { get; set; }
}

public class TestRunnerResponse
{
    public string Error { get; set; }
    public string Content { get; set; }
}