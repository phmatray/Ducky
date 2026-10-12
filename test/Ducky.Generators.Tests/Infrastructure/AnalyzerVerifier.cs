using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Ducky.Generators.Tests.Infrastructure;

// Microsoft.CodeAnalysis.CSharp.Analyzer.Testing verifier (SPEC §16.2) over the harness references: the running
// framework, the real Ducky.dll and the real Ducky.Blazor.dll with what it references (GEN-06: no stubs). The empty
// ReferenceAssemblies stops the verifier from downloading a reference pack from NuGet at test time.
internal static class AnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    private static readonly ImmutableArray<MetadataReference> _references =
        [.. DiagnosingHarness.WithBlazor(GeneratorHarness.Compile()).References];

    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor) => new(descriptor);

    public static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        var test = Test(source);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    // For a rule that reads another generator's output (DUCKY006, DUCKY016, DUCKY025): the real generator runs in the
    // driver; what it emits is its own tests' business, so only the diagnostics are compared.
    public static Task VerifyWithGeneratorAsync<TGenerator>(string source, params DiagnosticResult[] expected)
    {
        var test = Test<GeneratingTest<TGenerator>>(source);
        test.TestBehaviors |= TestBehaviors.SkipGeneratedSourcesCheck;
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    public static CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> Test(string source) => Test<CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>>(source);

    // A derived test type, e.g. one that adds source generators (RazorAnalyzerTest).
    public static TTest Test<TTest>(string source)
        where TTest : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>, new()
    {
        var test = new TTest
        {
            TestCode = source,
            ReferenceAssemblies = new ReferenceAssemblies("net10.0"),
        };
        test.TestState.AdditionalReferences.AddRange(_references);
        return test;
    }

    internal sealed class GeneratingTest<TGenerator> : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    {
        protected override IEnumerable<Type> GetSourceGenerators() => [typeof(TGenerator)];
    }
}
