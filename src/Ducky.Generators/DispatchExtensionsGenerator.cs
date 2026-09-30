using Microsoft.CodeAnalysis;

namespace Ducky.Generators;

/// <summary>GEN-01: emits the dispatch helpers of <c>[DuckyAction]</c> records (SPEC §16.1; emits nothing until M13).</summary>
[Generator(LanguageNames.CSharp)]
public sealed class DispatchExtensionsGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}
