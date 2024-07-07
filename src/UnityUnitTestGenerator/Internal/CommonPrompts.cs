namespace UnitTestGenerator.Internal;

internal static class CommonPrompts
{
    public static readonly string CommonTestGuidelines =
    @$"
ALWAYS use the 'evaluate_expression' open AI function if you need to to assert on a mathematical expression (you are NOT good at math without it).  This is an OpenAI function you must invoke - do not add expressions to code.  Use it to compute expressions then add the RESULT to the code directly.  Compute the value with it then use it in the tests as needed.
When asserting doubles be sure to use a reasonable tolerance.  Generally 5 or 6 decimals places will work.  Do NOT use Math.Round for this, use .Within() or .Approximately() or similar.
NEVER test with real Task.Delay or Thread.Sleep.  Instead, use any relevant supplemental classes provided or fallback to Task.CompletedTask as the return value.
ALWAYS use supplemental classes instead of mocking their interfaces.
NEVER test any private or protected methods or properties!  Only public ones.  If you think you need to test a private method, you are wrong.  You need to test the public method that calls it or invoke it through events.
NEVER USE REFLECTION or any clever tricks in your tests.
You cannot mock optional parameters.  Instead, just do an It.IsAny<type>() when you encounter them in an interface.
Often it will be hard to test methods directly, you will need to mock raising events to execute the code.  Make sure to do this and not use reflection.
Ensure you are using best practices and excellent code quality.
Use [TestCase] and [Values] as appropriate to cover multiple inputs - favor this over multiple tests.  Do not add redundant tests covered by other tests/test cases.
NEVER try to substitute ANY concrete classes.
NEVER assert anything in setup or teardown methods.
Do not ask for any permissions or responses or use any non json output.
ALWAYS output full test file code unless explicitly told not to.  Never shortcut with comments like // Other existing tests...
Never test any log messages (in unity this means LogAssert.Expect)
";
}
