using Ducky.Generators.Tests.Infrastructure;
using VerifyXunit;

namespace Ducky.Generators.Tests.Registration;

// GEN-02 shape matrix (SPEC §16.1, §17.7, INV-25): every shape's output compiles without any diagnostic under
// DocumentationMode.Diagnose, nullable enabled and warning level 9999, the generator reports no diagnostic, and its
// generated source is snapshotted. The core rows reference Ducky.dll only (no Ducky.Blazor, no Ducky.Reactive); the [Prerender]
// rows add the real Ducky.Blazor.dll.
public sealed class RegistrationShapeTests
{
    private const string Undocumented = "#pragma warning disable CS1591 // justification: shape sources are undocumented; only generated code is checked\n";

    private static readonly Dictionary<string, string> _shapes = new()
    {
        ["SlicesAndEffects"] = """
            namespace App;
            public sealed record Cart(int Count);
            public sealed record Todos(int Count);
            public sealed record Load(int Id);
            public interface IProductApi { }
            public sealed class CartSlice : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            public sealed class TodosStore : Ducky.SliceStore<Todos> { protected override Todos Initial => new(0); }
            public sealed class Ping : Ducky.Effect<Load>
            {
                public override System.Threading.Tasks.Task Handle(Load action, Ducky.EffectContext context, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
            }
            public sealed class LoadProduct(IProductApi api) : Ducky.Effect<Load>
            {
                public IProductApi Api => api;
                public override System.Threading.Tasks.Task Handle(Load action, Ducky.EffectContext context, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
            }
            public sealed class FetchRemote(System.Net.Http.HttpClient http) : Ducky.Effect<Load>
            {
                public System.Net.Http.HttpClient Http => http;
                public override System.Threading.Tasks.Task Handle(Load action, Ducky.EffectContext context, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
            }
            public sealed class CartEffects : Ducky.EffectGroup
            {
                public CartEffects() => On<Load>((_, _, _) => System.Threading.Tasks.Task.CompletedTask);
            }
            public sealed class Mixed : Ducky.EffectGroup
            {
                private Mixed() { }
                public Mixed(int unused) { }
            }
            public sealed class Unconstructible : Ducky.EffectGroup { internal Unconstructible() { } }
            public sealed class Audit : Ducky.Middleware { }
            public sealed class NotDucky : System.Exception { }
            """,
        ["Hierarchy"] = """
            namespace App;
            public sealed record Cart(int Count);
            public sealed record Load(int Id);
            public abstract class CartBase : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            public sealed class CartSlice : CartBase { }
            public abstract class LoadBase : Ducky.Effect<Load>
            {
                public override System.Threading.Tasks.Task Handle(Load action, Ducky.EffectContext context, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
            }
            public class LoadOnce : LoadBase { }
            public sealed class LoadTwice : LoadOnce { }
            """,
        ["Accessibility"] = """
            namespace App;
            public sealed record Cart(int Count);
            internal sealed class Hidden : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            public class Host
            {
                public sealed class Public : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
                internal sealed class Internal : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
                protected internal sealed class ProtectedInternal : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
                private sealed class Private : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
                protected sealed class Protected : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
                private protected sealed class PrivateProtected : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
                private static class Locked
                {
                    public sealed class Inside : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
                }
            }
            internal static class Outer
            {
                public static class Middle
                {
                    public sealed class Deep : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
                }
            }
            """,
        ["Generic"] = """
            namespace App;
            public sealed record Cart(int Count);
            public sealed record Load(int Id);
            public sealed class Box<T> : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            public sealed class Run<T> : Ducky.Effect<Load>
            {
                public override System.Threading.Tasks.Task Handle(Load action, Ducky.EffectContext context, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
            }
            public class Host<T>
            {
                public sealed class Inner : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            }
            """,
        ["Obsolete"] = """
            namespace App;
            public sealed record Cart(int Count);
            public sealed record Load(int Id);
            [System.Obsolete] public sealed class Old : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            [System.Obsolete("old")] public sealed class OldMessage : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            [System.Obsolete("old", false)] public sealed class OldWarning : Ducky.EffectGroup { }
            [System.Obsolete("gone", true)] public sealed class Gone : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            [System.Obsolete("gone", error: true)] public sealed class GoneEffect : Ducky.EffectGroup { }
            [System.Obsolete("gone", true)]
            public static class GoneHost
            {
                public sealed class Inside : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            }
            [System.Serializable] public sealed class Marked : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            [System.Obsolete("old", DiagnosticId = "APP002")] public sealed class OldWithId : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            [System.Obsolete("old", DiagnosticId = "APP003")]
            public static class OldHost
            {
                public sealed class Inner : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            }
            [System.Diagnostics.CodeAnalysis.Experimental("APP001")] public sealed class Trial : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            [System.Diagnostics.CodeAnalysis.Experimental("APP001")] public sealed class TrialEffect : Ducky.EffectGroup { }
            [System.Obsolete("old", DiagnosticId = "")] public sealed class OldEmptyId : Ducky.EffectGroup { }
            """,
        ["Names"] = """
            public sealed record Cart(int Count);
            public sealed class GlobalSlice : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            namespace App.@namespace
            {
                public sealed class @class : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            }
            """,
        ["Partial"] = """
            namespace App;
            public sealed record Cart(int Count);
            public partial class CartSlice : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            public partial class CartSlice : System.IDisposable { public void Dispose() { } }
            public partial class CartSlice { }
            """,
        ["RequiredMembers"] = """
            namespace App;
            public sealed record Cart(int Count);
            public sealed class Configured : Ducky.Slice<Cart>
            {
                [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
                public Configured() => Name = "cart";
                public required string Name { get; init; }
                protected override Cart Initial => new(0);
            }
            """,
        ["Empty"] = """
            namespace App;
            public sealed record Cart(int Count);
            """,
    };

    public static TheoryData<string> Shapes => [.. _shapes.Keys];

    // [Prerender] rows, compiled against the real Ducky.Blazor (stage 11): one fully qualified Prerender<T>(b) call per
    // registered [Prerender] slice, after every AddSlice, inside the [Obsolete]/[Experimental] suppressions.
    private static readonly Dictionary<string, string> _prerenderShapes = new()
    {
        ["Prerender"] = """
            namespace App;
            public sealed record Cart(int Count);
            public sealed record Todos(int Count);
            public sealed record Load(int Id);
            [Ducky.Prerender] public sealed class CartSlice : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            [Ducky.Prerender] public sealed class TodosStore : Ducky.SliceStore<Todos> { protected override Todos Initial => new(0); }
            public sealed class Plain : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            public partial class Split : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            [Ducky.Prerender] public partial class Split { }
            public class Host
            {
                [Ducky.Prerender] public sealed class Nested : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            }
            [Ducky.Prerender]
            public sealed class Configured : Ducky.Slice<Cart>
            {
                [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
                public Configured() => Name = "cart";
                public required string Name { get; init; }
                protected override Cart Initial => new(0);
            }
            [Ducky.Prerender, System.Obsolete("old")] public sealed class Old : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            [Ducky.Prerender, System.Diagnostics.CodeAnalysis.Experimental("APP001")] public sealed class Trial : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            [Ducky.Prerender, System.Obsolete("gone", true)] public sealed class Gone : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            public sealed class Ping : Ducky.Effect<Load>
            {
                public override System.Threading.Tasks.Task Handle(Load action, Ducky.EffectContext context, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
            }
            """,
        ["PrerenderGlobal"] = """
            public sealed record Cart(int Count);
            [Ducky.Prerender] public sealed class GlobalSlice : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            """,
    };

    public static TheoryData<string> PrerenderShapes => [.. _prerenderShapes.Keys];

    [Theory]
    [MemberData(nameof(PrerenderShapes))]
    public Task Generated_CompilesClean_PrerenderShape(string shape)
    {
        var input = DiagnosingHarness.WithBlazor(GeneratorHarness.Compile(Undocumented + _prerenderShapes[shape]));
        input.ShouldCompileClean();

        var driver = DiagnosingHarness.Run(new RegistrationGenerator(), input, out var output, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        output.ShouldCompileClean();
        var generated = driver.GetRunResult().Results.Single().GeneratedSources.ShouldHaveSingleItem();
        return Verifier.Verify(generated.SourceText.ToString()).UseParameters(shape);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public Task Generated_CompilesClean_RegistrationShape(string shape)
    {
        var input = GeneratorHarness.Compile(Undocumented + _shapes[shape]);
        input.ShouldCompileClean();

        var driver = DiagnosingHarness.Run(new RegistrationGenerator(), input, out var output, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        output.ShouldCompileClean();
        var generated = driver.GetRunResult().Results.Single().GeneratedSources.ShouldHaveSingleItem();
        generated.HintName.ShouldBe("DuckyRegistrations.g.cs");
        return Verifier.Verify(generated.SourceText.ToString()).UseParameters(shape);
    }
}
