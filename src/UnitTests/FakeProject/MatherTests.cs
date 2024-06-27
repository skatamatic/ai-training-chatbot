using System;
using NUnit.Framework;

namespace FakeProject;

[TestFixture]
public class MatherTests
{
    private Mather _mather;
    
    [SetUp]
    public void SetUp()
    {
        _mather = new Mather();
    }
    
    [Test]
    [TestCase(0, 0, 0)]
    [TestCase(1, 0, 3.141592653589793)]
    [TestCase(0, 1, 1.2246467991473533E-15)]
    [TestCase(1, 1, 3.1415926535897944)]
    [TestCase(2, 0.5, 16.283185307179586)]
    [TestCase(-1, -1, -3.1415926535897944)]
    [TestCase(2.5, 0.25, 14.925049445839958)]
    [TestCase(-2.5, -0.25, -14.925049445839958)]
    [TestCase(3.14159, 1.5708, 0.11594179917207015)]
    public void DoMaths_ValidInputs_ReturnsExpectedResults(double x, double y, double expected)
    {
        // Act
        var result = _mather.DoMaths(x, y);
        
        // Assert
        Assert.That(result, Is.EqualTo(expected).Within(0.000001));
    }
}
