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
    private const string UnresolvedMessage = "{0} on '{1}' is ignored: {2}";
    private const string UnresolvedHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY022.md";

    private const string ExcludedId = "DUCKY023";
    private const string ExcludedTitle = "Type excluded from generated registration";
    private const string ExcludedMessage = "'{0}' is not registered by AddDuckyGenerated: {1}";
    private const string ExcludedHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY023.md";

    private const string NotUnionId = "DUCKY026";
    private const string NotUnionTitle = "Union-first action type is not a union";
    private const string NotUnionMessage = "'{0}' derives from {1}, but its action type '{2}' is not a C# union";
    private const string NotUnionHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY026.md";

    private const string NotPartialId = "DUCKY027";
    private const string NotPartialTitle = "Union-first type is not partial";
    private const string NotPartialMessage = "The cases of '{0}' can't be registered: declare {1} partial";
    private const string NotPartialHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY027.md";

    private const string UndispatchableId = "DUCKY029";
    private const string UndispatchableTitle = "Union case can never be dispatched";
    private const string UndispatchableMessage = "The cases of '{0}' can't be registered: dispatch matches an action's exact runtime type, which is never {1}";
    private const string UndispatchableHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY029.md";

    private const string FileLocalId = "DUCKY030";
    private const string FileLocalTitle = "Union-first type is file-local";
    private const string FileLocalMessage = "The cases of '{0}' can't be registered: '{1}' is file-local, so no generated declaration can extend it";
    private const string FileLocalHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY030.md";

    private const string HandWrittenId = "DUCKY031";
    private const string HandWrittenTitle = "Hand-written RegisterCases on a generated union-first type";
    private const string HandWrittenMessage = "'{0}' overrides RegisterCases, which the Ducky generator writes for it: remove the override";
    private const string HandWrittenHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY031.md";

    public static readonly DiagnosticDescriptor Unresolved = new(
        UnresolvedId, UnresolvedTitle, UnresolvedMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: UnresolvedHelp);

    public static readonly DiagnosticDescriptor Excluded = new(
        ExcludedId, ExcludedTitle, ExcludedMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: ExcludedHelp);

    public static readonly DiagnosticDescriptor NotUnion = new(
        NotUnionId, NotUnionTitle, NotUnionMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: NotUnionHelp);

    public static readonly DiagnosticDescriptor NotPartial = new(
        NotPartialId, NotPartialTitle, NotPartialMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: NotPartialHelp);

    public static readonly DiagnosticDescriptor Undispatchable = new(
        UndispatchableId, UndispatchableTitle, UndispatchableMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: UndispatchableHelp);

    public static readonly DiagnosticDescriptor FileLocal = new(
        FileLocalId, FileLocalTitle, FileLocalMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: FileLocalHelp);

    public static readonly DiagnosticDescriptor HandWritten = new(
        HandWrittenId, HandWrittenTitle, HandWrittenMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: HandWrittenHelp);

    public static readonly ImmutableArray<DiagnosticDescriptor> All = [Unresolved, Excluded, NotUnion, NotPartial, Undispatchable, FileLocal, HandWritten];
}
