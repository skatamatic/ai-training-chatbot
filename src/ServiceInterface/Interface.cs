namespace ServiceInterface;

public interface IOpenAIAPI
{
    string ActiveSessionId { get; }
    Task<string> Prompt(string sessionId, string prompt);
}

public interface IFunctionInvocationObserver
{
    event EventHandler<string> OnFunctionInvocation;
    event EventHandler<string> OnFunctionResult;
}

public interface IFunctionInvocationEmitter
{
    void EmitInvocation(string functionName, string request);
    void EmitResult(string functionName, string result);
}

public interface ISystemMessageProvider
{
    string SystemMessage { get; }
}