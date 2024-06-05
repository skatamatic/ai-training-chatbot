using ServiceInterface;

namespace OpenAIAPI_Rystem.Functions
{
    public class TestRunnerFunction : FunctionBase
    {
        public override string Name => "run_unit_tests";

        public override string Description => "Runs tests at the provided project path (excluding any project file, just the root of the path) with some optional, mutually exclusive filters.  They can include a collection of individual test names (IndividualTestFilter) or a string that all the test names must contain (TestSuiteFiler).  If none are specified, all tests are run.";

        public override Type Input => typeof(TestRunnerRequest);

        ITestRunnerService service;
        IFunctionInvocationEmitter emitter;

        public TestRunnerFunction(ITestRunnerService service, IFunctionInvocationEmitter invocationEmitter)
            : base(invocationEmitter)
        {
            emitter = invocationEmitter;
            this.service = service;
            this.service.OnOutput += Service_OnOutput;
        }

        private void Service_OnOutput(object sender, string e)
        {
            emitter.EmitProgress(Name, e);
        }

        protected override async Task<object> ExecuteFunctionAsync(object request)
        {
            var req = request as TestRunnerRequest;
            return await service.RunTests(req);
        }
    }
}
