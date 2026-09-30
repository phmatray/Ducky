using Microsoft.CodeAnalysis;

namespace Ducky.Draft.Generators;

/// <summary>Emits the nested <c>Draft</c> type of <c>[Draftable]</c> records (SPEC §13.3; emits nothing until M10).</summary>
[Generator(LanguageNames.CSharp)]
public sealed class DraftGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}
