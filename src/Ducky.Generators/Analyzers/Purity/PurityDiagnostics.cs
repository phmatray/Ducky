using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Analyzers.Purity;

/// <summary>
/// The DUCKY001–003 descriptors (SPEC §16.2). Static readonly fields over constants, as in RegistrationDiagnostics:
/// release tracking needs the fields, Stryker doesn't mutate constants, and Descriptors_MatchCatalogue pins each value.
/// </summary>
internal static class PurityDiagnostics
{
    private const string Category = "Ducky";
    private const bool Enabled = true;

    private const string NonDeterministicId = "DUCKY001";
    private const string NonDeterministicTitle = "Non-deterministic call in a reducer or projector";
    private const string NonDeterministicMessage = "'{0}' is non-deterministic in a {1}: pass the value in the action or the selector argument instead";
    private const string NonDeterministicHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY001.md";

    private const string IoId = "DUCKY002";
    private const string IoTitle = "I/O or blocking in a reducer or projector";
    private const string IoMessage = "'{0}' does I/O or blocks in a {1}: move it to an effect";
    private const string IoHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY002.md";

    private const string MutationId = "DUCKY003";
    private const string MutationTitle = "Mutation in a reducer or projector";
    private const string MutationMessage = "'{0}' mutates {1} in a {2}: return a new value instead";
    private const string MutationHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY003.md";

    public static readonly DiagnosticDescriptor NonDeterministic = new(
        NonDeterministicId, NonDeterministicTitle, NonDeterministicMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: NonDeterministicHelp);

    public static readonly DiagnosticDescriptor Io = new(
        IoId, IoTitle, IoMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: IoHelp);

    public static readonly DiagnosticDescriptor Mutation = new(
        MutationId, MutationTitle, MutationMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: MutationHelp);
}
