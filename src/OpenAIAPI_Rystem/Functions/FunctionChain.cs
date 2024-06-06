using Newtonsoft.Json;
using Shared;

namespace OpenAIAPI_Rystem.Functions;

public interface IPromptingFunction
{
    void InstallApi(IOpenAIAPI api);
}

public class FunctionChainFunction : FunctionBase, IPromptingFunction
{
    public const string FUNCTION_NAME = "function_chain";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Allows you to tell me a prompt I can use to achieve a goal.  You can use other functions to find the data you might need in an iterative manner, perhaps chaining multiple of these commands together etc.  Use the new session bool if it would save tokens to exclude the chain from the main session context (ie to summarize a file, or to extract function names from files).  Remember the new session will not have any context, so give very detailed instructions including full file paths etc.";
    public override Type Input => typeof(FunctionChainRequest);

    private IOpenAIAPI _api;

    public FunctionChainFunction(IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        if (_api == null)
        {
            throw new InvalidOperationException("API is not set");
        }

        if (request is FunctionChainRequest chainRequest)
        {
            return await _api.Prompt(chainRequest.NewSession ? Guid.NewGuid().ToString() : _api.ActiveSessionId, chainRequest.Prompt);
        }

        throw new ArgumentException("Invalid request type", nameof(request));
    }

    public void InstallApi(IOpenAIAPI api)
    {
        _api = api;
    }
}

public class FunctionChainRequest
{
    [JsonProperty("prompt")]
    public string Prompt { get; set; }

    [JsonProperty("newSession")]
    public bool NewSession { get; set; }
}

public class FunctionChainResponse
{
    [JsonProperty("result")]
    public string Result { get; set; }
}