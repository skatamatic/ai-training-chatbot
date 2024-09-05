using Moq;
using NUnit.Framework;
using System;

namespace FakeProject
{
    /// <summary>
    /// Unit tests for the Mather class.
    /// </summary>
    [TestFixture]
    public class MatherTests
    {
        private Mock<IMathOutput> _mockOutput;
        private Mather _mather;

        /// <summary>
        /// Setup method that initializes mocks and the Mather instance.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _mockOutput = new Mock<IMathOutput>();
            _mather = new Mather(_mockOutput.Object);
        }

        /// <summary>
        /// Tests the DoMaths method of Mather class with various input values and verifies correct output and result.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="y">The y value.</param>
        /// <param name="algorithm">The algorithm to use.</param>
        /// <param name="expected">The expected result.</param>
        /// <param name="expectedOutput">The expected output message.</param>
        [TestCase(2, 3, MathsAlgorithm.Algo1, 6.28318530717959, "Running algo 1 with x=2 and y=3 resulted in 6.28318530717959")]
        [TestCase(2, 3, MathsAlgorithm.Algo2, 31, "Running algo 2 with x=2 and y=3 resulted in 31")]
        [TestCase(2, 3, MathsAlgorithm.Algo3, -969, "Running algo 3 with x=2 and y=3 resulted in -969")]
        [TestCase(0, 0, MathsAlgorithm.Algo1, 0, "Running algo 1 with x=0 and y=0 resulted in 0")]
        [TestCase(-2, -3, MathsAlgorithm.Algo2, -23, "Running algo 2 with x=-2 and y=-3 resulted in -23")]
        [TestCase(2, -3, MathsAlgorithm.Algo3, -1023, "Running algo 3 with x=2 and y=-3 resulted in -1023")]
        [TestCase(1e6, 1e6, MathsAlgorithm.Algo1, 3141592.6535897907, "Running algo 1 with x=1000000 and y=1000000 resulted in 3141592.6535897907")]
        [TestCase(-1e-6, -1e-6, MathsAlgorithm.Algo2, 9.99999E-13, "Running algo 2 with x=-1E-06 and y=-1E-06 resulted in 9.99999E-13")]
        [TestCase(-1e2, 1e3, MathsAlgorithm.Algo3, 1000009000, "Running algo 3 with x=-100 and y=1000 resulted in 1000009000")]
        [TestCase(double.MaxValue, double.MaxValue, MathsAlgorithm.Algo2, double.PositiveInfinity, "Running algo 2 with x=1.7976931348623157E+308 and y=1.7976931348623157E+308 resulted in ∞")]
        [TestCase(double.NaN, 2, MathsAlgorithm.Algo1, double.NaN, "Running algo 1 with x=NaN and y=2 resulted in NaN")]
        [TestCase(2, double.NaN, MathsAlgorithm.Algo2, double.NaN, "Running algo 2 with x=2 and y=NaN resulted in NaN")]
        [TestCase(double.PositiveInfinity, double.NegativeInfinity, MathsAlgorithm.Algo3, double.NaN, "Running algo 3 with x=∞ and y=-∞ resulted in NaN")]
        [Description("Test the DoMaths method for various input values and algorithms.")]
        public void DoMaths_WithVariousInputs_ExpectCorrectOutput(double x, double y, MathsAlgorithm algorithm, double expected, string expectedOutput)
        {
            // Act
            var result = _mather.DoMaths(x, y, algorithm);

            // Assert
            VerifyOutput(result, expected, expectedOutput);
        }

        /// <summary>
        /// Tests the DoMaths method with an unsupported algorithm and expects a NotSupportedException.
        /// </summary>
        [Test]
        [Description("Test DoMaths method with an unsupported algorithm.")]
        public void DoMaths_WithUnsupportedAlgorithm_ThrowsNotSupportedException()
        {
            // Arrange
            var unsupportedAlgorithm = (MathsAlgorithm)999;

            // Act & Assert
            var ex = Assert.Throws<NotSupportedException>(() => _mather.DoMaths(2, 3, unsupportedAlgorithm));
            Assert.That(ex.Message, Is.EqualTo("Algorithm 999 is not supported"));
        }

        /// <summary>
        /// Tests the DoMaths method with Algo4 and expects a NotSupportedException.
        /// </summary>
        [Test]
        [Description("Test DoMaths method with Algo4 expects NotSupportedException.")]
        public void DoMaths_WithAlgorithm4_ThrowsNotSupportedException()
        {
            // Act & Assert
            var ex = Assert.Throws<NotSupportedException>(() => _mather.DoMaths(2, 3, MathsAlgorithm.Algo4));
            Assert.That(ex.Message, Is.EqualTo("Algorithm Algo4 is not supported"));
        }

        /// <summary>
        /// Verifies the result and the output message.
        /// </summary>
        /// <param name="result">The actual result.</param>
        /// <param name="expected">The expected result.</param>
        /// <param name="expectedOutput">The expected output message.</param>
        private void VerifyOutput(double result, double expected, string expectedOutput)
        {
            Assert.That(result, Is.EqualTo(expected).Within(1e-6));
            _mockOutput.Verify(m => m.Output(expectedOutput), Times.Once);
        }
    }
}