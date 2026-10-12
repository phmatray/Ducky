using Ducky.Generators.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ducky.Generators.Tests.Usage;

// Descriptors_MatchCatalogue (SPEC §16.2) for DUCKY004, 006, 008, 010, 016 and 025, so a descriptor string mutant dies
// here instead of surviving the verifier tests.
public sealed class UsageDescriptorsTests
{
    private static readonly DiagnosticAnalyzer[] _analyzers =
        [new LifetimeAnalyzer(), new JsonContextAnalyzer(), new StoreInjectionAnalyzer(), new BlockingAnalyzer(), new RegistrationCallAnalyzer()];

    public static TheoryData<string, string, string> Catalogue => new()
    {
        {
            "DUCKY004", "Singleton store in a server project",
            "'Lifetime = ServiceLifetime.Singleton' in a web project that is not Blazor WebAssembly shares one store between every user of the server: keep the default lifetime (Scoped)"
        },
        { "DUCKY006", "Type missing from the JSON context", "'{0}' is not covered by the JSON context passed to UseJson: add [JsonSerializable(typeof({0}))] to it" },
        {
            "DUCKY008", "Store injected into an effect or middleware constructor",
            "'{0}' injects '{1}' into its constructor, where using it would re-enter materialization: {2}"
        },
        { "DUCKY010", "Blocking on a Ducky task", "Blocking on '{0}' with '{1}' can deadlock the dispatcher: await it instead" },
        {
            "DUCKY016", "Generated registration never called",
            "This assembly defines slices or effects but never calls 'AddDuckyGenerated_{0}': call b.AddDuckyGenerated_{0}() in AddDucky to register them"
        },
        {
            "DUCKY025", "JsonTypeInfoResolver.Combine drops the context options",
            "'{0}' sets [JsonSourceGenerationOptions] that JsonTypeInfoResolver.Combine drops, so they would not apply to persisted, seed and DevTools JSON: use UseJson(new JsonSerializerOptions({0}.Default.Options) {{ TypeInfoResolver = {1} }})"
        },
    };

    [Theory]
    [MemberData(nameof(Catalogue))]
    public void Descriptors_MatchCatalogue(string id, string title, string messageFormat)
    {
        var descriptor = _analyzers.SelectMany(a => a.SupportedDiagnostics).Single(d => d.Id == id);

        descriptor.ShouldSatisfyAllConditions(
            d => d.DefaultSeverity.ShouldBe(DiagnosticSeverity.Warning),
            d => d.Title.ToString(null).ShouldBe(title),
            d => d.MessageFormat.ToString(null).ShouldBe(messageFormat),
            d => d.Category.ShouldBe("Ducky"),
            d => d.IsEnabledByDefault.ShouldBeTrue(),
            d => d.HelpLinkUri.ShouldBe($"https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{id}.md"));
    }

    [Fact]
    public void Descriptors_EveryUsageDescriptorCatalogued() =>
        _analyzers.SelectMany(a => a.SupportedDiagnostics).Select(d => d.Id).Order().ShouldBe(Catalogue.Select(row => row.Data.Item1));

    // RS1037: a diagnostic reported from a compilation end action carries the CompilationEnd tag; only DUCKY016 is one.
    [Fact]
    public void Descriptors_OnlyRegistrationCallIsCompilationEnd() =>
        _analyzers.SelectMany(a => a.SupportedDiagnostics).Where(d => d.CustomTags.Contains(WellKnownDiagnosticTags.CompilationEnd))
            .Select(d => d.Id).ShouldBe(["DUCKY016"]);
}
