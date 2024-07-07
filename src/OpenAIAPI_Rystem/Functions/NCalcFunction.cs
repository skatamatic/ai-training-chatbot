using MySqlX.XDevAPI.Common;
using NCalc;
using Newtonsoft.Json;
using Shared;
using System.Text;

namespace OpenAIAPI_Rystem.Functions;

public class NCalcFunction : FunctionBase
{
    public const string FUNCTION_NAME = "evaluate_expression";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Takes a collection of expressions and uses NCalc (the .net library) to do math.  Use parameter PI to represent Math.PI (ie expression='PI * 2').  Use this anytime you need to do any math calculations (like when creating asserts for unit tests that require some math).  Returns a string that is typically a number (double).  Keep expressions as simple as possible - avoid ifs etc and stick to ncalc functions like Sin,Cos,Pow,etc and PI if needed.  DO NOT PREFIX WITH 'Math.', use NCalc syntax ONLY.  Don't use .ToString or ANY c# programming - stick to NCalc supported expression sytax only.";
    public override Type Input => typeof(NCalcRequest);

    public NCalcFunction(IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        if (request is not NCalcRequest ncalcRequest)
        {
            throw new ArgumentException("Invalid request type", nameof(request));
        }

        StringBuilder resultBuilder = new();
        foreach (var expression in ncalcRequest.Expressions)
        {
            try
            {
                var cleaned = CleanExpression(expression);
                var ncalc = new Expression(cleaned, EvaluateOptions.IgnoreCase);

                ncalc.EvaluateParameter += Expression_EvaluateParameter;
                var result = ncalc.Evaluate();

                resultBuilder.AppendLine($"{expression}={result}");
            }
            catch (Exception ex)
            {
                resultBuilder.AppendLine(($"{expression} had error {ex.Message}"));
            }
        }
        return new ResultResponse { Result = resultBuilder.ToString() };
    }

    private void Expression_EvaluateParameter(string name, ParameterArgs args)
    {
        if (name.ToLower() == "pi")
            args.Result = Math.PI;
    }

    private string CleanExpression(string expression)
    {
        var cleaned = expression.Replace("Math.", string.Empty)
            .Replace("double.MinValue", double.MinValue.ToString())
            .Replace("double.MaxValue", double.MaxValue.ToString())
            .Replace("double.PositiveInfinity", double.PositiveInfinity.ToString())
            .Replace("double.NegativeInfinity", double.NegativeInfinity.ToString())
            .Replace("PositiveInfinity", double.PositiveInfinity.ToString())
            .Replace("NegativeInfinity", double.NegativeInfinity.ToString())
            .Replace("double.Epsilon", double.Epsilon.ToString())
            .Replace("Epsilon", double.Epsilon.ToString())
            .Replace("double.NaN", double.NaN.ToString())
            .Replace("float.MinValue", float.MinValue.ToString())
            .Replace("float.MaxValue", float.MaxValue.ToString())
            .Replace("float.PositiveInfinity", float.PositiveInfinity.ToString())
            .Replace("float.NegativeInfinity", float.NegativeInfinity.ToString())
            .Replace("float.NaN", float.NaN.ToString())
            .Replace("float.Epsilon", float.Epsilon.ToString());

        return cleaned;
    }
}

public class NCalcRequest
{
    [JsonProperty("expressions")]
    public string[] Expressions { get; set; }
}

public class ResultResponse
{
    [JsonProperty("result")]
    public string Result { get; set; }

    [JsonProperty("error")]
    public string Error { get; set; }
}