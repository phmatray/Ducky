using Ducky.Generators.Analyzers.Purity;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Tests.Purity;

// Descriptors_MatchCatalogue rows of DUCKY001–003 (SPEC §16.2, §17.8): the verifier tests check IDs, locations and
// arguments only, so the strings are pinned here.
public sealed class PurityDescriptorsTests
{
    public static TheoryData<string, string, string> Catalogue => new()
    {
        { "DUCKY001", "Non-deterministic call in a reducer, mutator or projector", "'{0}' is non-deterministic in a {1}: read it outside the lambda and pass it in" },
        { "DUCKY002", "I/O or blocking in a reducer, mutator or projector", "'{0}' does I/O or blocks in a {1}: move it to an effect" },
        { "DUCKY003", "Mutation in a reducer, mutator or projector", "'{0}' mutates {1} in a {2}: return a new value instead" },
    };

    [Theory]
    [MemberData(nameof(Catalogue))]
    public void Descriptors_MatchCatalogue(string id, string title, string messageFormat)
    {
        var descriptor = new PurityAnalyzer().SupportedDiagnostics.Single(d => d.Id == id);

        descriptor.ShouldSatisfyAllConditions(
            d => d.DefaultSeverity.ShouldBe(DiagnosticSeverity.Warning),
            d => d.Title.ToString(null).ShouldBe(title),
            d => d.MessageFormat.ToString(null).ShouldBe(messageFormat),
            d => d.Category.ShouldBe("Ducky"),
            d => d.IsEnabledByDefault.ShouldBeTrue(),
            d => d.HelpLinkUri.ShouldBe($"https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{id}.md"));
    }

    [Fact]
    public void Descriptors_EveryPurityDescriptorCatalogued() =>
        new PurityAnalyzer().SupportedDiagnostics.Select(d => d.Id).ShouldBe(Catalogue.Select(row => row.Data.Item1));
}
