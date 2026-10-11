using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Generators.Dispatch;
using Ducky.Shared.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ducky.Generators;

/// <summary>GEN-01: emits the dispatch helpers of <c>[DuckyAction]</c> types (SPEC §16.1).</summary>
[Generator(LanguageNames.CSharp)]
public sealed class DispatchExtensionsGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var actions = context.SyntaxProvider
            .ForAttributeWithMetadataName("Ducky.DuckyActionAttribute", static (n, _) => n is TypeDeclarationSyntax, static (c, _) => ActionModel.Create(c))
            .WithTrackingName("DispatchActions");
        var reservedNames = context.CompilationProvider
            .Select(static (c, _) => ReservedNames(c))
            .WithTrackingName("DispatchReservedNames");
        var output = actions.Collect().Combine(reservedNames).WithTrackingName("DispatchOutput");
        context.RegisterSourceOutput(output, static (spc, input) => Emit(spc, input.Left, input.Right));
    }

    // DUCKY021: C# binds an accessible instance member before an extension method, so a helper named like a public
    // member of a receiver type (or of object) would never be called. IDispatcher comes with either interface list.
    private static ImmutableArray<string> Receivers => ["Ducky.IStore", "Ducky.EffectContext", "System.Object"];

    private static EquatableArray<string> ReservedNames(Compilation compilation) => new(
        Receivers
            .Select(compilation.GetTypeByMetadataName)
            .OfType<INamedTypeSymbol>()
            .SelectMany(t => t.AllInterfaces.Add(t))
            .SelectMany(t => t.GetMembers())
            .Where(m => m.DeclaredAccessibility == Accessibility.Public && m.CanBeReferencedByName)
            .Select(m => m.Name)
            .ToImmutableArray());

    private static void Emit(SourceProductionContext context, ImmutableArray<ActionModel> actions, EquatableArray<string> reservedNames)
    {
        var reserved = new HashSet<string>(reservedNames.Items);
        var sharedClass = actions.Where(a => a.Error is null)
            .GroupBy(a => (a.Namespace, a.ClassName))
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();
        var emitted = new List<ActionModel>();
        var hintNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase); // AddSource ignores case too
        foreach (var action in actions)
        {
            // Stryker disable once NullCoalescing : equivalent, sharedClass holds only actions whose Error is null
            var error = action.Error ?? (sharedClass.Contains(action) ? $"another [DuckyAction] type generates the class '{action.ClassName}'" : null);
            if (error is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(DispatchDiagnostics.InvalidAction, action.Location.ToLocation(), action.DisplayName, error));
                continue;
            }

            if (reserved.Contains(action.MethodName) || reserved.Contains(action.MethodName + "Async"))
            {
                context.ReportDiagnostic(Diagnostic.Create(DispatchDiagnostics.NameCollision, action.Location.ToLocation(), action.DisplayName, action.MethodName));
                continue;
            }

            // Each action emits M and MAsync, so Load's async helper can clash with LoadAsync's sync one.
            foreach (var other in emitted.Where(o => o.Helpers.Items.Any(h => action.Helpers.Items.Any(x => x.Signature == h.Signature))))
            {
                var name = HelperNames(action).Intersect(HelperNames(other)).FirstOrDefault();
                if (name is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(DispatchDiagnostics.AmbiguousHelpers, action.Location.ToLocation(), name, action.DisplayName, other.DisplayName));
                }
            }

            emitted.Add(action);
            var baseName = IdentifierSanitizer.HintName(action.DisplayName);
            var hintName = baseName;
            for (var i = 2; !hintNames.Add(hintName); i++)
            {
                hintName = $"{baseName}_{i}";
            }

            context.AddSource(hintName + ".DispatchExtensions.g.cs", Source(action));
        }
    }

    private static string[] HelperNames(ActionModel action) => [action.MethodName, action.MethodName + "Async"];

    private static string Source(ActionModel action)
    {
        var type = action.TypeName;
        var name = IdentifierSanitizer.EscapeKeyword(action.MethodName);
        var asyncName = IdentifierSanitizer.EscapeKeyword(action.MethodName + "Async");
        var ids = string.Join(", ", action.DiagnosticIds.Items);
        var hasIds = action.DiagnosticIds.Items.Length > 0;
        var helpers = action.Helpers.Items.Select(h => $$"""
            /// <summary>Dispatches a new <see cref="{{type}}"/>.</summary>
            {{Access(h.IsPublic)}} static void {{name}}(this global::Ducky.IDispatcher __dispatcher{{h.Parameters}}) =>
                __dispatcher.Dispatch(new {{type}}({{h.Arguments}}));

            /// <summary>Dispatches a new <see cref="{{type}}"/> and returns how the store handled it.</summary>
            {{Access(h.IsPublic)}} static global::System.Threading.Tasks.Task<global::Ducky.DispatchResult> {{asyncName}}(this global::Ducky.IDispatcher __dispatcher{{h.Parameters}}) =>
                __dispatcher.DispatchAsync(new {{type}}({{h.Arguments}}));
            """);
        return new CodeWriter()
            .Line("#pragma warning disable CS0612, CS0618 // justification: generated code references user [Obsolete] members (GEN-06)"
                + (hasIds ? $"\n#pragma warning disable {ids} // justification: generated code references user [Obsolete] or [Experimental] members with their own diagnostic id (GEN-06)" : ""))
            .Line(action.Namespace is null ? "" : $"namespace {action.Namespace};\n")
            .Line($"/// <summary>Dispatch helpers for <see cref=\"{type}\"/>, generated from its <c>[DuckyAction]</c>.</summary>")
            .Open($"{Access(action.Helpers.Items.Any(h => h.IsPublic))} static class {action.ClassName}")
            .Line(string.Join("\n\n", helpers))
            .Close()
            .Line("#pragma warning restore CS0612, CS0618" + (hasIds ? $"\n#pragma warning restore {ids}" : ""))
            .ToString();
    }

    private static string Access(bool isPublic) => isPublic ? "public" : "internal";
}
