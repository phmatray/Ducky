using System.Collections.Immutable;
using Ducky.Generators.Analyzers.Usage;
using Ducky.Generators.Registration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ducky.Generators;

/// <summary>
/// DUCKY008 (SPEC §6.6, §16.2): an effect, effect-group or middleware constructor that injects <c>IStore</c> or
/// <c>IDispatcher</c>. Effects and middleware are built while the store materializes, so a constructor that uses the
/// store re-enters materialization; effects dispatch through their <c>EffectContext</c>, middleware use <c>Store</c>.
/// Reported at each such parameter, primary constructors included.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StoreInjectionAnalyzer : DiagnosticAnalyzer
{
    private const string EffectFix = "dispatch through the EffectContext (ctx.Dispatch) instead";
    private const string MiddlewareFix = "use the Store property instead";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UsageDiagnostics.StoreInjected];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // Stryker disable once Statement : equivalent, concurrent execution is a scheduling hint with no observable result
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // Unresolved without Ducky: a null base type is never derived from and a null type never matches.
            var compilation = start.Compilation;
            var effect = compilation.GetTypeByMetadataName("Ducky.Effect");
            var middleware = compilation.GetTypeByMetadataName("Ducky.Middleware");
            ImmutableArray<INamedTypeSymbol?> injected = [compilation.GetTypeByMetadataName("Ducky.IStore"), compilation.GetTypeByMetadataName("Ducky.IDispatcher")];
            start.RegisterSymbolAction(c => Analyze(c, effect, middleware, injected), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol? effect, INamedTypeSymbol? middleware, ImmutableArray<INamedTypeSymbol?> injected)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var fix = RegistrationModel.Derives(type, effect) ? EffectFix : RegistrationModel.Derives(type, middleware) ? MiddlewareFix : null;
        if (fix is null)
        {
            return;
        }

        foreach (var constructor in type.InstanceConstructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                if (injected.Contains(parameter.Type, SymbolEqualityComparer.Default))
                {
                    context.ReportDiagnostic(Diagnostic.Create(UsageDiagnostics.StoreInjected, parameter.Locations[0], type.Name, parameter.Type.Name, fix));
                }
            }
        }
    }
}
