namespace Shared;

public interface IOpenAIAPI
{
    string SystemPrompt { get; set; }
    string ActiveSessionId { get; }
    Task<string> Prompt(string sessionId, string prompt);
}

public interface IFunctionInvocationObserver
{
    event EventHandler<string> OnFunctionInvocation;
    event EventHandler<string> OnFunctionProgressUpdate;
    event EventHandler<string> OnFunctionResult;
}

public interface IFunctionInvocationEmitter
{
    void EmitInvocation(string functionName, string request);
    void EmitResult(string functionName, string result);
    void EmitProgress(string functionName, string message);
}
