using Ducky.Generators.Analyzers.Structural;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Tests.Structural;

// Descriptors_MatchCatalogue (SPEC §16.2) for DUCKY005 and DUCKY011–015, so a descriptor string mutant dies here
// instead of surviving the verifier tests.
public sealed class StructuralDescriptorsTests
{
    public static TheoryData<string, DiagnosticSeverity, string, string> Catalogue => new()
    {
        { "DUCKY005", DiagnosticSeverity.Error, "Handler for a non-concrete action type", "'{0}' is abstract, an interface or object, so exact-type matching never runs this handler: use a concrete action type" },
        { "DUCKY011", DiagnosticSeverity.Info, "Reducer copies the state unchanged", "'{0}' is a new reference, so the slice notifies on every action: return the state itself when nothing changes" },
        { "DUCKY012", DiagnosticSeverity.Info, "Persisted slice relies on its derived key", "'{0}' is persisted under its derived Key: override Key so a rename doesn't orphan the stored data" },
        { "DUCKY013", DiagnosticSeverity.Warning, "Concurrency key without value equality", "ConcurrencyKey returns '{0}', which has no value equality, so no two runs share a key: return a record, a value type or a type that overrides Equals" },
        { "DUCKY014", DiagnosticSeverity.Error, "Duplicate handler for an action type", "On<{0}> is already registered in this constructor: register one handler per action type" },
        { "DUCKY015", DiagnosticSeverity.Error, "Two slices share a state type", "'{0}' shares the state type '{1}' with '{2}': each state type maps to exactly one slice" },
    };

    [Theory]
    [MemberData(nameof(Catalogue))]
    public void Descriptors_MatchCatalogue(string id, DiagnosticSeverity severity, string title, string messageFormat)
    {
        var descriptor = StructuralDiagnostics.All.Single(d => d.Id == id);

        descriptor.ShouldSatisfyAllConditions(
            d => d.DefaultSeverity.ShouldBe(severity),
            d => d.Title.ToString(null).ShouldBe(title),
            d => d.MessageFormat.ToString(null).ShouldBe(messageFormat),
            d => d.Category.ShouldBe("Ducky"),
            d => d.IsEnabledByDefault.ShouldBeTrue(),
            d => d.HelpLinkUri.ShouldBe($"https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{id}.md"));
    }

    [Fact]
    public void Descriptors_EveryStructuralDescriptorCatalogued() =>
        StructuralDiagnostics.All.Select(d => d.Id).ShouldBe(Catalogue.Select(row => row.Data.Item1));

    // DUCKY015 is reported at compilation end, which the IDE runs only on full analysis (RS1037).
    [Fact]
    public void Descriptors_SharedStateIsCompilationEnd() =>
        StructuralDiagnostics.All.Where(d => d.CustomTags.Contains(WellKnownDiagnosticTags.CompilationEnd)).Select(d => d.Id).ShouldBe(["DUCKY015"]);

    [Fact]
    public void Analyzer_SupportsEveryStructuralDescriptor() =>
        new StructuralAnalyzer().SupportedDiagnostics.ShouldBe(StructuralDiagnostics.All);
}
