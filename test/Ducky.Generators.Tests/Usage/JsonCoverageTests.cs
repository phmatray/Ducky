using System.Text.Json.SourceGeneration;
using Ducky.Generators.Analyzers.Usage;
using Microsoft.CodeAnalysis.Testing;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.JsonContextAnalyzer>;

namespace Ducky.Generators.Tests.Usage;

// DUCKY006 (SPEC §10, §16.2, ADR-0022): a [DuckyAction] or slice state type of this compilation outside the types the
// context passed to UseJson covers, reported at the UseJson argument. The covered set is the T of every JsonTypeInfo<T>
// property the context declares, so the real System.Text.Json source generator runs in the driver.
public sealed class JsonCoverageTests
{
    private static string Source(string contexts, string argument, string types = "") => $$"""
        using System.Collections.Generic;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using System.Text.Json.Serialization.Metadata;
        using Ducky;
        using Microsoft.Extensions.DependencyInjection;

        [DuckyAction] public sealed record Add(int Amount);
        public sealed record Count(int Value);
        public sealed class CountSlice : Slice<Count> { protected override Count Initial => new(0); }
        public sealed record Unrelated(int X);
        {{types}}
        {{contexts}}

        public static class Startup
        {
            public static void Configure(IServiceCollection services, IJsonTypeInfoResolver resolver, IJsonTypeInfoResolver[] resolvers) =>
                services.AddDucky(b => b.UseJson({{argument}}));
        }
        """;

    private const string Both = "[JsonSerializable(typeof(Add))] [JsonSerializable(typeof(Count))] internal partial class AppJson : JsonSerializerContext;";
    private const string Actions = "[JsonSerializable(typeof(Add))] internal partial class ActionJson : JsonSerializerContext;";
    private const string States = "[JsonSerializable(typeof(Count))] internal partial class StateJson : JsonSerializerContext;";
    private const string Neither = "[JsonSerializable(typeof(Unrelated))] internal partial class OtherJson : JsonSerializerContext;";

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected) =>
        Verifier.VerifyWithGeneratorAsync<JsonSourceGenerator>(source, expected);

    private static DiagnosticResult Missing(string type) => Verifier.Diagnostic(UsageDiagnostics.NotCovered).WithLocation(0).WithArguments(type);

    public static TheoryData<string, string, string> Reported => new()
    {
        // One context: the state type, or the action type, is missing.
        { Actions, "{|#0:ActionJson.Default|}", "Count" },
        { States, "{|#0:StateJson.Default|}", "Add" },

        // Combine: the union of the contexts still misses one, whatever form the resolvers take.
        { Actions + Neither, "{|#0:JsonTypeInfoResolver.Combine(ActionJson.Default, OtherJson.Default)|}", "Count" },
        { Actions, "{|#0:JsonTypeInfoResolver.Combine(new IJsonTypeInfoResolver[] { ActionJson.Default })|}", "Count" },
        { Actions, "{|#0:JsonTypeInfoResolver.Combine([ActionJson.Default])|}", "Count" },

        // A type the context reaches only through a [JsonIgnore]d property is not covered.
        {
            "public sealed record Holder(int N) { [JsonIgnore] public Count? Hidden { get; init; } }\n"
                + "[JsonSerializable(typeof(Add))] [JsonSerializable(typeof(Holder))] internal partial class HolderJson : JsonSerializerContext;",
            "{|#0:HolderJson.Default|}",
            "Count"
        },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky006_TypeNotCovered_Reported(string contexts, string argument, string type) =>
        VerifyAsync(Source(contexts, argument), Missing(type));

    [Fact]
    public Task Ducky006_EveryMissingType_Reported() =>
        VerifyAsync(Source(Neither, "{|#0:OtherJson.Default|}"), Missing("Add"), Missing("Count"));

    // A nested action and the state of a slice deriving from a generic base are found; the base's type parameter is not
    // a state type.
    [Fact]
    public Task Ducky006_NestedActionAndInheritedState_Reported() =>
        VerifyAsync(
            Source(Both, "{|#0:AppJson.Default|}", """
                public static class Feature { [DuckyAction] public sealed record Reset; }
                public sealed record Total(int Sum);
                public abstract class BaseSlice<T> : Slice<T> where T : class { }
                public sealed class TotalSlice : BaseSlice<Total> { protected override Total Initial => new(0); }
                """),
            Missing("Feature.Reset"),
            Missing("Total"));

    // A state type that contains a type parameter can't be named in [JsonSerializable(typeof(...))], so it is skipped;
    // a nested reference type's nullable annotation is dropped from the message (typeof(Count?) is CS8639).
    [Fact]
    public Task Ducky006_OpenOrAnnotatedState_ReportedOnlyWhenNameable() =>
        VerifyAsync(
            Source(Both, "{|#0:AppJson.Default|}", """
                [DuckyAction] public sealed record Wrap<T>(T Value);
                public abstract class ListSlice<T> : Slice<List<T>> { }
                public abstract class ArraySlice<T> : Slice<T[]> { }
                public static class Outer<T>
                {
                    public sealed record Inner(int N);
                    public abstract class InnerSlice : Slice<Inner> { }
                }
                public sealed class MaybeSlice : Slice<List<Count?>> { protected override List<Count?> Initial => []; }
                """),
            Missing("System.Collections.Generic.List<Count>"));

    public static TheoryData<string, string> NotReported => new()
    {
        // Every type is covered: one context, or the union of a Combine.
        { Both, "AppJson.Default" },
        { Actions + States, "JsonTypeInfoResolver.Combine(ActionJson.Default, StateJson.Default)" },

        // A state type reached only through another root's property, as a collection element, is covered.
        {
            "public sealed record Root(List<Count> Items);\n"
                + "[JsonSerializable(typeof(Add))] [JsonSerializable(typeof(Root))] internal partial class RootJson : JsonSerializerContext;",
            "RootJson.Default"
        },

        // Any other argument shape: options, a resolver that isn't a context, a Combine over one or over an array whose
        // elements aren't known.
        { Neither, "new JsonSerializerOptions { TypeInfoResolver = OtherJson.Default }" },
        { Neither, "resolver" },
        { Neither, "JsonTypeInfoResolver.Combine(OtherJson.Default, resolver)" },
        { Neither, "JsonTypeInfoResolver.Combine(resolvers)" },
        { Neither, "JsonTypeInfoResolver.Combine(new IJsonTypeInfoResolver[1])" },
    };

    [Theory]
    [MemberData(nameof(NotReported))]
    public Task Ducky006_CoveredOrOtherShape_NotReported(string contexts, string argument) => VerifyAsync(Source(contexts, argument));

    // A method named UseJson that isn't DuckyBuilder's.
    [Fact]
    public Task Ducky006_OtherUseJson_NotReported() =>
        VerifyAsync(Source(Both + Neither, "AppJson.Default", "public static class Lookalike { public static void UseJson(object o) { } public static void Call() => UseJson(OtherJson.Default); }"));
}
