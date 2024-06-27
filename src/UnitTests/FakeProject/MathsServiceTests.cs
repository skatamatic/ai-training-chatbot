using System; 
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;

namespace FakeProject;

[TestFixture]
public class MathsServiceTests
{
    private Mock<IMathsRequester> _mathsRequesterMock;
    private Mock<IMather> _matherMock;
    private Mock<IOutputter> _outputterMock;
    private TestDelayer _testDelayer;
    private MathsService _mathsService;

    [SetUp]
    public void SetUp()
    {
        _mathsRequesterMock = new Mock<IMathsRequester>();
        _matherMock = new Mock<IMather>();
        _outputterMock = new Mock<IOutputter>();
        _testDelayer = new TestDelayer();
        _mathsService = new MathsService(_testDelayer, _mathsRequesterMock.Object, _matherMock.Object, _outputterMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _testDelayer.Dispose();
    }

    [Test]
    public void Name_ReturnsCorrectValue()
    {
        // Act
        var name = _mathsService.Name;

        // Assert
        Assert.That(name, Is.EqualTo("I am some service"));
    }

    [Test]
    public async Task Start_WhenAlreadyStarted_DoesNotChangeState()
    {
        // Arrange
        await _mathsService.Start();

        // Act
        var task = _mathsService.Start();
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(_mathsService.State, Is.EqualTo(ServiceState.Started));
    }

    [Test]
    public async Task Start_WhenStopped_ChangesStateCorrectly()
    {
        // Arrange
        var stateChangedCount = 0;
        _mathsService.OnStateChanged += (sender, state) => stateChangedCount++;

        // Act
        var task = _mathsService.Start();
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(_mathsService.State, Is.EqualTo(ServiceState.Started));
        Assert.That(stateChangedCount, Is.EqualTo(2)); // Starting, Started
    }

    [Test]
    public async Task Stop_WhenAlreadyStopped_DoesNotChangeState()
    {
        // Arrange
        await _mathsService.Start();
        var task = _mathsService.Stop();
        _testDelayer.Elapse();
        await task;

        // Act
        task = _mathsService.Stop();
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(_mathsService.State, Is.EqualTo(ServiceState.Stopped));
    }

    [Test]
    public async Task Stop_WhenStarted_ChangesStateCorrectly()
    {
        // Arrange
        await _mathsService.Start();
        var task = _mathsService.Stop();

        // Act
        _testDelayer.Elapse();
        await task;

        // Assert
        Assert.That(_mathsService.State, Is.EqualTo(ServiceState.Stopped));
    }

    [TestCase(5, 3, 8)]
    [TestCase(-1, -1, -2)]
    [TestCase(double.MaxValue, double.MinValue, 0)]
    public async Task OnMathsRequested_RaisesEventAndOutputsResult(double x, double y, double expectedResult)
    {
        // Arrange
        _matherMock.Setup(m => m.DoMaths(x, y)).Returns(expectedResult);

        await _mathsService.Start();
        _testDelayer.Elapse();

        // Act
        _mathsRequesterMock.Raise(m => m.OnMathsRequested += null, (object?)null, (x, y));

        // Assert
        _outputterMock.Verify(o => o.Output(It.Is<string>(s => s.Contains(expectedResult.ToString()))), Times.Once);
        Assert.That(_mathsRequesterMock.Invocations.Count > 0, "OnMathsRequested is set and invoked");
    }

    [Test]
    public async Task OnMathsRequested_HandlesExceptionAndOutputsError()
    {
        // Arrange
        double x = 5.0;
        double y = 3.0;
        string exceptionMessage = "Test Exception";
        _matherMock.SetupSequence(m => m.DoMaths(x, y)).Throws(new Exception(exceptionMessage));

        await _mathsService.Start();
        _testDelayer.Elapse();

        // Act
        _mathsRequesterMock.Raise(m => m.OnMathsRequested += null, (object?)null, (x, y));

        // Assert
        _outputterMock.Verify(o => o.Output(It.Is<string>(s => s.Contains(exceptionMessage))), Times.Once);
    }

    [Test]
    public async Task OnStateChanged_EventIsRaisedCorrectly()
    {
        // Arrange
        int stateChangedCount = 0;
        _mathsService.OnStateChanged += (sender, state) => stateChangedCount++;

        // Act
        var startTask = _mathsService.Start();
        _testDelayer.Elapse();
        await startTask;

        var stopTask = _mathsService.Stop();
        _testDelayer.Elapse();
        await stopTask;

        // Assert
        Assert.That(stateChangedCount, Is.EqualTo(4)); // Starting, Started, Stopping, Stopped
    }
}
