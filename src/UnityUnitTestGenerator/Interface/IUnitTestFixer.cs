using Sorcerer.Model;

namespace Sorcerer.Interface;

public interface IUnitTestFixer
{
    Task<UnitTestGenerationResult> Fix(FixContext context, string testFilePath, string uutPath);
}
