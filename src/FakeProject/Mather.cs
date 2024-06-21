namespace FakeProject;

public class Mather : IMather
{
   public double DoMaths(double x, double y)
    {
        return (x * Math.PI) + (10 * Math.Sin(y * Math.PI));
    }
}
