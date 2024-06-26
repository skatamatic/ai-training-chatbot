namespace FakeProject;

using NUnit.Framework;
using Moq;
using System;
using System.Threading.Tasks;

[TestFixture]
public class MathsServiceTests
{
    private TestDelayer _testDelayer;
    private Mock<IMathsRequester> _mockMathsRequester;
    private Mock<IMather> _mockMather;
    private Mock<IOutputter> _mockOutputter;
    private MathsService _mathsService;

    [SetUp]
    public void SetUp()
    {
        _testDelayer = new TestDelayer { WaitForElapsed = true };
        _mockMathsRequester = new Mock<IMathsRequester>();
        _mockMather = new Mock<IMather>();
        _mockOutputter = new Mock<IOutputter>();
        _mathsService = new MathsService(_testDelayer, _mockMathsRequester.Object, _mockMather.Object, _mockOutputter.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _testDelayer.Dispose();
    }

    [Test]
    public void Name_ReturnsExpectedValue()
    {
        // Act
        var name = _mathsService.Name;

        // Assert
        Assert.That(name, Is.EqualTo("I am some service"));
    }

    [Test]
    public async Task Start_WhenServiceIsStopped_ChangesStateToStarted()
    {
        // Act
        var task = _mathsService.Start();
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(_mathsService.State, Is.EqualTo(ServiceState.Started));
    }

    [Test]
    public async Task Start_WhenServiceIsStopped_ChangesStateToStarting()
    {
        // Act
        var task = _mathsService.Start();

        // Assert
        Assert.That(_mathsService.State, Is.EqualTo(ServiceState.Starting));
    }

    [Test]
    public async Task Stop_WhenServiceIsStarted_ChangesStateToStopped()
    {
        // Arrange
        await _mathsService.Start();
        _testDelayer.Elapse();

        // Act
        var task = _mathsService.Stop();
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(_mathsService.State, Is.EqualTo(ServiceState.Stopped));
    }

    [Test]
    public async Task Stop_WhenServiceIsStarted_ChangesStateToStopping()
    {
        // Arrange
        await _mathsService.Start();
        _testDelayer.Elapse();

        // Act
        var task = _mathsService.Stop();
        // Assert
        Assert.That(_mathsService.State, Is.EqualTo(ServiceState.Stopping));
    }

    [Test]
    public async Task Start_WhenServiceIsAlreadyStarted_DoesNotChangeState()
    {
        // Arrange
        await _mathsService.Start();
        _testDelayer.Elapse();

        // Act
        var task = _mathsService.Start();
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(_mathsService.State, Is.EqualTo(ServiceState.Started));
    }

    [Test]
    public async Task Stop_WhenServiceIsAlreadyStopped_DoesNotChangeState()
    {
        // Act
        var task = _mathsService.Stop();
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(_mathsService.State, Is.EqualTo(ServiceState.Stopped));
    }

    [Test]
    public async Task Start_RaisesOnStateChangedEvent()
    {
        // Arrange
        var stateChangedRaised = false;
        _mathsService.OnStateChanged += (s, e) => stateChangedRaised = true;

        // Act
        var task = _mathsService.Start();
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(stateChangedRaised, Is.True);
    }

    [Test]
    public async Task Stop_RaisesOnStateChangedEvent()
    {
        // Arrange
        await _mathsService.Start();
        _testDelayer.Elapse();

        var stateChangedRaised = false;
        _mathsService.OnStateChanged += (s, e) => stateChangedRaised = true;

        // Act
        var task = _mathsService.Stop();
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(stateChangedRaised, Is.True);
    }

    [Test]
    public async Task Start_WhenAlreadyStarted_DoesNotRaiseOnStateChangedEvent()
    {
        // Arrange
        await _mathsService.Start();
        _testDelayer.Elapse();

        var stateChangedRaised = false;
        _mathsService.OnStateChanged += (s, e) => stateChangedRaised = true;

        // Act
        var task = _mathsService.Start();
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(stateChangedRaised, Is.False);
    }

    [Test]
    public async Task Stop_WhenAlreadyStopped_DoesNotRaiseOnStateChangedEvent()
    {
        // Arrange
        var stateChangedRaised = false;
        _mathsService.OnStateChanged += (s, e) => stateChangedRaised = true;

        // Act
        var task = _mathsService.Stop();
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(stateChangedRaised, Is.False);
    }

    [TestCase(1.0, 2.0, 42)]
    [TestCase(3.0, 4.0, 12)]
    [TestCase(5.0, 6.0, 30)]
    [TestCase(7.0, 8.0, 56)]
    [TestCase(9.0, 10.0, 90)]
    public async Task OnMathsRequest_InvokesDoMathsAndOutput(double x, double y, double expectedResult)
    {
        // Arrange
        _mockMather.Setup(m => m.DoMaths(x, y)).Returns(expectedResult);
        await _mathsService.Start();
        _testDelayer.Elapse();

        // Act
        _mockMathsRequester.Raise(m => m.OnMathsRequested += null, this, (x, y));

        // Assert
        _mockMather.Verify(m => m.DoMaths(x, y), Times.Once);
        _mockOutputter.Verify(o => o.Output($"Result: {expectedResult}"), Times.Once);
    }

    [Test]
    public async Task OnMathsRequest_WhenExceptionThrown_OutputsErrorMessage()
    {
        // Arrange
        var exceptionMessage = "Error";
        _mockMather.Setup(m => m.DoMaths(It.IsAny<double>(), It.IsAny<double>())).Throws(new Exception(exceptionMessage));
        await _mathsService.Start();
        _testDelayer.Elapse();

        // Act
        _mockMathsRequester.Raise(m => m.OnMathsRequested += null, this, (1.0, 2.0));

        // Assert
        _mockOutputter.Verify(o => o.Output($"Error: {exceptionMessage}"), Times.Once);
    }
}
