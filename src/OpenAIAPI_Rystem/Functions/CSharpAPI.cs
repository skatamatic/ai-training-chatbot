using ServiceInterface;

namespace OpenAIAPI_Rystem.Functions
{
    public class CSharpDefinitionsFunction : FunctionBase
    {
        public override string Name => "get_code_definitions_for_files";

        public override string Description => "Given an array of absolute file paths of csharp files and a max depth, analyze each one and return an analysis result that includes all relevant definitions.  This is recursive up to a max depth.  Also include total lines of code of all the definitions (context) and overall (context + file that was analyzed).  Finally, provide an indication on the anticipated quality of AI generated tests based on the amount of code that is required to get the full context";

        public override Type Input => typeof(CSharpDefinitionsRequest);

        ICSharpService service;
        IFunctionInvocationEmitter emitter;

        public CSharpDefinitionsFunction(ICSharpService service, IFunctionInvocationEmitter invocationEmitter)
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
            var req = request as CSharpDefinitionsRequest;
            return await service.GetDefinitions(req);
        }
    }
}
