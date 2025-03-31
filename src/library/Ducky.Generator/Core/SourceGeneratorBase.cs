using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Ducky.Generator.Core;

/// <summary>
/// A base class for source generators.
/// Provides helper methods for adding sources and other common tasks.
/// </summary>
public abstract class SourceGeneratorBase : IIncrementalGenerator
{
    /// <inheritdoc/>
    public abstract void Initialize(IncrementalGeneratorInitializationContext context);

    /// <summary>
    /// Adds a generated source file to the compilation.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="hintName">A unique hint name for the generated file.</param>
    /// <param name="source">The source code to add.</param>
    protected void AddSource(in SourceProductionContext context, string hintName, string source)
    {
        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }
}
