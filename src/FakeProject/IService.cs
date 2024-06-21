namespace FakeProject;

public interface IService
{
    event EventHandler<ServiceState> OnStateChanged;
    ServiceState State { get; }
    string Name { get; }
    Task Start();
    Task Stop();
}
