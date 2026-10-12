using System.Text.Json.SourceGeneration;
using Ducky.Generators.Analyzers.Usage;
using Microsoft.CodeAnalysis.Testing;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.JsonContextAnalyzer>;

namespace Ducky.Generators.Tests.Usage;

// DUCKY025 (SPEC §10, §16.2, ADR-0022): UseJson(JsonTypeInfoResolver.Combine(...)) over a context whose
// [JsonSourceGenerationOptions] sets a non-default option, reported at that context with the fix. The contexts are
// compiled by the real System.Text.Json source generator.
public sealed class CombineOptionsTests
{
    private const string Plain = "[JsonSerializable(typeof(int))] internal partial class PlainJson : JsonSerializerContext;";
    private const string Camel =
        "[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)] [JsonSerializable(typeof(string))] internal partial class CamelJson : JsonSerializerContext;";
    private const string Web = "[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)] [JsonSerializable(typeof(string))] internal partial class WebJson : JsonSerializerContext;";

    private static string Source(string contexts, string argument) => $$"""
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using System.Text.Json.Serialization.Metadata;
        using Ducky;
        using Microsoft.Extensions.DependencyInjection;

        {{contexts}}

        public static class Startup
        {
            public static void Configure(IServiceCollection services, IJsonTypeInfoResolver resolver) =>
                services.AddDucky(b => b.UseJson({{argument}}));
        }
        """;

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected) =>
        Verifier.VerifyWithGeneratorAsync<JsonSourceGenerator>(source, expected);

    private static DiagnosticResult Dropped(int location, string context, string combine) =>
        Verifier.Diagnostic(UsageDiagnostics.OptionsDropped).WithLocation(location).WithArguments(context, combine);

    public static TheoryData<string, string, string, string> Reported => new()
    {
        { Camel + Plain, "JsonTypeInfoResolver.Combine({|#0:CamelJson.Default|}, PlainJson.Default)", "CamelJson", "JsonTypeInfoResolver.Combine(CamelJson.Default, PlainJson.Default)" },
        { Web + Plain, "JsonTypeInfoResolver.Combine(PlainJson.Default, {|#0:WebJson.Default|})", "WebJson", "JsonTypeInfoResolver.Combine(PlainJson.Default, WebJson.Default)" },

        // The context's options are lost next to any other resolver too.
        { Camel, "JsonTypeInfoResolver.Combine({|#0:CamelJson.Default|}, resolver)", "CamelJson", "JsonTypeInfoResolver.Combine(CamelJson.Default, resolver)" },

        // GenerationMode beside a serializer option doesn't hide it.
        {
            "[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)] [JsonSerializable(typeof(string))] internal partial class MixedJson : JsonSerializerContext;" + Plain,
            "JsonTypeInfoResolver.Combine({|#0:MixedJson.Default|}, PlainJson.Default)", "MixedJson", "JsonTypeInfoResolver.Combine(MixedJson.Default, PlainJson.Default)"
        },

        // The fix is one line whatever the Combine's layout or resolver form.
        { Camel + Plain, "JsonTypeInfoResolver.Combine(\n    {|#0:CamelJson.Default|},\n    PlainJson.Default)", "CamelJson", "JsonTypeInfoResolver.Combine(CamelJson.Default, PlainJson.Default)" },
        { Camel + Plain, "JsonTypeInfoResolver.Combine([\n    {|#0:CamelJson.Default|},\n    PlainJson.Default])", "CamelJson", "JsonTypeInfoResolver.Combine(CamelJson.Default, PlainJson.Default)" },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky025_CombineDropsContextOptions_Reported(string contexts, string argument, string context, string combine) =>
        VerifyAsync(Source(contexts, argument), Dropped(0, context, combine));

    [Fact]
    public Task Ducky025_EveryContextWithOptions_Reported() =>
        VerifyAsync(
            Source(Camel + Web, "JsonTypeInfoResolver.Combine({|#0:CamelJson.Default|}, {|#1:WebJson.Default|})"),
            Dropped(0, "CamelJson", "JsonTypeInfoResolver.Combine(CamelJson.Default, WebJson.Default)"),
            Dropped(1, "WebJson", "JsonTypeInfoResolver.Combine(CamelJson.Default, WebJson.Default)"));

    public static TheoryData<string, string> NotReported => new()
    {
        // Contexts with default options: none declared, none set, or the General defaults.
        { Plain + "[JsonSerializable(typeof(long))] internal partial class OtherJson : JsonSerializerContext;", "JsonTypeInfoResolver.Combine(PlainJson.Default, OtherJson.Default)" },
        { "[JsonSourceGenerationOptions] [JsonSerializable(typeof(long))] internal partial class EmptyJson : JsonSerializerContext;" + Plain, "JsonTypeInfoResolver.Combine(EmptyJson.Default, PlainJson.Default)" },
        {
            "[JsonSourceGenerationOptions(JsonSerializerDefaults.General)] [JsonSerializable(typeof(long))] internal partial class GeneralJson : JsonSerializerContext;" + Plain,
            "JsonTypeInfoResolver.Combine(GeneralJson.Default, PlainJson.Default)"
        },

        // GenerationMode is a source-generation setting, not a serializer option: Combine drops nothing.
        {
            "[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)] [JsonSerializable(typeof(long))] internal partial class MetadataJson : JsonSerializerContext;" + Plain,
            "JsonTypeInfoResolver.Combine(MetadataJson.Default, PlainJson.Default)"
        },

        // The recommended form, and a single context, whose options UseJson copies.
        { Camel + Plain, "new JsonSerializerOptions(CamelJson.Default.Options) { TypeInfoResolver = JsonTypeInfoResolver.Combine(CamelJson.Default, PlainJson.Default) }" },
        { Camel, "CamelJson.Default" },

        // A Combine that isn't JsonTypeInfoResolver's.
        {
            Camel + Plain + "public static class Resolvers { public static IJsonTypeInfoResolver Combine(params IJsonTypeInfoResolver[] all) => all[0]; }",
            "Resolvers.Combine(CamelJson.Default, PlainJson.Default)"
        },
    };

    [Theory]
    [MemberData(nameof(NotReported))]
    public Task Ducky025_DefaultOptionsOrNotCombine_NotReported(string contexts, string argument) => VerifyAsync(Source(contexts, argument));
}
