using OpenAIAPI_Rystem.Services;
using Shared;

namespace OpenAIAPI_Rystem.Functions;

public class TestRunnerFunction : FunctionBase<Services.TestRunnerRequest, TestRunnerResponse>
{
    public override string Name => "run_unit_tests";

    public override string Description => "Runs tests at the provided project path (excluding any project file, just the root of the path) with some optional, mutually exclusive filters. They can include a collection of individual test names (IndividualTestFilter) or a string that all the test names must contain (TestSuiteFiler). If none are specified, all tests are run.";

    private readonly ITestRunnerService service;
    private readonly IFunctionInvocationEmitter emitter;

    public TestRunnerFunction(ITestRunnerService service, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        this.emitter = invocationEmitter;
        this.service = service;
        this.service.OnOutput += Service_OnOutput;
    }

    private void Service_OnOutput(object sender, string e)
    {
        emitter.EmitProgress(Name, e);
    }

    protected override async Task<TestRunnerResponse> ExecuteFunctionAsync(Services.TestRunnerRequest request)
    {
        try
        {
            return await service.RunTests(request);
        }
        catch (Exception ex)
        {
            return new TestRunnerResponse() { Error = ex.ToString() };
        }
    }
}