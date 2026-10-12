using Ducky.Generators.Analyzers.Usage;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.StoreInjectionAnalyzer>;

namespace Ducky.Generators.Tests.Usage;

// DUCKY008 (SPEC §6.6, §16.2): IStore or IDispatcher injected into an effect, effect-group or middleware constructor,
// reported at the parameter with the type, the injected interface and the fix.
public sealed class StoreInjectionTests
{
    private const string EffectFix = "dispatch through the EffectContext (ctx.Dispatch) instead";
    private const string MiddlewareFix = "use the Store property instead";

    private const string Prelude = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Ducky;

        public sealed record Ping;
        public abstract class PingEffect : Effect<Ping>
        {
            public override Task Handle(Ping action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        """;

    public static TheoryData<string, string, string, string> Reported => new()
    {
        { "public sealed class E : PingEffect { public E(IStore {|#0:store|}) { } }", "E", "IStore", EffectFix },
        { "public sealed class E : PingEffect { public E(int n, IDispatcher {|#0:dispatcher|}) { } }", "E", "IDispatcher", EffectFix },
        { "public sealed class E(IStore? {|#0:store|}) : PingEffect { }", "E", "IStore", EffectFix },
        { "public sealed class G : EffectGroup { public G(IDispatcher {|#0:dispatcher|}) { } }", "G", "IDispatcher", EffectFix },
        { "public abstract class Base : PingEffect { protected Base(IStore {|#0:store|}) { } }", "Base", "IStore", EffectFix },
        { "public sealed class M : Middleware { public M(IStore {|#0:store|}) { } }", "M", "IStore", MiddlewareFix },
        { "public sealed class M(IDispatcher {|#0:dispatcher|}) : Middleware { }", "M", "IDispatcher", MiddlewareFix },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky008_StoreInjected_Reported(string source, string type, string injected, string fix) =>
        Verifier.VerifyAsync(Prelude + source, Verifier.Diagnostic(UsageDiagnostics.StoreInjected).WithLocation(0).WithArguments(type, injected, fix));

    [Fact]
    public Task Ducky008_EveryInjectedParameter_Reported() =>
        Verifier.VerifyAsync(
            Prelude + "public sealed class E : PingEffect { public E(IStore {|#0:a|}) { } public E(IStore {|#1:b|}, IDispatcher {|#2:c|}) { } }",
            Verifier.Diagnostic(UsageDiagnostics.StoreInjected).WithLocation(0).WithArguments("E", "IStore", EffectFix),
            Verifier.Diagnostic(UsageDiagnostics.StoreInjected).WithLocation(1).WithArguments("E", "IStore", EffectFix),
            Verifier.Diagnostic(UsageDiagnostics.StoreInjected).WithLocation(2).WithArguments("E", "IDispatcher", EffectFix));

    public static TheoryData<string> NotReported =>
    [
        // Other parameter types, including a slice store and the service provider.
        "public sealed record S(int N); public sealed class E : PingEffect { public E(SliceStore<S> slice, IServiceProvider services) { } }",
        // A store in a method of an effect, not its constructor.
        "public sealed class E : PingEffect { public static void Use(IStore store) { } }",
        // A store injected into a type that is neither an effect nor a middleware.
        "public sealed class Service { public Service(IStore store, IDispatcher dispatcher) { } }",
        "public sealed class Service(IStore store) { public IStore Store => store; }",
    ];

    [Theory]
    [MemberData(nameof(NotReported))]
    public Task Ducky008_NoStoreInConstructor_NotReported(string source) => Verifier.VerifyAsync(Prelude + source);
}
