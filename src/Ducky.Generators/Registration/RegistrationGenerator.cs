using System;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Generators.Registration;
using Ducky.Shared.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ducky.Generators;

/// <summary>GEN-02: emits <c>AddDuckyGenerated_{Asm}</c>, which registers the assembly's slices and effects (SPEC §16.1).</summary>
[Generator(LanguageNames.CSharp)]
public sealed class RegistrationGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is ClassDeclarationSyntax { BaseList: not null }, RegistrationModel.From)
            .WithTrackingName("RegistrationTypes");
        var gates = context.CompilationProvider
            .Select(static (compilation, _) => RegistrationGates.From(compilation))
            .WithTrackingName("RegistrationGates");

        // Emit drops the Location (a tree reference), so an edit to a slice's file that leaves its registration as it was
        // doesn't regenerate it.
        var registered = types
            .Select(static (model, _) => model is { Excluded: null } ? model with { Location = Location.None } : null)
            .WithTrackingName("RegistrationEmit");
        context.RegisterSourceOutput(registered.Collect().Combine(gates), static (spc, input) => Emit(spc, input.Left, input.Right));

        var attributes = Attributed(context, "Persist").Collect().Combine(Attributed(context, "Prerender").Collect());
        context.RegisterSourceOutput(
            types.Collect().Combine(attributes).Combine(gates),
            static (spc, input) => Report(spc, input.Left.Left, input.Left.Right.Left.AddRange(input.Left.Right.Right), input.Right));
    }

    // Only type declarations (INV-25): FAWMN ignores AttributeUsage, so an enum, delegate, method or field carrying the
    // attribute (already CS0592) would otherwise reach AttributeModel.From's casts and crash the generator (CS8785).
    private static IncrementalValuesProvider<AttributeModel> Attributed(IncrementalGeneratorInitializationContext context, string attribute) =>
        context.SyntaxProvider
            .ForAttributeWithMetadataName($"Ducky.{attribute}Attribute", static (node, _) => node is TypeDeclarationSyntax, (c, _) => AttributeModel.From(c, attribute))
            .WithTrackingName($"Registration{attribute}");

    // Middleware is never registered: its order matters (MW-01). A partial class with several base lists yields one
    // model per declaration, hence Distinct.
    private static void Emit(SourceProductionContext context, ImmutableArray<RegistrationModel?> models, RegistrationGates gates)
    {
        var registered = models.OfType<RegistrationModel>().Distinct().ToList();
        var suppressed = string.Join(", ", registered.SelectMany(m => m.Suppressed.Items).Distinct().OrderBy(id => id, StringComparer.Ordinal));
        var writer = new CodeWriter()
            .Line("namespace Ducky.Generated;")
            .Line()
            .Line("/// <summary>The slices and effects of this assembly, registered by the Ducky generator.</summary>")
            .Open($"public static class {gates.Assembly}_DuckyRegistrations")
            .Line("""
                /// <summary>Registers every slice and effect of this assembly (never middleware, whose order matters).</summary>
                /// <param name="b">The Ducky builder.</param>
                /// <returns>The same builder.</returns>
                """)
            .Open($"public static global::Ducky.DuckyBuilder AddDuckyGenerated_{gates.Assembly}(this global::Ducky.DuckyBuilder b)")
            .Line("#pragma warning disable CS0612, CS0618 // justification: generated code references user [Obsolete] members (GEN-06)");
        if (suppressed.Length > 0)
        {
            writer.Line($"#pragma warning disable {suppressed} // justification: generated code references user [Obsolete]/[Experimental] members (GEN-06)");
        }

        foreach (var model in registered)
        {
            writer.Line(model.IsSlice ? $"b.AddSlice<{model.Type}>();" : $"b.AddEffect<{model.Type}>();");
        }

        // After every AddSlice (Prerender<T> on a slice not added is DUCKY310), only while the exact method resolves.
        foreach (var model in registered.Where(m => m.Prerender && gates.Prerender))
        {
            writer.Line($"global::Ducky.Blazor.DuckyBlazorBuilderExtensions.Prerender<{model.Type}>(b);");
        }

        if (suppressed.Length > 0)
        {
            writer.Line($"#pragma warning restore {suppressed}");
        }

        writer.Line("#pragma warning restore CS0612, CS0618").Line("return b;").Close().Close();
        context.AddSource("DuckyRegistrations.g.cs", writer.ToString());
    }

    // DUCKY023 for each excluded type; DUCKY022 for [Persist]/[Prerender] on a non-slice, or on any slice (registered or
    // not) while its gated method doesn't resolve. Emit writes Prerender<T> for a resolved [Prerender] (M13-04); the
    // PersistAttributeDefaults<T> arm ships with its API (M13-04b): until then a resolved [Persist] emits nothing.
    private static void Report(SourceProductionContext context, ImmutableArray<RegistrationModel?> models, ImmutableArray<AttributeModel> attributes, RegistrationGates gates)
    {
        foreach (var model in models.OfType<RegistrationModel>().Distinct().Where(m => m.Excluded is not null))
        {
            context.ReportDiagnostic(Diagnostic.Create(RegistrationDiagnostics.Excluded, model.Location, model.Display, model.Excluded));
        }

        foreach (var attribute in attributes)
        {
            var persist = attribute.Attribute == "Persist";
            var reason = !attribute.IsSlice ? "it is not a slice"
                : (persist ? gates.Persist : gates.Prerender) ? null
                : $"'{(persist ? RegistrationGates.PersistMethod : RegistrationGates.PrerenderMethod)}' does not resolve (reference a Ducky.Blazor version that declares it)";
            if (reason is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(RegistrationDiagnostics.Unresolved, attribute.Location, attribute.Attribute, attribute.Display, reason));
            }
        }
    }
}
