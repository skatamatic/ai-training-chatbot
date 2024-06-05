namespace Shared;

public class FunctionInvocationObserver : IFunctionInvocationObserver, IFunctionInvocationEmitter
{
    public event EventHandler<string> OnFunctionInvocation;
    public event EventHandler<string> OnFunctionResult;
    public event EventHandler<string> OnFunctionProgressUpdate;

    public void EmitInvocation(string functionName, string request)
    {
        OnFunctionInvocation?.Invoke(this, $"{functionName}()\nArgs: {request}");
    }

    public void EmitProgress(string functionName, string message)
    {
        OnFunctionProgressUpdate?.Invoke(this, $"{functionName}()\n{message}");
    }

    public void EmitResult(string functionName, string result)
    {
        OnFunctionResult?.Invoke(this, $"{functionName}()\n{result}");
    }
}