using Moq;

namespace FakeProject;

[TestFixture]
public class MathsServiceTests
{
    private TestDelayer _testDelayer;
    private Mock<IMathsRequester> _mathsRequesterMock;
    private Mock<IMather> _matherMock;
    private Mock<IOutputter> _outputterMock;
    private MathsService _service;

    [SetUp]
    public void SetUp()
    {
        _testDelayer = new TestDelayer();
        _mathsRequesterMock = new Mock<IMathsRequester>();
        _matherMock = new Mock<IMather>();
        _outputterMock = new Mock<IOutputter>();
        _service = new MathsService(_testDelayer, _mathsRequesterMock.Object, _matherMock.Object, _outputterMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _testDelayer.Dispose();
    }

    [Test]
    public async Task Start_WhenCalled_SetsStateToStartingAndStarted()
    {
        // Arrange
        var stateChanges = new List<ServiceState>();
        _service.OnStateChanged += (s, e) => stateChanges.Add(e);
        _testDelayer.WaitForElapsed = true;

        // Act
        var startTask = _service.Start();
        _testDelayer.Elapse(); // Simulate delay elapsing
        await startTask;

        // Assert
        Assert.That(stateChanges, Is.EquivalentTo(new[] { ServiceState.Starting, ServiceState.Started }), "State changes did not occur as expected.");
        Assert.That(_service.State, Is.EqualTo(ServiceState.Started), "Service state did not change to Started.");
        _mathsRequesterMock.VerifyAdd(m => m.OnMathsRequested += It.IsAny<EventHandler<(double x, double y)>>(), Times.Once);
        _mathsRequesterMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Stop_WhenCalled_SetsStateToStoppingAndStopped()
    {
        // Arrange
        var stateChanges = new List<ServiceState>();
        _service.OnStateChanged += (s, e) => stateChanges.Add(e);
        await _service.Start(); // Ensure the service has started first
        stateChanges.Clear(); // Clear state changes from the start process
        _testDelayer.WaitForElapsed = true;

        // Act
        var stopTask = _service.Stop();
        _testDelayer.Elapse(); // Simulate delay elapsing
        await stopTask;

        // Assert
        Assert.That(stateChanges, Is.EquivalentTo(new[] { ServiceState.Stopping, ServiceState.Stopped }), "State changes did not occur as expected.");
        Assert.That(_service.State, Is.EqualTo(ServiceState.Stopped), "Service state did not change to Stopped.");
        _mathsRequesterMock.VerifyRemove(m => m.OnMathsRequested -= It.IsAny<EventHandler<(double x, double y)>>(), Times.Once);
    }

    [Test]
    public void OnMathsRequest_WhenCalled_OutputsResult()
    {
        // Arrange
        double x = 2.0, y = 3.0;
        double expectedResult = 5.0;
        _matherMock.Setup(m => m.DoMaths(x, y)).Returns(expectedResult);
        _service.Start().Wait(); // Ensure event subscription

        // Act
        _mathsRequesterMock.Raise(m => m.OnMathsRequested += null, this, (x, y));

        // Assert
        _outputterMock.Verify(o => o.Output(It.Is<string>(s => s == $"Result: {expectedResult}")), Times.Once);
        _mathsRequesterMock.Verify(m => m.RequestMaths(x, y), Times.Never);
        _matherMock.Verify(m => m.DoMaths(x, y), Times.Once);
    }

    [Test]
    public void Name_WhenCalled_ReturnsExpectedName()
    {
        // Arrange & Act
        var name = _service.Name;

        // Assert
        Assert.That(name, Is.EqualTo("I am some service"));
        Assert.IsNotNull(name);
        Assert.IsNotEmpty(name);
    }

    [Test]
    public async Task State_WhenChanges_RaisesOnStateChangedEvent()
    {
        // Arrange
        var stateChanges = new List<ServiceState>();
        _service.OnStateChanged += (s, e) => stateChanges.Add(e);

        // Act
        await _service.Start();
        await _service.Stop();

        // Assert
        Assert.That(stateChanges, Is.EquivalentTo(new[] { ServiceState.Starting, ServiceState.Started, ServiceState.Stopping, ServiceState.Stopped }));
    }

    [Test]
    public void Start_WhenTimeoutOccurs_StateRemainsStarting()
    {
        // Arrange
        _testDelayer.WaitForElapsed = true;

        // Act
        var exception = Assert.ThrowsAsync<TimeoutException>(async () => await _service.Start());

        // Assert
        Assert.That(exception.Message, Is.EqualTo("Delay took too long, did you forget to dispose or elapse?"));
        Assert.That(_service.State, Is.EqualTo(ServiceState.Starting));
        _mathsRequesterMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Start_WhenExceptionThrown_ThrowsException()
    {
        // Arrange
        _testDelayer.WaitForElapsed = false;
        _mathsRequesterMock.SetupAdd(m => m.OnMathsRequested += It.IsAny<EventHandler<(double x, double y)>>()).Throws<Exception>();

        // Act & Assert
        var exception = Assert.ThrowsAsync<Exception>(async () => await _service.Start());
        Assert.That(_service.State, Is.EqualTo(ServiceState.Starting));
        _mathsRequesterMock.VerifyAdd(m => m.OnMathsRequested += It.IsAny<EventHandler<(double x, double y)>>(), Times.Once);
        _mathsRequesterMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Stop_WhenExceptionThrown_ThrowsException()
    {
        // Arrange
        _testDelayer.WaitForElapsed = false;
        await _service.Start();
        _mathsRequesterMock.SetupRemove(m => m.OnMathsRequested -= It.IsAny<EventHandler<(double x, double y)>>()).Throws<Exception>();

        // Act & Assert
        var exception = Assert.ThrowsAsync<Exception>(async () => await _service.Stop());
        Assert.That(_service.State, Is.EqualTo(ServiceState.Stopped));
        _mathsRequesterMock.VerifyRemove(m => m.OnMathsRequested -= It.IsAny<EventHandler<(double x, double y)>>(), Times.Once);
    }

    [Test]
    public void OnMathsRequest_WhenCalled_VerifiesMathsRequesterInvocation()
    {
        // Arrange
        double x = 5.0, y = 7.0;
        _service.Start().Wait();

        // Act
        _mathsRequesterMock.Raise(m => m.OnMathsRequested += null, this, (x, y));

        // Assert
        _matherMock.Verify(m => m.DoMaths(x, y), Times.Once);
        _outputterMock.Verify(o => o.Output(It.Is<string>(s => s.Contains("Result:"))), Times.Once);
    }

    [Test]
    public async Task Stop_WhenCalledNotStarted_DoesNotChangeState()
    {
        // Arrange
        var stateChanges = new List<ServiceState>();
        _service.OnStateChanged += (s, e) => stateChanges.Add(e);

        // Act
        await _service.Stop();

        // Assert
        Assert.That(stateChanges, Is.Empty);
        Assert.That(_service.State, Is.EqualTo(ServiceState.Stopped));
    }

    [Test]
    public void Start_WhenAlreadyStarted_DoesNotChangeState()
    {
        // Arrange
        _service.Start().Wait();
        var stateBefore = _service.State;

        // Act
        _service.Start().Wait();

        // Assert
        Assert.That(_service.State, Is.EqualTo(stateBefore));
    }

    [Test]
    public void Stop_WhenAlreadyStopped_DoesNotChangeState()
    {
        // Arrange
        var stateBefore = _service.State;

        // Act
        _service.Stop().Wait();

        // Assert
        Assert.That(_service.State, Is.EqualTo(stateBefore));
    }
}