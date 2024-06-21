namespace FakeProject;

public class Delayer : IDelayer
{
    public Task Delay(int milliseconds) => Task.Delay(milliseconds);
}

public class TestDelayer : IDelayer, IDisposable
{
    private readonly List<TaskCompletionSource<bool>> _tcsList = new();
    private bool _isDisposed = false;
    private object _lock = new();

    public bool WaitForElapsed { get; set; } = false;

    public void Elapse()
    {
        lock (_lock)
        {
            foreach (var tcs in _tcsList.ToList())
            {
                tcs.TrySetResult(true);
            }
            _tcsList.Clear();
        }
    }

    public async Task Delay(int milliseconds)
    {
        if (!WaitForElapsed)
        {
            return;
        }

        var tcs = new TaskCompletionSource<bool>();

        try
        {
            lock (_lock)
            {
                _tcsList.Add(tcs);
            }

            var timeout = Task.Delay(1000);
            var finishedFirst = await Task.WhenAny(tcs.Task, timeout);

            if (finishedFirst == timeout)
            {
                throw new TimeoutException("Delay took too long, did you forget to dispose or elapse?");
            }
        }
        finally
        {
            lock (_lock)
            {
                _tcsList.Remove(tcs);
            }
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Elapse();
            _isDisposed = true;
        }
    }
}