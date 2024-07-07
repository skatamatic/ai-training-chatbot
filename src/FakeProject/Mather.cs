namespace FakeProject;

public class Mather : IMather
{
    readonly IMathOutput _output;

    public Mather(IMathOutput output)
    {
        _output = output;
    }

    public double DoMaths(double x, double y, MathsAlgorithm algo)
    {
        switch (algo)
        {
            case MathsAlgorithm.Algo1:
            {
                var result = (x * Math.PI) + (10 * Math.Sin(y * Math.PI));
                _output.Output($"Running algo 1 with x={x} and y={y} resulted in {result}");
                return result;
            }
            case MathsAlgorithm.Algo2:
            {
                var result = Math.Pow(x, 2) + Math.Pow(y, 3);
                _output.Output($"Running algo 2 with x={x} and y={y} resulted in {result}");
                return result;
            }
            case MathsAlgorithm.Algo3:
            {
                var result = Math.Pow(x, 2) + Math.Pow(y, 3) - 1000;
                _output.Output($"Running algo 3 with x={x} and y={y} resulted in {result}");
                return result;
            }
            default:
                throw new NotSupportedException($"Algorithm {algo} is not supported");
        }
        
    }
}
