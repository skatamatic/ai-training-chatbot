using UnitTestGenerator.Model;

namespace UnitTestGenerator.Interface;

public interface IUnitTestFixer
{
    Task<UnitTestGenerationResult> Fix(FixContext context, string testFilePath, string uutPath);
}
