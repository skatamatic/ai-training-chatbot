namespace FakeProject;

public interface IOutputter
{
    void Output(string message);
}

public class Outputter : IOutputter
{
    public void Output(string message)
    {
        Console.WriteLine(message);
    }
}