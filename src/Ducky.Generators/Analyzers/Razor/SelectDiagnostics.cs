using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Analyzers.Razor;

/// <summary>
/// The DUCKY009 descriptor (SPEC §16.2). Static readonly fields over constants, as in RegistrationDiagnostics: release
/// tracking needs the field, Stryker doesn't mutate constants, and <c>Descriptors_MatchCatalogue</c> pins each value.
/// </summary>
internal static class SelectDiagnostics
{
    private const string Category = "Ducky";
    private const bool Enabled = true;

    private const string SelectAfterFirstRenderId = "DUCKY009";
    private const string SelectAfterFirstRenderTitle = "Select after the first render";
    private const string SelectAfterFirstRenderMessage =
        "'Select' in '{0}' runs after the component's first render, when registration is closed (DUCKY352): call it in OnInitialized and keep the selection in a field";
    private const string SelectAfterFirstRenderHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY009.md";

    public static readonly DiagnosticDescriptor SelectAfterFirstRender = new(
        SelectAfterFirstRenderId, SelectAfterFirstRenderTitle, SelectAfterFirstRenderMessage, Category, DiagnosticSeverity.Warning, Enabled,
        helpLinkUri: SelectAfterFirstRenderHelp);

    public static readonly ImmutableArray<DiagnosticDescriptor> Supported = [SelectAfterFirstRender];
}
