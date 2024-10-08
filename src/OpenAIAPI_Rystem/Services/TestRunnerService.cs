﻿using CSharpTools.SolutionTools;
using CSharpTools.TestRunner.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OpenAIAPI_Rystem.Services;

public interface ITestRunnerService
{
    event EventHandler<string> OnOutput;
    Task<TestRunnerResponse> RunTests(TestRunnerRequest request);
}

public class UnityTestRunnerService : ITestRunnerService
{
    readonly UnityWebClientTestRunner _runner;

    public event EventHandler<string> OnOutput;

    public UnityTestRunnerService()
    {
        _runner = new UnityWebClientTestRunner(new UnitySolutionTools());
        _runner.OnOutput += (_, x) => Emit(x);
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

            var result = await _runner.RunTestsAsync(request.ProjectPath, filter);

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