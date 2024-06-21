namespace FakeProject;

public interface IMathsRequester
{
    event EventHandler<(double x, double y)> OnMathsRequested;
    void RequestMaths(double x, double y);
}

public class MathsRequester : IMathsRequester
{
    public event EventHandler<(double x, double y)>? OnMathsRequested;

    public void RequestMaths(double x, double y)
    {
        OnMathsRequested?.Invoke(this, (x, y));
    }
}
