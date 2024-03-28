using Newtonsoft.Json;
using Rystem.OpenAi.Chat;
using ServiceInterface;

namespace OpenAIAPI_Rystem.Functions;

public abstract class FunctionBase : IOpenAiChatFunction
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract Type Input { get; }

    protected readonly IFunctionInvocationEmitter _invocationEmitter;

    protected FunctionBase(IFunctionInvocationEmitter invocationEmitter)
    {
        _invocationEmitter = invocationEmitter ?? throw new ArgumentNullException(nameof(invocationEmitter));
    }

    public async Task<object> WrapAsync(string message)
    {
        var requestType = Input;
        var request = JsonConvert.DeserializeObject(message, requestType);

        _invocationEmitter.EmitInvocation(Name, message);

        var result = await ExecuteFunctionAsync(request);

        _invocationEmitter.EmitResult(Name, JsonConvert.SerializeObject(result, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));

        return result;
    }

    protected abstract Task<object> ExecuteFunctionAsync(object request);
}
