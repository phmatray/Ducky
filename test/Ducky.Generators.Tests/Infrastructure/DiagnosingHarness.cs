using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Generators.Tests.Infrastructure;

// GeneratorHarness.Run for a shape the generator diagnoses: a generator exception (CS8785) still fails, and the caller
// asserts the reported diagnostics instead of requiring none.
internal static class DiagnosingHarness
{
    public static GeneratorDriver Run(
        IIncrementalGenerator generator, Compilation compilation, out Compilation output, out ImmutableArray<Diagnostic> diagnostics)
    {
        var driver = CSharpGeneratorDriver.Create(
                [generator.AsSourceGenerator()],
                parseOptions: GeneratorHarness.ParseOptions,
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true))
            .RunGeneratorsAndUpdateCompilation(compilation, out output, out diagnostics, TestContext.Current.CancellationToken);
        driver.GetRunResult().Results.Select(r => r.Exception?.ToString()).OfType<string>().ShouldBeEmpty();
        return driver;
    }

    // The real Ducky.Blazor.dll and the assemblies it references, added to a Ducky-only compilation (GEN-06: no stubs).
    public static Compilation WithBlazor(Compilation compilation)
    {
        var blazor = typeof(Ducky.Blazor.DuckyBlazorBuilderExtensions).Assembly;
        var names = blazor.GetReferencedAssemblies().Select(a => a.Name).Append(blazor.GetName().Name).ToHashSet();
        var present = compilation.References.OfType<PortableExecutableReference>().Select(r => r.FilePath).ToHashSet();
        return compilation.AddReferences(((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(p => names.Contains(Path.GetFileNameWithoutExtension(p)) && !present.Contains(p))
            .Select(p => MetadataReference.CreateFromFile(p)));
    }
}
