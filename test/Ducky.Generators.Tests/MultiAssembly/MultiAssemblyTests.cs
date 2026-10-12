using System.Collections.Immutable;
using Ducky.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Generators.Tests.MultiAssembly;

// SPEC §16.3 (INV-25): three compilations with warnings as errors. Lib (MyCompany.Todos) and App run both Ducky
// generators and declare `[DuckyAction] record Increment` in different namespaces; Tests references both images
// without the generator and calls both registrations and both Increment helpers.
public sealed class MultiAssemblyTests
{
    private const string Undocumented = "#pragma warning disable CS1591 // justification: fixture sources are undocumented; only generated code is checked\n";

    private const string Lib = Undocumented + """
        namespace MyCompany.Todos;
        public sealed record Todos(int Count);
        public sealed class TodosSlice : Ducky.Slice<Todos> { protected override Todos Initial => new(0); }
        [Ducky.DuckyAction] public sealed record Increment(int By = 1);
        """;

    // App.Extra.Increment repeats App.Counter.Increment's helper signature in the same compilation: DUCKY024.
    private const string App = Undocumented + """
        namespace App.Counter
        {
            public sealed record Counter(int Value);
            public sealed class CounterSlice : Ducky.Slice<Counter> { protected override Counter Initial => new(0); }
            [Ducky.DuckyAction] public sealed record Increment(int By = 1);
        }
        namespace App.Extra
        {
            [Ducky.DuckyAction] public sealed record Increment(int Step);
        }
        """;

    private const string Tests = Undocumented + """
        using Ducky.Generated;

        namespace Tests
        {
            public static class Calls
            {
                public static void Register(Ducky.DuckyBuilder b)
                {
                    b.AddDuckyGenerated_MyCompany_Todos().AddDuckyGenerated_App();
                    Ducky.Generated.App_DuckyRegistrations.AddDuckyGenerated_App(b);
                }

                public static void Dispatch(Ducky.IDispatcher d)
                {
                    global::MyCompany.Todos.IncrementDispatchExtensions.Increment(d);
                    global::App.Counter.IncrementDispatchExtensions.Increment(d, 2);
                }
            }
        }
        """;

    [Fact]
    public void MultiAssembly_NoAmbiguityOrConflict()
    {
        var lib = Generate(Strict("MyCompany.Todos", Lib), out var libDiagnostics);
        libDiagnostics.ShouldBeEmpty();
        lib.ShouldCompileClean();

        var app = Generate(Strict("App", App).AddReferences(Image(lib)), out var appDiagnostics);
        var ducky024 = appDiagnostics.ShouldHaveSingleItem();
        ducky024.Id.ShouldBe("DUCKY024");
        ducky024.Location.SourceSpan.ShouldBe(app.GetTypeByMetadataName("App.Extra.Increment")!.Locations.Single().SourceSpan);
        app.ShouldCompileClean();

        var tests = Strict("Tests", Tests).AddReferences(Image(lib), Image(app));
        tests.ShouldCompileClean(); // no CS0121, CS0433 or CS0436, nor any other diagnostic
    }

    // TreatWarningsAsErrors on top of the GEN-06 consumer settings.
    private static CSharpCompilation Strict(string assemblyName, string source)
    {
        var compilation = GeneratorHarness.Compile(source).WithAssemblyName(assemblyName);
        return compilation.WithOptions(compilation.Options.WithGeneralDiagnosticOption(ReportDiagnostic.Error));
    }

    // Both Ducky generators in one driver; a generator exception (CS8785) fails.
    private static Compilation Generate(Compilation compilation, out ImmutableArray<Diagnostic> diagnostics)
    {
        var driver = CSharpGeneratorDriver.Create(
                [new RegistrationGenerator().AsSourceGenerator(), new DispatchExtensionsGenerator().AsSourceGenerator()],
                parseOptions: GeneratorHarness.ParseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out diagnostics, TestContext.Current.CancellationToken);
        driver.GetRunResult().Results.Select(r => r.Exception?.ToString()).OfType<string>().ShouldBeEmpty();
        return output;
    }

    // An emitted image, as a consumer references a built assembly.
    private static PortableExecutableReference Image(Compilation compilation)
    {
        using var image = new MemoryStream();
        compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        return MetadataReference.CreateFromImage(image.ToArray());
    }
}
