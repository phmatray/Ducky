using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Dispatch;

/// <summary>
/// The GEN-01 descriptors (SPEC §16.2). Static readonly fields, as release tracking requires (a property-created
/// descriptor is RS2002). Their initializers run once per test host, before Stryker activates any mutant, so every
/// literal is a constant, which Stryker doesn't mutate; <c>Descriptors_MatchCatalogue</c> pins each value.
/// </summary>
internal static class DispatchDiagnostics
{
    private const string Category = "Ducky";
    private const bool Enabled = true;

    private const string InvalidId = "DUCKY020";
    private const string InvalidTitle = "Invalid [DuckyAction] type";
    private const string InvalidMessage = "'{0}' gets no dispatch helper: {1}";
    private const string InvalidHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY020.md";

    private const string CollisionId = "DUCKY021";
    private const string CollisionTitle = "Dispatch helper name collides with a store member";
    private const string CollisionMessage = "The dispatch helpers of '{0}' are skipped: '{1}' or '{1}Async' is a member of IDispatcher, IStore, EffectContext or object, which C# binds before an extension method; set MethodName";
    private const string CollisionHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY021.md";

    private const string AmbiguousId = "DUCKY024";
    private const string AmbiguousTitle = "Ambiguous dispatch helpers";
    private const string AmbiguousMessage = "The dispatch helper '{0}' of '{1}' has the same name and parameter types as the one of '{2}': a call that sees both is ambiguous (CS0121); set MethodName on one of them";
    private const string AmbiguousHelp = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY024.md";

    public static readonly DiagnosticDescriptor InvalidAction = new(
        InvalidId, InvalidTitle, InvalidMessage, Category, DiagnosticSeverity.Error, Enabled, helpLinkUri: InvalidHelp);

    public static readonly DiagnosticDescriptor NameCollision = new(
        CollisionId, CollisionTitle, CollisionMessage, Category, DiagnosticSeverity.Warning, Enabled, helpLinkUri: CollisionHelp);

    public static readonly DiagnosticDescriptor AmbiguousHelpers = new(
        AmbiguousId, AmbiguousTitle, AmbiguousMessage, Category, DiagnosticSeverity.Info, Enabled, helpLinkUri: AmbiguousHelp);

    public static readonly ImmutableArray<DiagnosticDescriptor> All = [InvalidAction, NameCollision, AmbiguousHelpers];
}
