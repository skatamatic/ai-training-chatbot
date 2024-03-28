namespace ServiceInterface;

public class FunctionInvocationObserver : IFunctionInvocationObserver, IFunctionInvocationEmitter
{
    public event EventHandler<string> OnFunctionInvocation;
    public event EventHandler<string> OnFunctionResult;

    public void EmitInvocation(string functionName, string request)
    {
        OnFunctionInvocation?.Invoke(this, $"{functionName}()\nArgs: {request}");
    }

    public void EmitResult(string functionName, string result)
    {
        OnFunctionResult?.Invoke(this, $"{functionName}()\n{result}");
    }
}