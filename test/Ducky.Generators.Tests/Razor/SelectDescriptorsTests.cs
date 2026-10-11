using Ducky.Generators.Analyzers.Razor;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Tests.Razor;

// Descriptors_MatchCatalogue (SPEC §16.2) for DUCKY009, so a descriptor string mutant dies here instead of surviving the
// analyzer tests.
public sealed class SelectDescriptorsTests
{
    public static TheoryData<string, DiagnosticSeverity, string, string> Catalogue => new()
    {
        {
            "DUCKY009", DiagnosticSeverity.Warning, "Select after the first render",
            "'Select' in '{0}' runs after the component's first render, when registration is closed (DUCKY352): call it in OnInitialized and keep the selection in a field"
        },
    };

    [Theory]
    [MemberData(nameof(Catalogue))]
    public void Descriptors_MatchCatalogue(string id, DiagnosticSeverity severity, string title, string messageFormat)
    {
        var descriptor = SelectDiagnostics.Supported.Single(d => d.Id == id);

        descriptor.ShouldSatisfyAllConditions(
            d => d.DefaultSeverity.ShouldBe(severity),
            d => d.Title.ToString(null).ShouldBe(title),
            d => d.MessageFormat.ToString(null).ShouldBe(messageFormat),
            d => d.Category.ShouldBe("Ducky"),
            d => d.IsEnabledByDefault.ShouldBeTrue(),
            d => d.HelpLinkUri.ShouldBe($"https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{id}.md"));
    }

    [Fact]
    public void Descriptors_EverySelectDescriptorCatalogued() =>
        SelectDiagnostics.Supported.Select(d => d.Id).ShouldBe(Catalogue.Select(row => row.Data.Item1));

    [Fact]
    public void Descriptors_AnalyzerSupportsEverySelectDescriptor() =>
        new SelectInRenderAnalyzer().SupportedDiagnostics.ShouldBe(SelectDiagnostics.Supported);
}
