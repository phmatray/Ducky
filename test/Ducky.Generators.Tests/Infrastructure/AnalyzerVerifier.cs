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

    public static CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> Test(string source)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = new ReferenceAssemblies("net10.0"),
        };
        test.TestState.AdditionalReferences.AddRange(_references);
        return test;
    }
}
