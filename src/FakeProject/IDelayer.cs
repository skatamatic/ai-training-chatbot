namespace FakeProject;

public interface IDelayer
{
    Task Delay(int milliseconds);
}
