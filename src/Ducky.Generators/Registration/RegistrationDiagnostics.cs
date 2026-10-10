using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Registration;

/// <summary>
/// The GEN-02 descriptors (SPEC §16.2). Static readonly fields, as release tracking requires (a property-created
/// descriptor is RS2002). Their initializers run once per test host, before Stryker activates any mutant, so every
/// literal is a constant, which Stryker doesn't mutate; <c>Descriptors_MatchCatalogue</c> pins each value.
/// </summary>
internal static class RegistrationDiagnostics
{
    private const string Category = "Ducky";
    private const bool Enabled = true;

    private const string UnresolvedId = "DUCKY022";
    private const string UnresolvedTitle = "Registration call not emitted";
    private const string UnresolvedMessage = "[{0}] on '{1}' is ignored: {2}";
    private const string UnresolvedHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY022.md";

    private const string ExcludedId = "DUCKY023";
    private const string ExcludedTitle = "Type excluded from generated registration";
    private const string ExcludedMessage = "'{0}' is not registered by AddDuckyGenerated: {1}";
    private const string ExcludedHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY023.md";

    public static readonly DiagnosticDescriptor Unresolved = new(
        UnresolvedId, UnresolvedTitle, UnresolvedMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: UnresolvedHelp);

    public static readonly DiagnosticDescriptor Excluded = new(
        ExcludedId, ExcludedTitle, ExcludedMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: ExcludedHelp);

    public static readonly ImmutableArray<DiagnosticDescriptor> All = [Unresolved, Excluded];
}
