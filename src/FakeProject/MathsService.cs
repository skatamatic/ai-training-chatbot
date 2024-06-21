namespace FakeProject;

public class MathsService(IDelayer delayer, IMathsRequester mathsRequester, IMather mather, IOutputter outputter) : IService
{
    public string Name => "I am some service";

    private ServiceState _state = ServiceState.Stopped;
    public ServiceState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                OnStateChanged?.Invoke(this, _state);
            }
        }
    }

    public event EventHandler<ServiceState>? OnStateChanged;

    public async Task Start()
    {
        if (State is not ServiceState.Stopped)
        {
            return;
        }

        State = ServiceState.Starting;
        await delayer.Delay(1000);
        mathsRequester.OnMathsRequested += OnMathsRequest;
        State = ServiceState.Started;
    }

    private void OnMathsRequest(object? sender, (double x, double y) e)
    {
        var result = mather.DoMaths(e.x, e.y);
        outputter.Output($"Result: {result}");
    }

    public async Task Stop()
    {
        if (State is not ServiceState.Started)
        {
            return;
        }

        State = ServiceState.Stopping;
        await delayer.Delay(1000);
        State = ServiceState.Stopped;
        mathsRequester.OnMathsRequested -= OnMathsRequest;
    }
}
