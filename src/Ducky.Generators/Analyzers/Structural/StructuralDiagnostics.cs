using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Analyzers.Structural;

/// <summary>
/// The DUCKY005 and DUCKY011–015 descriptors (SPEC §16.2). Static readonly fields over constants, as in
/// RegistrationDiagnostics: release tracking needs the fields, Stryker doesn't mutate constants, and
/// Descriptors_MatchCatalogue pins each value.
/// </summary>
internal static class StructuralDiagnostics
{
    private const string Category = "Ducky";
    private const bool Enabled = true;

    private const string NonConcreteHandlerId = "DUCKY005";
    private const string NonConcreteHandlerTitle = "Handler for a non-concrete action type";
    private const string NonConcreteHandlerMessage = "'{0}' is abstract, an interface or object, so exact-type matching never runs this handler: use a concrete action type";
    private const string NonConcreteHandlerHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY005.md";

    private const string EmptyWithId = "DUCKY011";
    private const string EmptyWithTitle = "Reducer copies the state unchanged";
    private const string EmptyWithMessage = "'{0}' is a new reference, so the slice notifies on every action: return the state itself when nothing changes";
    private const string EmptyWithHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY011.md";

    private const string DerivedPersistKeyId = "DUCKY012";
    private const string DerivedPersistKeyTitle = "Persisted slice relies on its derived key";
    private const string DerivedPersistKeyMessage = "'{0}' is persisted under its derived Key: override Key so a rename doesn't orphan the stored data";
    private const string DerivedPersistKeyHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY012.md";

    private const string ConcurrencyKeyEqualityId = "DUCKY013";
    private const string ConcurrencyKeyEqualityTitle = "Concurrency key without value equality";
    private const string ConcurrencyKeyEqualityMessage = "ConcurrencyKey returns '{0}', which has no value equality, so no two runs share a key: return a record, a value type or a type that overrides Equals";
    private const string ConcurrencyKeyEqualityHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY013.md";

    private const string DuplicateHandlerId = "DUCKY014";
    private const string DuplicateHandlerTitle = "Duplicate handler for an action type";
    private const string DuplicateHandlerMessage = "On<{0}> is already registered in this constructor: register one handler per action type";
    private const string DuplicateHandlerHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY014.md";

    private const string SharedStateId = "DUCKY015";
    private const string SharedStateTitle = "Two slices share a state type";
    private const string SharedStateMessage = "'{0}' shares the state type '{1}' with '{2}': each state type maps to exactly one slice";
    private const string SharedStateHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY015.md";

    public static readonly DiagnosticDescriptor NonConcreteHandler = new(
        NonConcreteHandlerId, NonConcreteHandlerTitle, NonConcreteHandlerMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: NonConcreteHandlerHelp);

    public static readonly DiagnosticDescriptor EmptyWith = new(
        EmptyWithId, EmptyWithTitle, EmptyWithMessage, Category, DiagnosticSeverity.Info, Enabled, helpLinkUri: EmptyWithHelp);

    public static readonly DiagnosticDescriptor DerivedPersistKey = new(
        DerivedPersistKeyId, DerivedPersistKeyTitle, DerivedPersistKeyMessage, Category, DiagnosticSeverity.Info, Enabled, helpLinkUri: DerivedPersistKeyHelp);

    public static readonly DiagnosticDescriptor ConcurrencyKeyEquality = new(
        ConcurrencyKeyEqualityId, ConcurrencyKeyEqualityTitle, ConcurrencyKeyEqualityMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: ConcurrencyKeyEqualityHelp);

    public static readonly DiagnosticDescriptor DuplicateHandler = new(
        DuplicateHandlerId, DuplicateHandlerTitle, DuplicateHandlerMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: DuplicateHandlerHelp);

    // Reported at compilation end, once every slice is known (RS1037).
    public static readonly DiagnosticDescriptor SharedState = new(
        SharedStateId, SharedStateTitle, SharedStateMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: SharedStateHelp, customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly ImmutableArray<DiagnosticDescriptor> All =
        [NonConcreteHandler, EmptyWith, DerivedPersistKey, ConcurrencyKeyEquality, DuplicateHandler, SharedState];
}
