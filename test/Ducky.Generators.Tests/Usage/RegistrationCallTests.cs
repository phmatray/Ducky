using Ducky.Generators.Analyzers.Usage;
using Ducky.Generators.Registration;
using Ducky.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.RegistrationCallAnalyzer>;

namespace Ducky.Generators.Tests.Usage;

// DUCKY016 (SPEC §16.2, ADR-0017): an assembly that defines slices or effects and calls AddDucky but never its own
// AddDuckyGenerated_{Asm}, reported at each AddDucky call. The real RegistrationGenerator runs in the driver, so the
// call that silences the rule binds to the generated method; the verifier's assembly is "TestProject".
public sealed class RegistrationCallTests
{
    private static string Source(string types, string configure) => $$"""
        using System.Threading;
        using System.Threading.Tasks;
        using Ducky;
        using Ducky.Generated;
        using Microsoft.Extensions.DependencyInjection;

        public sealed record Count(int Value);
        public sealed record Ping;
        {{types}}

        public static class Startup
        {
            public static void Configure(IServiceCollection services) { {{configure}}; }
        }

        public static class Other
        {
            public static void AddDucky(this IServiceCollection services) { }
        }
        """;

    private const string Slice = "public sealed class CountSlice : Slice<Count> { protected override Count Initial => new(0); }";

    private const string Effect = """
        public sealed class PingEffect : Effect<Ping>
        {
            public override Task Handle(Ping action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        }
        """;

    // Stage 16: a reactive effect is registered by the generated method too (AddReactiveEffect<T>).
    private const string Reactive = """
        public sealed class Echo : Ducky.Reactive.ReactiveEffect
        {
            public override R3.Observable<object> Handle(R3.Observable<object> actions, R3.Observable<StateSnapshot> state) => actions;
        }
        """;

    public static TheoryData<string, string> Reported => new()
    {
        { Reactive, "{|#0:services.AddDucky(b => { })|}" },
        { Slice, "{|#0:services.AddDucky(b => { })|}" },
        { Effect, "{|#0:services.AddDucky(b => b.AddEffect<PingEffect>())|}" },
        { Slice + Effect, "{|#0:services.AddDucky(b => b.AddSlice<CountSlice>())|}" },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky016_GeneratedRegistrationNotCalled_Reported(string types, string configure) =>
        Verifier.VerifyWithGeneratorAsync<RegistrationGenerator>(
            Source(types, configure),
            Verifier.Diagnostic(UsageDiagnostics.RegistrationNotCalled).WithLocation(0).WithArguments("TestProject"));

    [Fact]
    public Task Ducky016_EveryAddDuckyCall_Reported() =>
        Verifier.VerifyWithGeneratorAsync<RegistrationGenerator>(
            Source(Slice, "{|#0:services.AddDucky(b => { })|}; {|#1:services.AddDucky(b => { })|}"),
            Verifier.Diagnostic(UsageDiagnostics.RegistrationNotCalled).WithLocation(0).WithArguments("TestProject"),
            Verifier.Diagnostic(UsageDiagnostics.RegistrationNotCalled).WithLocation(1).WithArguments("TestProject"));

    public static TheoryData<string, string> NotReported => new()
    {
        // The generated registration is called, as an extension or as a static method.
        { Slice + Effect, "services.AddDucky(b => b.AddDuckyGenerated_TestProject())" },
        { Slice, "services.AddDucky(b => TestProject_DuckyRegistrations.AddDuckyGenerated_TestProject(b))" },
        { Reactive, "services.AddDucky(b => b.AddDuckyGenerated_TestProject())" },

        // No slice or effect to register: none, an abstract one, or only middleware, which is never generated.
        { "", "services.AddDucky(b => { })" },
        { "public abstract class BaseSlice : Slice<Count> { }", "services.AddDucky(b => { })" },
        { "public sealed class Logging : Middleware { }", "services.AddDucky(b => b.Use<Logging>())" },

        // Slices and effects the generated method can't register either: private nested, generic, or an effect without
        // a public constructor.
        { "public static class Fixture { private sealed class FixtureSlice : Slice<Count> { protected override Count Initial => new(0); } }", "services.AddDucky(b => { })" },
        { "public sealed class BoxSlice<T> : Slice<Count> { protected override Count Initial => new(0); }", "services.AddDucky(b => { })" },
        { Effect.Replace("public override", "internal PingEffect() { } public override", StringComparison.Ordinal), "services.AddDucky(b => { })" },

        // Slices without an AddDucky call (a class library), or with only a lookalike AddDucky.
        { Slice, "services.AddSingleton<Count>(_ => new(1))" },
        { Slice, "services.AddDucky()" },
    };

    [Theory]
    [MemberData(nameof(NotReported))]
    public Task Ducky016_CalledOrNothingToRegister_NotReported(string types, string configure) =>
        Verifier.VerifyWithGeneratorAsync<RegistrationGenerator>(Source(types, configure));

    // A skewed or older Ducky.Reactive (the hand-written skew fixture of SPEC §16.1): ReactiveEffect resolves but
    // AddReactiveEffect<TEffect>(DuckyBuilder) doesn't, so the generated method registers nothing for the reactive effect
    // (DUCKY022 instead) and there is nothing to call it for.
    [Fact]
    public Task Ducky016_OlderShapeReactiveOnly_NotReported()
    {
        var older = GeneratorHarness.Compile("""
            #pragma warning disable CS1591 // justification: undocumented fixture
            namespace Ducky.Reactive;
            public abstract class ReactiveEffect { }
            """).WithAssemblyName("Ducky.Reactive");
        older.ShouldCompileClean();
        var test = Verifier.Test<Verifier.GeneratingTest<RegistrationGenerator>>(
            Source("public sealed class {|#0:Echo|} : Ducky.Reactive.ReactiveEffect { }", "services.AddDucky(b => { })"));
        test.TestBehaviors |= TestBehaviors.SkipGeneratedSourcesCheck;
        test.TestState.AdditionalReferences.RemoveAll(r =>
            Path.GetFileNameWithoutExtension(((PortableExecutableReference)r).FilePath) is "Ducky.Reactive" or "R3");
        test.TestState.AdditionalReferences.Add(older.ToMetadataReference());
        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(RegistrationDiagnostics.Unresolved).WithLocation(0).WithArguments(
            "ReactiveEffect", "Echo", $"'{RegistrationGates.ReactiveMethod}' does not resolve (reference a Ducky.Reactive version that declares it)"));
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    // A file-local slice isn't registered either: the generator reports it as DUCKY023 instead.
    [Fact]
    public Task Ducky016_FileLocalSlice_NotReported() =>
        Verifier.VerifyWithGeneratorAsync<RegistrationGenerator>(
            Source("file sealed class {|#0:FileSlice|} : Slice<Count> { protected override Count Initial => new(0); }", "services.AddDucky(b => { })"),
            Verifier.Diagnostic(RegistrationDiagnostics.Excluded).WithLocation(0).WithArguments("FileSlice", RegistrationModel.FileLocal));
}
