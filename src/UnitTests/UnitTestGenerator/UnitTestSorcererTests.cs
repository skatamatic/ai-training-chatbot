using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using CSharpTools.SolutionTools;
using CSharpTools.TestRunner;

namespace UnitTestGenerator.Tests
{
    [TestFixture]
    public class UnitTestSorcererTests
    {
        private Mock<IUnitTestFixer> _mockFixer;
        private Mock<IUnitTestGenerator> _mockGenerator;
        private Mock<IUnitTestRunner> _mockRunner;
        private Mock<ISolutionTools> _mockSolutionTools;
        private List<string> _loggedMessages;
        private Action<string> _mockOutput;
        private UnitTestSorcererConfig _config;
        private UnitTestSorcerer _sut; // System Under Test

        [SetUp]
        public void SetUp()
        {
            _mockFixer = new Mock<IUnitTestFixer>();
            _mockGenerator = new Mock<IUnitTestGenerator>();
            _mockRunner = new Mock<IUnitTestRunner>();
            _mockSolutionTools = new Mock<ISolutionTools>();
            _loggedMessages = new List<string>();
            _mockOutput = _loggedMessages.Add; // Capturing log messages
            _config = new UnitTestSorcererConfig { MaxFixAttempts = 3 };
            _sut = new UnitTestSorcerer(_config, _mockFixer.Object, _mockGenerator.Object, _mockRunner.Object, _mockSolutionTools.Object, _mockOutput);
        }

        [Test]
        public async Task GenerateAsync_WhenGenerationSuccess_ShouldReturnTrue()
        {
            // Arrange
            var genResult = new UnitTestGenerationResult { Config = new GenerationConfig(), AIResponse = new UnitTestAIResponse() };
            _mockGenerator.Setup(g => g.Generate()).ReturnsAsync(genResult);
            var runResult = new TestRunResult { PassedTests = { new TestCaseResult() } };
            _mockSolutionTools.Setup(st => st.SaveTestFile(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("testFilePath");
            _mockSolutionTools.Setup(st => st.FindProjectFile(It.IsAny<string>())).Returns("projectFile");
            _mockRunner.Setup(r => r.RunTestsAsync(It.IsAny<string>(), null)).ReturnsAsync(runResult);

            // Act
            var result = await _sut.GenerateAsync();

            // Assert
            Assert.That(result, Is.True);
            _mockGenerator.Verify(g => g.Generate(), Times.Once);
            _mockSolutionTools.Verify(st => st.SaveTestFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            Assert.That(_loggedMessages, Is.Not.Empty);
        }

        [Test]
        public async Task GenerateAsync_WhenGenerationFails_ShouldAttemptFixAndReturnTrue()
        {
            // Arrange
            var genResult = new UnitTestGenerationResult { Config = new GenerationConfig(), AIResponse = new UnitTestAIResponse() };
            var fixResult = new UnitTestGenerationResult { Config = new GenerationConfig(), AIResponse = new UnitTestAIResponse() };
            var failedRunResult = new TestRunResult { FailedTests = { new TestCaseResult() } };
            var successfulRunResult = new TestRunResult { PassedTests = { new TestCaseResult() } };

            _mockGenerator.Setup(g => g.Generate()).ReturnsAsync(genResult);
            _mockSolutionTools.Setup(st => st.SaveTestFile(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("testFilePath");
            _mockSolutionTools.Setup(st => st.FindProjectFile(It.IsAny<string>())).Returns("projectFile");
            _mockRunner.SetupSequence(r => r.RunTestsAsync(It.IsAny<string>(), null)).ReturnsAsync(failedRunResult).ReturnsAsync(successfulRunResult);
            _mockFixer.Setup(f => f.Fix(It.IsAny<FixContext>(), It.IsAny<string>())).ReturnsAsync(fixResult);

            // Act
            var result = await _sut.GenerateAsync();

            // Assert
            Assert.That(result, Is.True);
            _mockFixer.Verify(f => f.Fix(It.IsAny<FixContext>(), It.IsAny<string>()), Times.Once);
            Assert.That(_loggedMessages, Is.Not.Empty);
        }

        [Test]
        public async Task GenerateAsync_WhenFixAttemptsExceedMaxFixAttempts_ShouldReturnFalse()
        {
            // Arrange
            var genResult = new UnitTestGenerationResult { Config = new GenerationConfig(), AIResponse = new UnitTestAIResponse() };
            var failedRunResult = new TestRunResult { FailedTests = { new TestCaseResult() } };
            var fixResult = new UnitTestGenerationResult { Config = new GenerationConfig(), AIResponse = new UnitTestAIResponse() };

            _mockGenerator.Setup(g => g.Generate()).ReturnsAsync(genResult);
            _mockSolutionTools.Setup(st => st.SaveTestFile(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("testFilePath");
            _mockSolutionTools.Setup(st => st.FindProjectFile(It.IsAny<string>())).Returns("projectFile");
            _mockRunner.Setup(r => r.RunTestsAsync(It.IsAny<string>(), null)).ReturnsAsync(failedRunResult);
            _mockFixer.Setup(f => f.Fix(It.IsAny<FixContext>(), It.IsAny<string>())).ReturnsAsync(fixResult);

            // Act
            var result = await _sut.GenerateAsync();

            // Assert
            Assert.That(result, Is.False);
            _mockFixer.Verify(f => f.Fix(It.IsAny<FixContext>(), It.IsAny<string>()), Times.Exactly(_config.MaxFixAttempts));
            Assert.That(_loggedMessages, Is.Not.Empty);
        }

        [Test]
        public async Task GenerateAsync_WhenMaxFixAttemptsIsZero_ShouldReturnFalse()
        {
            // Arrange
            _config.MaxFixAttempts = 0;
            var genResult = new UnitTestGenerationResult { Config = new GenerationConfig(), AIResponse = new UnitTestAIResponse() };
            var failedRunResult = new TestRunResult { FailedTests = { new TestCaseResult() } };

            _mockGenerator.Setup(g => g.Generate()).ReturnsAsync(genResult);
            _mockSolutionTools.Setup(st => st.SaveTestFile(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("testFilePath");
            _mockSolutionTools.Setup(st => st.FindProjectFile(It.IsAny<string>())).Returns("projectFile");
            _mockRunner.Setup(r => r.RunTestsAsync(It.IsAny<string>(), null)).ReturnsAsync(failedRunResult);

            // Act
            var result = await _sut.GenerateAsync();

            // Assert
            Assert.That(result, Is.False);
            _mockFixer.Verify(f => f.Fix(It.IsAny<FixContext>(), It.IsAny<string>()), Times.Never);
            Assert.That(_loggedMessages, Is.Not.Empty);
        }

        [Test]
        public void Constructor_WhenCalled_ShouldInitializeProperties()
        {
            // Act
            var sut = new UnitTestSorcerer(_config, _mockFixer.Object, _mockGenerator.Object, _mockRunner.Object, _mockSolutionTools.Object, _mockOutput);

            // Assert
            Assert.That(sut, Is.Not.Null);
        }
    }
}
