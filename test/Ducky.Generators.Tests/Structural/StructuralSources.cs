namespace Ducky.Generators.Tests.Structural;

// The types the structural verifier tests share: concrete, abstract, interface and record actions, and one state.
internal static class StructuralSources
{
    public const string Prelude = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Ducky;

        public sealed record S(int Count);
        public sealed record T2(int Count);
        public sealed record Hit(int Id);
        public sealed record Miss(int Id);
        public interface IAct;
        public abstract record BaseAct;

        """;

    // A slice whose constructor body is `body`.
    public static string Slice(string body) => Prelude + $$"""
        public sealed class CounterSlice : Slice<S>
        {
            protected override S Initial => new(0);

            public CounterSlice()
            {
                {{body}}
            }
        }
        """;

    // An effect group whose constructor body is `body`.
    public static string Group(string body) => Prelude + $$"""
        public sealed class Group : EffectGroup
        {
            public Group()
            {
                {{body}}
            }
        }
        """;
}
