using CSharpTools.TestRunner;
using Microsoft.Extensions.Hosting;
using Shared;
using UnitTestGenerator.Interface;

namespace UnitTestGenerator.Services;

public class FunctionOutputter : IOutputter
{
    readonly IFunctionInvocationObserver _observer;

    public FunctionOutputter(IFunctionInvocationObserver observer)
    {
        _observer = observer;
        _observer.OnFunctionInvocation += OnFunctionInvocation;
        _observer.OnFunctionResult += OnFunctionResult;
    }

    private void OnFunctionResult(object sender, string e)
    {
        OnOutput?.Invoke(this, "Result:" + e);
    }

    private void OnFunctionInvocation(object sender, string e)
    {
        OnOutput?.Invoke(this, e);
    }

    public event EventHandler<string> OnOutput;
}