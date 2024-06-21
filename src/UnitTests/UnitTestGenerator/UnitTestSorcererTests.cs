using NUnit.Framework;
using Moq;
using System;
using System.Threading.Tasks;
using UnitTestGenerator;
using CSharpTools.TestRunner;
using CSharpTools.SolutionTools;
using CSharpTools.DefinitionAnalyzer;

namespace UnitTestGenerator;

[TestFixture]
public class UnitTestSorcererTests
{
    private Mock<IUnitTestFixer> _mockFixer;
    private Mock<IUnitTestGenerator> _mockGenerator;
    private Mock<IUnitTestRunner> _mockRunner;
    private Mock<IUnitTestEnhancer> _mockEnhancer;
    private Mock<ISolutionTools> _mockSolutionTools;
    private UnitTestSorcererConfig _config;
    private UnitTestSorcerer _unitTestSorcerer;
    private Mock<Action<string>> _mockOutput;

    [SetUp]
    public void SetUp()
    {
        _mockFixer = new Mock<IUnitTestFixer>();
        _mockGenerator = new Mock<IUnitTestGenerator>();
        _mockRunner = new Mock<IUnitTestRunner>();
        _mockEnhancer = new Mock<IUnitTestEnhancer>();
        _mockSolutionTools = new Mock<ISolutionTools>();
        _mockOutput = new Mock<Action<string>>();

        _config = new UnitTestSorcererConfig
        {
            MaxFixAttempts = 3,
            EnhancementPasses = 2,
            FileToTest = "TestFile.cs"
        };

        _unitTestSorcerer = new UnitTestSorcerer(
            _config,
            _mockFixer.Object,
            _mockGenerator.Object,
            _mockRunner.Object,
            _mockSolutionTools.Object,
            _mockEnhancer.Object,
            _mockOutput.Object);
    }

    [Test]
    public async Task GenerateAsync_WhenGenerationAndRunTestSuccess_ExpectSuccessMessages()
    {
        // Arrange
        var genResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Test content" }
        };

        _mockGenerator.Setup(g => g.Generate(_config.FileToTest)).ReturnsAsync(genResult);
        _mockSolutionTools.Setup(st => st.SaveTestFile(_config.FileToTest, genResult.AIResponse.TestFileContent)).ReturnsAsync("TestFilePath");
        _mockSolutionTools.Setup(st => st.FindProjectFile("TestFilePath")).Returns("TestProjectFilePath");
        _mockRunner.Setup(r => r.RunTestsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new TestRunResult { PassedTests = { new TestCaseResult() } });

        // Act
        var result = await _unitTestSorcerer.GenerateAsync();

        // Assert
        Assert.That(result, Is.True);
        _mockOutput.Verify(o => o(It.IsAny<string>()), Times.Exactly(6), "Expected 6 log messages");
        _mockOutput.Verify(o => o("Generating tests..."), Times.Once);
        _mockOutput.Verify(o => o("Tests written to TestFilePath"), Times.Once);
        _mockOutput.Verify(o => o("Running tests..."), Times.Once);
        _mockOutput.Verify(o => o("Enhancing tests (pass 1/2)"), Times.Once);
        _mockOutput.Verify(o => o("Tests enhanced"), Times.Once);
        _mockOutput.Verify(o => o("Done"), Times.Once);
    }

    [Test]
    public async Task GenerateAsync_WhenTestsFailAfterFixAttempts_LogsFailureMessageAndReturnsFalse()
    {
        // Arrange
        var genResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Test content" }
        };

        _mockGenerator.Setup(g => g.Generate(_config.FileToTest)).ReturnsAsync(genResult);
        _mockSolutionTools.Setup(st => st.SaveTestFile(_config.FileToTest, genResult.AIResponse.TestFileContent)).ReturnsAsync("TestFilePath");
        _mockSolutionTools.Setup(st => st.FindProjectFile("TestFilePath")).Returns("TestProjectFilePath");
        _mockRunner.Setup(r => r.RunTestsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new TestRunResult { Errors = { "Test Error" } });
        _mockFixer.Setup(f => f.Fix(It.IsAny<FixContext>(), "TestFilePath", _config.FileToTest)).ReturnsAsync(genResult);

        // Act
        var result = await _unitTestSorcerer.GenerateAsync();

        // Assert
        Assert.That(result, Is.False);
        _mockOutput.Verify(o => o(It.IsAny<string>()), Times.AtLeastOnce);
        _mockOutput.Verify(o => o("Failed to fix tests after max attempts."), Times.Once);
    }

    [Test]
    public async Task GenerateAsync_WhenEnhancementFails_LogsEnhancementFailureAndReturnsFalse()
    {
        // Arrange
        var genResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Test content" }
        };
        var run1Result = new TestRunResult { PassedTests = { new TestCaseResult() } };
        var run2Result = new TestRunResult { FailedTests = { new TestCaseResult() } };

        _mockGenerator.Setup(g => g.Generate(_config.FileToTest)).ReturnsAsync(genResult);
        _mockSolutionTools.Setup(st => st.SaveTestFile(_config.FileToTest, genResult.AIResponse.TestFileContent)).ReturnsAsync("TestFilePath");
        _mockSolutionTools.Setup(st => st.FindProjectFile("TestFilePath")).Returns("TestProjectFilePath");

        _mockRunner.SetupSequence(r => r.RunTestsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(run1Result).ReturnsAsync(run2Result);

        // Act
        var result = await _unitTestSorcerer.GenerateAsync();

        // Assert
        Assert.That(result, Is.False);
        _mockOutput.Verify(o => o("Enhancements failed"), Times.Once);
    }

    [Test]
    public async Task GenerateAsync_WhenEnhancesSuccessfully_ExpectLogsSuccess()
    {
        // Arrange
        var genResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Test content" },
        };
        var enhancedResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Enhanced content" }
        };

        _mockGenerator.Setup(g => g.Generate(_config.FileToTest)).ReturnsAsync(genResult);
        _mockSolutionTools.Setup(st => st.SaveTestFile(_config.FileToTest, genResult.AIResponse.TestFileContent)).ReturnsAsync("TestFilePath");
        _mockSolutionTools.Setup(st => st.FindProjectFile("TestFilePath")).Returns("TestProjectFilePath");
        _mockRunner.Setup(r => r.RunTestsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new TestRunResult { PassedTests = { new TestCaseResult() } });
        _mockEnhancer.Setup(e => e.Enhance(It.IsAny<AnalysisResult>(), _config.FileToTest, "TestFilePath")).ReturnsAsync(enhancedResult);

        // Act
        var result = await _unitTestSorcerer.GenerateAsync();

        // Assert
        Assert.That(result, Is.True);
        _mockOutput.Verify(o => o("Tests enhanced"), Times.Once);
    }

    [Test]
    public async Task GenerateAsync_WhenTestsAlreadyExist_SkipsToEnhanceAndLogsMessages()
    {
        // Arrange
        _config.SkipToEnhanceIfTestsExist = true;
        var genResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Test content" },
            Analysis = new AnalysisResult()
        };
        var enhancedResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Enhanced content" }
        };

        string existingTestPath = "ExistingTestPath";
        _mockSolutionTools.Setup(st => st.HasTestsAlready(_config.FileToTest, out existingTestPath)).Returns(true);
        _mockGenerator.Setup(g => g.AnalyzeOnly(_config.FileToTest)).ReturnsAsync(genResult);
        _mockEnhancer.Setup(e => e.Enhance(It.IsAny<AnalysisResult>(), _config.FileToTest, existingTestPath)).ReturnsAsync(enhancedResult);
        _mockRunner.Setup(r => r.RunTestsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new TestRunResult { PassedTests = { new TestCaseResult() } });

        // Act
        var result = await _unitTestSorcerer.GenerateAsync();

        // Assert
        Assert.That(result, Is.True);
        _mockOutput.Verify(o => o("Skipping generation - found existing tests at 'ExistingTestPath'"), Times.Once);
        _mockOutput.Verify(o => o("Tests enhanced"), Times.Once);
    }

    [Test]
    public async Task GenerateAsync_WhenTestEnhancementSucceeds_InvokesOutputCorrectly()
    {
        // Arrange
        var genResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Test content" },
            Analysis = new AnalysisResult()
        };
        var enhancedResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Enhanced content" }
        };

        _mockGenerator.Setup(g => g.Generate(_config.FileToTest)).ReturnsAsync(genResult);
        _mockSolutionTools.Setup(st => st.SaveTestFile(_config.FileToTest, genResult.AIResponse.TestFileContent)).ReturnsAsync("TestFilePath");
        _mockSolutionTools.Setup(st => st.FindProjectFile("TestFilePath")).Returns("TestProjectFilePath");
        _mockRunner.Setup(r => r.RunTestsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new TestRunResult { PassedTests = { new TestCaseResult() } });
        _mockEnhancer.Setup(e => e.Enhance(It.IsAny<AnalysisResult>(), _config.FileToTest, "TestFilePath")).ReturnsAsync(enhancedResult);

        // Act
        var result = await _unitTestSorcerer.GenerateAsync();

        // Assert
        Assert.That(result, Is.True);
        _mockOutput.Verify(o => o("Tests enhanced"), Times.Once);
    }

    [Test]
    public async Task GenerateAsync_WhenTestFixSucceeds_InvokesOutputCorrectly()
    {
        // Arrange
        var genResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Test content" },
            Analysis = new AnalysisResult()
        };
        var runResult = new TestRunResult { FailedTests = { new TestCaseResult() } };
        var fixedResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Fixed content" }
        };

        _mockGenerator.Setup(g => g.Generate(_config.FileToTest)).ReturnsAsync(genResult);
        _mockSolutionTools.Setup(st => st.SaveTestFile(_config.FileToTest, genResult.AIResponse.TestFileContent)).ReturnsAsync("TestFilePath");
        _mockSolutionTools.Setup(st => st.FindProjectFile("TestFilePath")).Returns("TestProjectFilePath");
        _mockRunner.SetupSequence(r => r.RunTestsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(runResult)
            .ReturnsAsync(new TestRunResult { PassedTests = { new TestCaseResult() } });
        _mockFixer.Setup(f => f.Fix(It.IsAny<FixContext>(), "TestFilePath", _config.FileToTest)).ReturnsAsync(fixedResult);
        _mockSolutionTools.Setup(st => st.WriteSourceFile("TestFilePath", fixedResult.AIResponse.TestFileContent)).Returns(Task.CompletedTask);

        // Act
        var result = await _unitTestSorcerer.GenerateAsync();

        // Assert
        Assert.That(result, Is.True);
        _mockOutput.Verify(o => o(It.IsAny<string>()), Times.AtLeastOnce);
        _mockOutput.Verify(o => o("Fixed tests written to TestFilePath"), Times.Once);
        _mockOutput.Verify(o => o("Running fixed tests..."), Times.Once);
        _mockOutput.Verify(o => o("Success!"), Times.Once);
    }

    [Test]
    public async Task GenerateAsync_WhenMockRunnerThrowsException_ExpectFailureMessageAndReturnsFalse()
    {
        // Arrange
        var genResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Test content" }
        };

        _mockGenerator.Setup(g => g.Generate(_config.FileToTest)).ReturnsAsync(genResult);
        _mockSolutionTools.Setup(st => st.SaveTestFile(_config.FileToTest, genResult.AIResponse.TestFileContent)).ReturnsAsync("TestFilePath");
        _mockSolutionTools.Setup(st => st.FindProjectFile("TestFilePath")).Returns("TestProjectFilePath");

        var exceptionMessage = "Test runner exception";
        _mockRunner.Setup(r => r.RunTestsAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception(exceptionMessage));

        // Act
        var result = await _unitTestSorcerer.GenerateAsync();

        // Assert
        Assert.That(result, Is.False);
        _mockOutput.Verify(o => o(It.IsAny<string>()), Times.AtLeastOnce);
        _mockOutput.Verify(o => o($"Failed to run tests: {exceptionMessage}"), Times.Once);
    }

    [Test]
    public async Task GenerateAsync_WhenIntermittentFailures_ExpectRetryAndEventuallySuccess()
    {
        // Arrange
        var genResult = new UnitTestGenerationResult
        {
            AIResponse = new UnitTestAIResponse { TestFileContent = "Test content" }
        };
        var runResultFail = new TestRunResult { FailedTests = { new TestCaseResult { FullName = "FailingTest" } } };
        var runResultPass = new TestRunResult { PassedTests = { new TestCaseResult { FullName = "PassingTest" } } };

        _mockGenerator.Setup(g => g.Generate(_config.FileToTest)).ReturnsAsync(genResult);
        _mockSolutionTools.Setup(st => st.SaveTestFile(_config.FileToTest, genResult.AIResponse.TestFileContent)).ReturnsAsync("TestFilePath");
        _mockSolutionTools.Setup(st => st.FindProjectFile("TestFilePath")).Returns("TestProjectFilePath");
        _mockRunner.SetupSequence(r => r.RunTestsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(runResultFail)
            .ReturnsAsync(runResultPass);

        _mockFixer.Setup(f => f.Fix(It.IsAny<FixContext>(), "TestFilePath", _config.FileToTest)).ReturnsAsync(genResult);

        // Act
        var result = await _unitTestSorcerer.GenerateAsync();

        // Assert
        Assert.That(result, Is.True);
        _mockOutput.Verify(o => o(It.IsAny<string>()), Times.AtLeastOnce);
        _mockOutput.Verify(o => o("Generating tests..."), Times.Once);
        _mockOutput.Verify(o => o($"Failed... Attempting to fix (1/{_config.MaxFixAttempts})"), Times.Once);
        _mockOutput.Verify(o => o("Success!"), Times.Once);
    }
}
