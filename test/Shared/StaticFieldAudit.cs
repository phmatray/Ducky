using System.CodeDom.Compiler;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ducky.TestSupport;

/// <summary>
/// The static-state check behind <c>NoStaticMutableFields</c> (SPEC §17.5, INV-22): lists every static field of an
/// assembly that is not readonly (outside a [CompilerGenerated] type) or is readonly outside the allow-list.
/// </summary>
internal static class StaticFieldAudit
{
    private const BindingFlags DeclaredStatics =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    // Named immutable framework types allowed as static readonly fields, matched by name so no audited assembly's
    // references are needed here.
    private static readonly HashSet<string> _allowedTypeNames =
    [
        "System.Object", // sentinels (NullKey, Unset)
        "System.String",
        "System.Diagnostics.ActivitySource",
        "Microsoft.Extensions.Logging.EventId",
        "Microsoft.CodeAnalysis.DiagnosticDescriptor",
        "Microsoft.CodeAnalysis.SymbolDisplayFormat",
        "Microsoft.CodeAnalysis.LocalizableString",
        "System.Text.Json.JsonEncodedText",
        "System.Buffers.SearchValues`1",
        "System.Text.CompositeFormat",
        "Ducky.EntityState`2", // immutable types owned by the library
    ];

    private static readonly HashSet<string> _immutableCollectionNamespaces =
        ["System.Collections.Frozen", "System.Collections.Immutable"];

    public static IReadOnlyList<string> Violations(Assembly assembly) =>
    [
        .. from type in assembly.GetTypes()
           where !InCompilerGeneratedType(type) && !IsCoverageTracker(type) && !IsStrykerHelper(type)
           from field in type.GetFields(DeclaredStatics)
           where !field.IsLiteral && (!field.IsInitOnly || !IsAllowedReadonly(field))
           select $"{type.FullName}.{field.Name}",
    ];

    private static bool InCompilerGeneratedType(Type? type) =>
        type is not null && (type.IsDefined(typeof(CompilerGeneratedAttribute)) || InCompilerGeneratedType(type.DeclaringType));

    // [CompilerGenerated] counts on the declaring type only: on a field it also marks auto-property backing fields,
    // so `static List<int> Cache { get; } = [];` would slip through (INV-22).
    private static bool IsAllowedReadonly(FieldInfo field) =>
        field.IsDefined(typeof(GeneratedCodeAttribute)) || IsGenerated(field.DeclaringType!)
        || IsBlazorWireContext(field.DeclaringType!)
        || typeof(Delegate).IsAssignableFrom(field.FieldType)
        || IsAllowedType(field.FieldType);

    private static bool IsGenerated(MemberInfo member) =>
        member.IsDefined(typeof(GeneratedCodeAttribute)) || member.IsDefined(typeof(CompilerGeneratedAttribute));

    // Ducky.Blazor's internal wire context: the one accepted static cache (fixed wire types, no user or circuit data).
    private static bool IsBlazorWireContext(Type type) =>
        type.Name == "BlazorWireContext" && type.Assembly.GetName().Name == "Ducky.Blazor";

    private static bool IsAllowedType(Type type)
    {
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return _allowedTypeNames.Contains(definition.FullName ?? definition.Name)
            || (!definition.IsNested && !definition.IsInterface && _immutableCollectionNamespaces.Contains(definition.Namespace ?? ""));
    }

    // §17.5: the tracker type that MTP CodeCoverage's static instrumentation injects into the audited assembly during
    // the Test target's coverage run (tool state, not library state). Namespace and name prefix must both match.
    private static bool IsCoverageTracker(Type type) =>
        type.Namespace == "Microsoft.CodeCoverage.Instrumentation.Static.Tracker"
        && type.Name.StartsWith("StaticManagedTrackerTemplate_", StringComparison.Ordinal);

    // §17.5: the helper types Stryker.NET injects into the audited assembly during a mutation run (tool state, not
    // library state), in a per-run namespace "Stryker" + random letters and digits (spike S-6). Namespace shape and
    // name must both match.
    private static bool IsStrykerHelper(Type type) =>
        type.Namespace is { Length: > 7 } ns && ns.StartsWith("Stryker", StringComparison.Ordinal) && ns[7..].All(char.IsAsciiLetterOrDigit)
        && type.Name is "MutantControl" or "MutantContext";
}
