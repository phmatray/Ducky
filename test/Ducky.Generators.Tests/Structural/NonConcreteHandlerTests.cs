using Ducky.Generators.Analyzers.Structural;
using static Ducky.Generators.Tests.Structural.StructuralSources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.StructuralAnalyzer>;

namespace Ducky.Generators.Tests.Structural;

// DUCKY005 (SPEC §16.2, ADR-0008): On<T>, EffectGroup.On<T> and Effect<T> with T abstract, an interface or object can
// never match an action's exact runtime type. The On<T> and OfActionType<T> arms are reported on the called method, the
// Effect<T> arm on the effect's name; the argument is T.
public sealed class NonConcreteHandlerTests
{
    public static TheoryData<string, string> ReportedOn => new()
    {
        { Slice("{|#0:On<IAct>|}((s, a) => s);"), "IAct" },
        { Slice("{|#0:On<BaseAct>|}((s, a) => s);"), "BaseAct" },
        { Slice("{|#0:On<object>|}((s, a) => s);"), "object" },
        // dynamic is object at run time.
        { Slice("{|#0:On<dynamic>|}((s, a) => s);"), "dynamic" },
        { Slice("{|#0:On<IAct>|}(s => s);"), "IAct" },
        { Slice("{|#0:this.On<IAct>|}(s => s);"), "IAct" },
        { Slice("{|#0:On|}((S s, IAct a) => s);"), "IAct" },
        { Group("{|#0:On<IAct>|}((a, ctx, ct) => Task.CompletedTask);"), "IAct" },
        { Group("{|#0:On<object>|}((a, ctx, ct) => Task.CompletedTask, Concurrency.Switch);"), "object" },
    };

    [Theory]
    [MemberData(nameof(ReportedOn))]
    public Task Ducky005_NonConcreteOn_Reported(string source, string type) =>
        Verifier.VerifyAsync(source, Verifier.Diagnostic(StructuralDiagnostics.NonConcreteHandler).WithLocation(0).WithArguments(type));

    public static TheoryData<string, string> ReportedEffect => new()
    {
        { "public sealed class {|#0:E|} : Effect<IAct> { public override Task Handle(IAct a, EffectContext c, CancellationToken t) => Task.CompletedTask; }", "IAct" },
        { "public abstract class {|#0:E|} : Effect<object>;", "object" },
        // Closed through a generic base: the base is silent, its non-concrete closing is reported.
        { "public abstract class Base<TA> : Effect<TA> where TA : notnull; public sealed class {|#0:E|} : Base<BaseAct> { public override Task Handle(BaseAct a, EffectContext c, CancellationToken t) => Task.CompletedTask; }", "BaseAct" },
        // Reported once, where the non-concrete closing first appears, not again on every derived effect.
        { "public abstract class {|#0:Mid|} : Effect<IAct>; public sealed class E : Mid { public override Task Handle(IAct a, EffectContext c, CancellationToken t) => Task.CompletedTask; }", "IAct" },
    };

    [Theory]
    [MemberData(nameof(ReportedEffect))]
    public Task Ducky005_NonConcreteEffect_Reported(string source, string type) =>
        Verifier.VerifyAsync(Prelude + source, Verifier.Diagnostic(StructuralDiagnostics.NonConcreteHandler).WithLocation(0).WithArguments(type));

    public static TheoryData<string> Silent => new()
    {
        Slice("On<Hit>((s, a) => s); On<Miss>(s => s); On<int>(s => s);"),
        Group("On<Hit>((a, ctx, ct) => Task.CompletedTask);"),
        Prelude + "public sealed class E : Effect<Hit> { public override Task Handle(Hit a, EffectContext c, CancellationToken t) => Task.CompletedTask; }",
        // A type parameter may be closed with a concrete type.
        Prelude + "public abstract class Open<TA> : Effect<TA> where TA : notnull;",
        Prelude + "public sealed class G<TA> : Slice<S> where TA : notnull { protected override S Initial => new(0); public G() { On<TA>(s => s); } }",
        // An explicit base-constructor call is an invocation of a Slice<T> or EffectGroup member, not On<T>.
        Prelude + "public sealed class B : Slice<S> { protected override S Initial => new(0); public B() : base() { On<Hit>(s => s); } }",
        Prelude + "public sealed class B : EffectGroup { public B() : base() { On<Hit>((a, ctx, ct) => Task.CompletedTask); } }",
        Prelude + "public sealed class B() : Slice<S>() { protected override S Initial => new(0); }",
        // A user method named On is not Ducky's.
        Prelude + "public sealed class Other { private void On<TA>(Func<TA, TA> f) { } public Other() { On<IAct>(a => a); } }",
        // Neither a slice nor an effect.
        Prelude + "public sealed class Plain : List<IAct>;",
    };

    [Theory]
    [MemberData(nameof(Silent))]
    public Task Ducky005_ConcreteOrOpenHandler_NotReported(string source) => Verifier.VerifyAsync(source);

    // A reactive effect whose Handle body is `body` (stage 16, the real Ducky.Reactive): OfActionType<T> filters on the
    // exact runtime type too.
    private static string Reactive(string body) => ReactivePrelude + $$"""
        public sealed class Search : ReactiveEffect
        {
            public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state)
            {
                {{body}}
            }
        }
        """;

    private const string ReactivePrelude = "using Ducky.Reactive;\nusing R3;\n" + Prelude;

    public static TheoryData<string, string> ReportedOfActionType => new()
    {
        { Reactive("return {|#0:actions.OfActionType<IAct>|}().Select(a => (object)a);"), "IAct" },
        { Reactive("return {|#0:actions.OfActionType<BaseAct>|}().Select(a => (object)a);"), "BaseAct" },
        { Reactive("return {|#0:actions.OfActionType<object>|}();"), "object" },
        { Reactive("return {|#0:actions.OfActionType<dynamic>|}().Select(a => (object)a);"), "dynamic" },
        // Called as a static method.
        { Reactive("return {|#0:DuckyReactiveExtensions.OfActionType<IAct>|}(actions).Select(a => (object)a);"), "IAct" },
    };

    [Theory]
    [MemberData(nameof(ReportedOfActionType))]
    public Task Ducky005_NonConcreteOfActionType_Reported(string source, string type) =>
        Verifier.VerifyAsync(source, Verifier.Diagnostic(StructuralDiagnostics.NonConcreteHandler).WithLocation(0).WithArguments(type));

    public static TheoryData<string> SilentOfActionType => new()
    {
        Reactive("return actions.OfActionType<Hit>().Select(a => (object)a);"),
        Reactive("return actions.OfActionType<int>().Select(a => (object)a);"),
        // A type parameter may be closed with a concrete type.
        ReactivePrelude + "public static class Filters { public static Observable<T> Only<T>(Observable<object> a) where T : notnull => a.OfActionType<T>(); }",
        // Another Ducky.Reactive generic method with an abstract type argument, and a user OfActionType.
        ReactivePrelude + "public static class Setup { public static DuckyBuilder Add(DuckyBuilder b) => b.AddReactiveEffect<ReactiveEffect>(); }",
        "using R3;\n" + Prelude + "public static class Mine { public static Observable<T> OfActionType<T>(this Observable<object> a) => a.Select(x => (T)x); public static Observable<IAct> Use(Observable<object> a) => a.OfActionType<IAct>(); }",
    };

    [Theory]
    [MemberData(nameof(SilentOfActionType))]
    public Task Ducky005_ConcreteOrOpenOfActionType_NotReported(string source) => Verifier.VerifyAsync(source);
}
