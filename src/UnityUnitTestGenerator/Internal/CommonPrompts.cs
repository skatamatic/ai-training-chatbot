using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitTestGenerator.Model;

namespace UnitTestGenerator.Internal
{
    internal static class CommonPrompts
    {
        public static readonly string CommonTestGuidelines =
        @$"
ALWAYS use the built in 'calculate' function if you need to to assert on a mathematical expression (you are NOT good at math on your own).  Do not just output functions.calculate() in the test code, compute the value ahead of time then use it in the test.
When asserting doubles be sure to use a reasonable tolerance.  Generally 5 or 6 decimals places will work.  Do NOT use Math.Round for this, use .Within() or .Approximately() or similar.
NEVER test with real Task.Delay or Thread.Sleep.  Instead, use Task.Delay(1) or Task.Yield() or Task.CompletedTask in any mocks - or better yet use any relevant supplemental classes provided.
ALWAYS use supplemental classes instead of mocking their interfaces.
NEVER test any private or protected methods or properties!  Only public ones.  If you think you need to test a private method, you are wrong.  You need to test the public method that calls it or invoke it through events.
NEVER USE REFLECTION or any clever tricks in your tests.
You cannot mock optional parameters.  Instead, just do an It.IsAny<type>() when you encounter them in an interface.
Often it will be hard to test methods directly, you will need to mock raising events to execute the code.  Make sure to do this and not use reflection.
Ensure you are using best practices and excellent code quality
Use [TestCases] and [Values] as appropriate to cover multiple inputs.  Favor this over multiple tests, do not add redundant tests covered by other tests/test cases.
Do NOT try to substitute ANY concrete classes.  That does not work.
Do not ask for any permissions or responses or use any non json output.
";
    }
}
