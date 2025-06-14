using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;

namespace Ducky.Generator.Core;

/// <summary>
/// Base class for Roslyn-based generators.
/// Implements the common flow:
/// 1) Build a model (CompilationUnitElement)
/// 2) Visit it to get a SyntaxTree
/// 3) Format it via an AdhocWorkspace
/// 4) Return its text
/// </summary>
public abstract class SourceGeneratorBase<TOptions>
{
    /// <summary>
    /// Generates source code based on the provided options.
    /// </summary>
    /// <param name="opts">The options to configure code generation.</param>
    /// <returns>The generated source code as a formatted string.</returns>
    public string GenerateCode(TOptions opts)
    {
        // 1) Build the model
        CompilationUnitElement unitModel = BuildModel(opts);

        // 2) Visit to get a syntax tree
        SyntaxFactoryVisitor visitor = new();
        var syntaxNode = (CompilationUnitSyntax)unitModel.Accept(visitor);

        // 3) Format it
        CompilationUnitSyntax formatted = Format(syntaxNode);

        // 4) Render to string
        return formatted.ToFullString();
    }

    /// <summary>
    /// Asynchronously generates source code based on the provided options.
    /// </summary>
    /// <param name="opts">The options to configure code generation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the generated source code as a formatted string.</returns>
    public Task<string> GenerateCodeAsync(TOptions opts)
        => Task.FromResult(GenerateCode(opts));

    /// <summary>
    /// Must be implemented by each generator to produce the CompilationUnitElement.
    /// </summary>
    protected abstract CompilationUnitElement BuildModel(TOptions opts);

    /// <summary>
    /// Creates and configures the AdhocWorkspace for formatting.
    /// Override if you need custom MEF adapters, etc.
    /// </summary>
    protected virtual AdhocWorkspace CreateWorkspace()
        => new(MefHostServices.Create(MefHostServices.DefaultAssemblies));

    /// <summary>
    /// Tweak your formatting rules here (or pull from .editorconfig).
    /// </summary>
    protected virtual OptionSet ConfigureFormatting(OptionSet options)
        => options
            .WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, false)
            .WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, 4)
            .WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, 4)
            .WithChangedOption(FormattingOptions.SmartIndent, LanguageNames.CSharp, FormattingOptions.IndentStyle.Smart)
            .WithChangedOption(CSharpFormattingOptions.NewLineForMembersInObjectInit, true)
            .WithChangedOption(CSharpFormattingOptions.NewLineForMembersInAnonymousTypes, true);

    private CompilationUnitSyntax Format(CompilationUnitSyntax root)
    {
        using AdhocWorkspace workspace = CreateWorkspace();
        OptionSet opts = ConfigureFormatting(workspace.Options);
        CompilationUnitSyntax annotated = root.WithAdditionalAnnotations(Formatter.Annotation);
        return (CompilationUnitSyntax)Formatter.Format(annotated, workspace, opts);
    }
}
