namespace FakeProject;

public class Delayer : IDelayer
{
    public Task Delay(int milliseconds) => Task.Delay(milliseconds);
}

/// <summary>
/// Used to delays via IDelayer.Delay() in a unit testable manner that does not use real time
/// 
/// IMPORTANT!!!
/// 
/// Generally use WaitForElapsed = false unless the test is specifically testing time based elements of the unit under test
/// Many times it is fine to set to false which will just have the delays No-op and you do not have to await them
/// 
/// If you need to test the actual time based elements of the code use WaitForElapsed = true.  Here's an example with very important considerations,
/// especially the task assignement, elapse, then await of the assigned task.
/// 
/// Service serviceWithDelay;
/// TestDelayer delayer;
/// 
/// [SetUp]
/// public void Setup()
/// {
///     //Be sure to use a using to ensure the delayer gets disposed
///     delayer = new TestDelayer() { WaitForElapsed = true };
///     serviceWithDelay = new Service(delayer);
/// }
/// 
/// [TearDown]
/// public void TearDown()
/// {
///     delayer.Dispose(); //Important!  Make sure to dispose this to elapse any pending timers
/// }
/// 
/// [Test]
/// public void DelayTest()
/// {
///     //Note we can't directly await the method since it will hang indefinitely (well, timeout actually)
///     //We need to get a reference to it, elapse the timer, then await the task.  If there's multiple delays in it, we will need to 
///     //elapse multiple times
///     var taskMethodWithDelayInIt = serviceWithDelay.DoTheThing();
///     delayer.Elapse();
///     await taskMethodWithDelayInIt;
///     
///     //Assert whatever
/// }
/// </summary>
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