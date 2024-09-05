namespace Shared;

public interface IOutputter
{
    event EventHandler<string> OnOutput;
}
