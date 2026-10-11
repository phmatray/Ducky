namespace Ducky.Generators.Tests.Purity;

// One host per arm with the same captured context, so a lambda body is checked as an On<T> reducer and as a
// Selector.Define projector unchanged: both take (Bag s, Hit a), and a block body returning s fits both.
internal static class PuritySources
{
    public const string Reducer = "reducer";
    public const string Projector = "projector";

    public static TheoryData<string> Arms => [Reducer, Projector];

    public const string Prelude = """
        using System;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Diagnostics;
        using System.IO;
        using System.Linq;
        using System.Net.Http;
        using System.Net.Http.Json;
        using System.Text;
        using System.Threading.Tasks;
        using Ducky;
        using Microsoft.JSInterop;

        public sealed class Box { public int Value { get; set; } public int Field; public string? Name { get; set; } }
        public sealed record Bag(int Count, List<int> Items, Dictionary<string, int> Map, ImmutableList<int> Frozen, int[] Array, Box Box);
        public sealed record Hit(int Amount, List<int> Items, Box Box);
        public sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => default; }
        public sealed class Draft { public List<int> Items { get; } = []; public int Count { get; set; } }
        public static class Recipes
        {
            public static Bag Produce(this Bag bag, Action<Draft> recipe) => bag;
            public static int Use<T>(Func<T> factory) => 0;
        }
        public static class Holder { public static int Static { get; set; } }
        """;

    public static string Host(string arm, string body)
    {
        var (baseType, initial, call) = arm == Reducer
            ? (" : Slice<Bag>", "protected override Bag Initial => new(0, [], [], [], [], new());", "On<Hit>(")
            : ("", "", "_ = Selector.Define(x => x.Get<Bag>(), x => x.Get<Hit>(), ");
        return Prelude + $$"""

            public sealed class Host{{baseType}}
            {
                private int _count;
                private static int _total;
                {{initial}}

                public Host(TimeProvider time, FixedTime fixedTime, HttpClient http, IJSRuntime js, Task<int> task, List<int> log, int seed)
                {
                    var captured = 0;
                    {{call}}(s, a) => {{body}});
                }
            }
            """;
    }
}
