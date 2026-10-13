using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Ducky.Draft.Generators.Tests.Infrastructure;

// Microsoft.CodeAnalysis.CSharp.Analyzer.Testing verifier (SPEC §16.2) over the harness references: the running
// framework and the real Ducky.Draft.dll (GEN-06: no stubs). The empty ReferenceAssemblies stops the verifier from
// downloading a reference pack from NuGet at test time. The Draft generator runs in the driver, as in a consumer build;
// what it emits is its own tests' business, so only diagnostics are compared, compiler warnings included, under the
// harness's options (nullable on, warning level 9999), so a recipe shape that does not compile clean fails too.
internal static class AnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    private static readonly ImmutableArray<MetadataReference> _references = [.. GeneratorHarness.Compile().References];

    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor) => new(descriptor);

    public static Task VerifyAsync(string source, params DiagnosticResult[] expected) => VerifyAsync(Create(source), expected);

    public static Task VerifyAsync(Test test, params DiagnosticResult[] expected)
    {
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    public static Test Create(string source)
    {
        var test = new Test
        {
            TestCode = source,
            ReferenceAssemblies = new ReferenceAssemblies("net10.0"),
            CompilerDiagnostics = CompilerDiagnostics.Warnings,
            // Generator output is not compared; and the rule reports in generated code on purpose (Razor @code blocks).
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck | TestBehaviors.SkipGeneratedCodeCheck,
        };
        test.TestState.AdditionalReferences.AddRange(_references);
        test.SolutionTransforms.Add((solution, id) =>
        {
            var options = (CSharpCompilationOptions)solution.GetProject(id)!.CompilationOptions!;
            return solution
                .WithProjectCompilationOptions(id, options.WithNullableContextOptions(NullableContextOptions.Enable).WithWarningLevel(9999))
                .WithProjectParseOptions(id, GeneratorHarness.ParseOptions);
        });
        return test;
    }

    internal sealed class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    {
        protected override IEnumerable<Type> GetSourceGenerators() => [typeof(DraftGenerator)];
    }
}
