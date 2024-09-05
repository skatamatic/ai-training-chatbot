using Newtonsoft.Json;
using Rystem.OpenAi.Chat;
using Shared;

namespace OpenAIAPI_Rystem.Functions;

public abstract class FunctionBase<TInput, TOutput> : IOpenAiChatFunction 
    where TInput: class 
    where TOutput: class
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public Type Input => typeof(TInput);

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

        if (request is not TInput input)
        {
            throw new InvalidCastException($"Request is not of type {typeof(TInput)}");
        }

        var result = await ExecuteFunctionAsync(input);

        _invocationEmitter.EmitResult(Name, JsonConvert.SerializeObject(result, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));

        return result;
    }

    protected abstract Task<TOutput> ExecuteFunctionAsync(TInput request);
}
