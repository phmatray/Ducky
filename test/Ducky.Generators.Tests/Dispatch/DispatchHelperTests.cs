using System.Runtime.Loader;
using Ducky.Generators.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Generators.Tests.Dispatch;

// GEN-01 helpers run against a real store (no mocks): the consumer's calls are compiled C#, so overload resolution
// picks the helper exactly as an application would (SPEC §16.1).
public sealed class DispatchHelperTests
{
    // `App.Registration.Setup` registers a slice that keeps the last TAction; the shape's `App.Probe.Run` drives the helpers.
    private const string Undocumented = "#pragma warning disable CS1591 // justification: probe sources are undocumented; only generated code is checked\n";

    private const string Recorder = Undocumented + """
        namespace App;
        public sealed record Seen(object? Action);
        public sealed class SeenSlice : Ducky.Slice<Seen>
        {
            protected override Seen Initial => new((object?)null);
            public SeenSlice() => On<TAction>((_, a) => new(a));
        }
        public static class Registration
        {
            public static void Setup(Ducky.DuckyBuilder d) => d.AddSlice<SeenSlice>();
        }
        """;

    private static async Task<string> RunProbe(string actionType, string source)
    {
        var compilation = GeneratorHarness.Compile(Recorder.Replace("TAction", actionType, StringComparison.Ordinal), Undocumented + source);
        GeneratorHarness.Run(new DispatchExtensionsGenerator(), compilation, out var output);
        output.ShouldCompileClean();

        using var image = new MemoryStream();
        output.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        image.Position = 0;
        var context = new AssemblyLoadContext("probe", isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(image);
            var setup = assembly.GetType("App.Registration")!.GetMethod("Setup")!.CreateDelegate<Action<DuckyBuilder>>();
            var run = assembly.GetType("App.Probe")!.GetMethod("Run")!.CreateDelegate<Func<IStore, Task<string>>>();
            var services = new ServiceCollection().AddDucky(setup);
            await using var provider = services.BuildServiceProvider();
            return await run(provider.GetRequiredService<IStore>());
        }
        finally
        {
            context.Unload();
        }
    }

    // The implicit Inc() of a record struct gets no helper, or store.Inc() would bind to it and dispatch By = 0.
    [Fact]
    public async Task Helper_RecordStructWithDefaults_DispatchesDefaultValues()
    {
        var result = await RunProbe("Inc", """
            namespace App;
            [Ducky.DuckyAction] public readonly record struct Inc(int By = 1);
            public static class Probe
            {
                public static async System.Threading.Tasks.Task<string> Run(Ducky.IStore store)
                {
                    store.Inc();
                    await store.WhenIdleAsync();
                    var first = store.State.Get<Seen>().Action;
                    var result = await store.IncAsync();
                    var second = store.State.Get<Seen>().Action;
                    var named = await store.IncAsync(By: 5);
                    return $"{first} | {result} {second} | {named} {store.State.Get<Seen>().Action}";
                }
            }
            """);

        result.ShouldBe("Inc { By = 1 } | Reduced Inc { By = 1 } | Reduced Inc { By = 5 }");
    }

    // Non-normative: every default literal kind dispatches the constructor's own default (NaN, infinities, -0.0, enums,
    // decimal precision, escaped strings): the action equals `new Configure()`.
    [Fact]
    public async Task Helper_DefaultValues_MatchConstructorDefaults()
    {
        var result = await RunProbe("Configure", """
            namespace App;
            public enum Color { Red, Green }
            [System.Flags] public enum Access : long { None = 0, Read = 1, Write = 2, Negative = -4 }
            [Ducky.DuckyAction]
            public sealed record Configure(
                float Ratio = 0.1f, double Scale = -0.0, decimal Price = 0.1000000000000000000000000001m,
                double Nan = double.NaN, double Up = double.PositiveInfinity, float Down = float.NegativeInfinity,
                long Big = long.MinValue, ulong Huge = ulong.MaxValue, char Separator = '\n', string Label = "a\"b\\\u0001",
                bool Enabled = true, Color Tint = Color.Green, Access Rights = Access.Read | Access.Write,
                Access Denied = Access.Negative, Color? Maybe = Color.Red, System.DateTime When = default, int? Count = null,
                string? Note = null, byte Small = 7);
            public static class Probe
            {
                public static async System.Threading.Tasks.Task<string> Run(Ducky.IStore store)
                {
                    await store.ConfigureAsync();
                    var seen = (Configure)store.State.Get<Seen>().Action!;
                    return $"{seen == new Configure()} {double.IsNegative(seen.Scale)} {float.IsNegativeInfinity(seen.Down)}";
                }
            }
            """);

        result.ShouldBe("True True True");
    }

    // A named tuple literal binds to the helper without CS8123: element names survive in both nullable contexts.
    [Theory]
    [InlineData("enable")]
    [InlineData("disable")]
    public async Task Helper_NamedTupleParameter_KeepsElementNames(string context)
    {
        var result = await RunProbe("Person", $$"""
            namespace App;
            #nullable {{context}}
            [Ducky.DuckyAction]
            public sealed record Person((string Name, int Age) Info, System.Collections.Generic.List<(string K, int V)> Pairs);
            #nullable restore
            public static class Probe
            {
                public static async System.Threading.Tasks.Task<string> Run(Ducky.IStore store)
                {
                    await store.PersonAsync((Name: "a", Age: 1), [(K: "k", V: 2)]);
                    var seen = (Person)store.State.Get<Seen>().Action!;
                    return $"{seen.Info} {seen.Pairs[0]}";
                }
            }
            """);

        result.ShouldBe("(a, 1) (k, 2)");
    }
}
