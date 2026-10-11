using System.Globalization;
using Ducky.Generators.Tests.Infrastructure;
using VerifyXunit;

namespace Ducky.Generators.Tests.Dispatch;

// GEN-01 shape matrix (SPEC §16.1, §19, INV-25): every shape's output compiles without any diagnostic under
// DocumentationMode.Diagnose, nullable enabled and warning level 9999, and its generated sources and generator
// diagnostics are snapshotted. Shape sources skip their own docs (CS1591); generated code gets no such pass.
public sealed class DispatchShapeTests
{
    private const string Undocumented = "#pragma warning disable CS1591 // justification: shape sources are undocumented; only generated code is checked\n";

    private static readonly Dictionary<string, string> _shapes = new()
    {
        ["Positional"] = """
            namespace App;
            [Ducky.DuckyAction] public sealed record Increment(int By = 1);
            """,
        ["NonPositional"] = """
            namespace App;
            [Ducky.DuckyAction] public sealed record Reset;
            [Ducky.DuckyAction] public sealed record Rename
            {
                public Rename(string name) => Name = name;
                public string Name { get; }
            }
            [Ducky.DuckyAction] public sealed class Clear { }
            [Ducky.DuckyAction] public struct Tick { public int Count; }
            """,
        ["RecordStructs"] = """
            namespace App;
            [Ducky.DuckyAction] public readonly record struct Inc(int By = 1);
            [Ducky.DuckyAction] public record struct Ping;
            [Ducky.DuckyAction] public record struct Pair(int A, string B)
            {
                public Pair(int a) : this(a, "") { }
            }
            """,
        ["Defaults"] = """
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
            """,
        ["Constructors"] = """
            namespace App;
            [System.Obsolete("gone", error: true)] public sealed class Gone { }
            [Ducky.DuckyAction]
            public record Load(int Id)
            {
                public Load(string slug) : this(slug.Length) { }
                internal Load(long id) : this((int)id) { }
                protected internal Load(short id) : this((int)id) { }
                protected Load(ushort id) : this((int)id) { }
                private Load(byte id) : this((int)id) { }
                public Load(Load original) => Id = original.Id;
                [System.Obsolete("no", error: true)] public Load(double id) : this((int)id) { }
                [System.Obsolete("old")] public Load(Gone gone, int id) : this(id) { }
            }
            """,
        ["Accessibility"] = """
            namespace App;
            internal sealed class Secret { }
            [Ducky.DuckyAction] internal sealed record Hidden(int X);
            [Ducky.DuckyAction] public sealed record Mixed
            {
                internal Mixed(Secret secret) { }
            }
            internal static class Container
            {
                [Ducky.DuckyAction] public sealed record Inside(int X);
            }
            """,
        ["Nested"] = """
            namespace App;
            public static class Outer
            {
                public static class Middle
                {
                    [Ducky.DuckyAction] public sealed record Inner(int X);
                }
                [Ducky.DuckyAction] public sealed record Shallow;
            }
            public struct Holder
            {
                [Ducky.DuckyAction] public readonly record struct Deep(string Value);
            }
            public class Base
            {
                [Ducky.DuckyAction] protected internal sealed record Shared(int X);
            }
            """,
        ["Obsolete"] = """
            namespace App;
            [System.Obsolete] public sealed class OldType { }
            [Ducky.DuckyAction, System.Obsolete] public sealed record Retired(int X);
            [Ducky.DuckyAction, System.Obsolete("use Fresh")] public sealed record Deprecated;
            [Ducky.DuckyAction, System.Obsolete] public sealed record Uses(OldType Type);
            [Ducky.DuckyAction] public sealed record Partly
            {
                [System.Obsolete("old")] public Partly(int x) { }
            }
            [System.Obsolete] public static class OldHost
            {
                [Ducky.DuckyAction] public sealed record InOld;
            }
            """,
        // Each is DUCKY020 and emits nothing: generated code can't reference an error-obsolete symbol (CS0619).
        ["ObsoleteError"] = """
            using System.Collections.Generic;
            namespace App;
            [System.Obsolete("gone", error: true)] public sealed class Gone { }
            [System.Obsolete("gone", true)] public class GoneOuter { public sealed class Leaf { } }
            [Ducky.DuckyAction, System.Obsolete("gone", true)] public sealed record GoneAction(int X);
            [System.Obsolete("gone", true)] public static class GoneHost
            {
                [Ducky.DuckyAction] public sealed record InGone;
            }
            [Ducky.DuckyAction] public sealed record GoneCtor
            {
                [System.Obsolete("no", true)] public GoneCtor(int x) { }
            }
            [Ducky.DuckyAction] public sealed record GoneElement
            {
                [System.Obsolete("old")] public GoneElement(List<Gone> items) { }
            }
            [Ducky.DuckyAction] public sealed record GoneArray
            {
                [System.Obsolete("old")] public GoneArray(Gone[] items) { }
            }
            [Ducky.DuckyAction] public sealed record GoneContainer
            {
                [System.Obsolete("old")] public GoneContainer(GoneOuter.Leaf leaf) { }
            }
            public class Wrap<T> { public sealed class Inner { } }
            [Ducky.DuckyAction] public sealed record GoneTypeArgumentOfContainer
            {
                [System.Obsolete("old")] public GoneTypeArgumentOfContainer(Wrap<Gone>.Inner inner) { }
            }
            """,
        ["FileLocal"] = """
            namespace App;
            [Ducky.DuckyAction] file sealed record Local(int X);
            file static class LocalHost
            {
                [Ducky.DuckyAction] public sealed record InLocal;
            }
            """,
        ["GlobalNamespace"] = """
            [Ducky.DuckyAction] public sealed record GlobalPing(int Count = 0);
            """,
        ["KeywordNames"] = """
            #pragma warning disable CS8981 // justification: the keyword-named record is the shape under test
            namespace App.@event;
            [Ducky.DuckyAction] public sealed record @class(int @int, string Class);
            [Ducky.DuckyAction] public sealed record Move(int X, int x, int dispatcher);
            [Ducky.DuckyAction(MethodName = "Go")] public sealed record Navigate(string To);
            [Ducky.DuckyAction(MethodName = "default")] public sealed record Fallback;
            """,
        // Oblivious type arguments of constrained type parameters stay non-annotated (no CS8714, CS8634, CS8631).
        ["NullableDisabled"] = """
            using System.Collections.Generic;
            namespace App;
            public sealed class Box<T> where T : class { }
            public sealed class LooseBox<T> where T : class? { }
            public sealed class Sorted<T> where T : System.IComparable { }
            public class Holder<T> { public sealed class Inner { } }
            #nullable disable
            [Ducky.DuckyAction]
            public sealed record Search(
                string Query = null, List<string> Tags = null, string[] Names = null,
                object Payload = null, int Page = 1, int? Size = null, KeyValuePair<string, int> Pair = default);
            [Ducky.DuckyAction]
            public sealed record Index(
                Dictionary<string, int> Map = null, Dictionary<string, List<string>> Groups = null, Box<string> Boxed = null,
                LooseBox<string> Loose = null, Sorted<string> Order = null, Holder<string>.Inner Inner = null);
            """,
        // A type nested in a generic type is not itself generic (IsGenericType counts containing types).
        ["GenericNesting"] = """
            using System.Collections.Generic;
            namespace App;
            public class Outer<T> { public sealed class Inner { } public sealed class Leaf<U> { } }
            [Ducky.DuckyAction]
            public sealed record UsesNested(Outer<int>.Inner X, Dictionary<string, int>.KeyCollection Keys, Outer<string>.Leaf<string> Leaf);
            """,
        // `new` without [SetsRequiredMembers] is CS9035 while the type or a base type has a required member.
        ["Required"] = """
            using System.Diagnostics.CodeAnalysis;
            namespace App;
            [Ducky.DuckyAction] public sealed record Rename { public required string Name { get; init; } }
            [Ducky.DuckyAction] public sealed class Move
            {
                public required int X;
                public Move() { }
                [SetsRequiredMembers] public Move(int x) => X = x;
            }
            public class Named { public required string Name { get; init; } }
            [Ducky.DuckyAction] public sealed class Child : Named
            {
                public Child() { }
                [SetsRequiredMembers] public Child(string name) => Name = name;
            }
            """,
        // An [Obsolete] DiagnosticId or an [Experimental] id replaces CS0618 and is suppressed by its own pragma.
        ["DiagnosticIds"] = """
            using System.Diagnostics.CodeAnalysis;
            namespace App;
            [Experimental("APPX")] public sealed class Preview { }
            [System.Obsolete("old", DiagnosticId = "APP002")] public sealed class Legacy { }
            [Ducky.DuckyAction, System.Obsolete("old", DiagnosticId = "APP001")] public sealed record Retired(int X);
            [Ducky.DuckyAction, Experimental("APPY")] public sealed record Trial;
            [Ducky.DuckyAction] public sealed record Uses
            {
                [System.Obsolete("old", DiagnosticId = "APP003")] public Uses(System.Collections.Generic.List<Legacy> items) { }
                [Experimental("APPX")] public Uses(Preview preview) { }
            }
            [System.Obsolete("old", DiagnosticId = "APP004")] public sealed class Ancient { }
            public class Wrap<T> { public sealed class Inner { } }
            [Ducky.DuckyAction] public sealed record UsesNested
            {
                [System.Obsolete("old", DiagnosticId = "APP005")] public UsesNested(Wrap<Ancient>.Inner inner) { }
            }
            """,
        // Tuple element names survive, nested and as type arguments, under enabled and disabled nullable context.
        ["Tuples"] = """
            using System.Collections.Generic;
            namespace App;
            public sealed class Dup { }
            [Ducky.DuckyAction] public sealed record Person((string Name, int Age) Info);
            [Ducky.DuckyAction]
            public sealed record Nested((int A, (Dup B, string? C) Inner) Value, List<(string K, int V)> Pairs, (int, string Named) Partial);
            #nullable disable
            [Ducky.DuckyAction] public sealed record Legacy((string Name, int Age) Info, (int, string Named) Partial, List<(string K, Dup V)> Pairs = null);
            """,
        ["RefKinds"] = """
            namespace App;
            [Ducky.DuckyAction]
            public sealed class Measure
            {
                public Measure(ref int a, out int b, in int c, ref readonly int d, params int[] rest) => b = a + c + d + rest.Length;
            }
            """,
    };

    public static TheoryData<string> Shapes => [.. _shapes.Keys];

    [Theory]
    [MemberData(nameof(Shapes))]
    public Task Generated_CompilesClean_DispatchShape(string shape)
    {
        var compilation = GeneratorHarness.Compile(Undocumented + _shapes[shape]);
        compilation.ShouldCompileClean();

        var driver = DiagnosingHarness.Run(new DispatchExtensionsGenerator(), compilation, out var output, out var diagnostics);

        output.ShouldCompileClean();
        var sources = driver.GetRunResult().Results.Single().GeneratedSources
            .OrderBy(s => s.HintName, StringComparer.Ordinal)
            .Select(s => $"// {s.HintName}\n{s.SourceText}");
        var reported = diagnostics.Select(d => $"// {d.Severity} {d.Id} at {d.Location.GetLineSpan().Span}: {d.GetMessage(CultureInfo.InvariantCulture)}");
        return Verifier.Verify(string.Join("\n", reported.Concat(sources))).UseParameters(shape);
    }

    // Default literals are culture-invariant: a NegativeSign of U+2212 (sv-SE, fi-FI, nb-NO) must not reach `(-1D / 0D)`.
    [Fact]
    public void Generated_CompilesClean_NegativeSignCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "\u2212";
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
        try
        {
            var compilation = GeneratorHarness.Compile(Undocumented + _shapes["Defaults"]);
            DiagnosingHarness.Run(new DispatchExtensionsGenerator(), compilation, out var output, out _);

            output.ShouldCompileClean();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
