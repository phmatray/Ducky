using Microsoft.CodeAnalysis;

namespace TheBlazorState.Generators;

internal static class DiagnosticDescriptors
{
    private const string Category = "TheBlazorState";

    public static readonly DiagnosticDescriptor NonPartialProperty = new(
        "TBS001",
        "[Persist] requires partial property",
        "Property '{0}' has [{1}] but is not declared as partial",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NonPartialClassForPersist = new(
        "TBS002",
        "[Persist] requires partial class",
        "Property '{0}' has [{1}] but class '{2}' is not partial",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidPersistTimeToLive = new(
        "TBS004",
        "Invalid TimeToLive format",
        "TimeToLive '{0}' on property '{1}' is not a valid TimeSpan string",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
}
