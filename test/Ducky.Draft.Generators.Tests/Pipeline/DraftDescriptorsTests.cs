using Microsoft.CodeAnalysis;

namespace Ducky.Draft.Generators.Tests.Pipeline;

// Descriptors_MatchCatalogue (SPEC §16.2) for the Draft generator descriptors, so a descriptor string mutant dies here
// instead of surviving the verifier tests, which check only IDs, locations and arguments.
public sealed class DraftDescriptorsTests
{
    public static TheoryData<string, DiagnosticSeverity, string, string> Catalogue => new()
    {
        { "DUCKY101", DiagnosticSeverity.Error, "[Draftable] type is not a partial record class", "'{0}' gets no draft: [Draftable] supports only a partial record class that is not file-local (not a record struct, a class, a non-partial record or a file-local type)" },
        { "DUCKY102", DiagnosticSeverity.Error, "Containing type of a [Draftable] record is not partial", "'{0}' gets no draft: its containing type '{1}' is not partial" },
        { "DUCKY103", DiagnosticSeverity.Error, "Generic [Draftable] record", "'{0}' gets no draft: it is generic or nested in a generic type, which Ducky.Draft 2.0 does not support" },
        { "DUCKY104", DiagnosticSeverity.Error, "[Draftable] record declares a generated member name", "'{0}' gets no draft: it already declares a member named '{1}', which the generator emits" },
        { "DUCKY105", DiagnosticSeverity.Warning, "[Draftable] record member of a mutable collection type", "'{0}' is a plain member of the draft: its type '{1}' is a mutable collection, so structural sharing and immutability can't be guaranteed" },
        { "DUCKY106", DiagnosticSeverity.Warning, "Draft escapes its Produce recipe", "'{0}' escapes its Produce recipe: the draft is revoked when Produce returns, so any later use throws ObjectDisposedException" },
        { "DUCKY107", DiagnosticSeverity.Info, "Member type is [Draftable] in metadata but has no compatible Draft", "'{0}' gets no draft of '{1}': '{1}' is [Draftable] in another assembly but has no nested public Draft implementing IDraft<{1}> with a public Draft({1}) constructor; it is edited by assignment" },
        { "DUCKY108", DiagnosticSeverity.Warning, "[Draftable] record is error-obsolete", "'{0}' gets no draft: it is marked [Obsolete(error: true)], so generated code can't reference it (CS0619)" },
        { "DUCKY109", DiagnosticSeverity.Warning, "Result of a draft member's method discarded", "result discarded: assign it, {0} = {0}.{1}(...)" },
    };

    [Theory]
    [MemberData(nameof(Catalogue))]
    public void Descriptors_MatchCatalogue(string id, DiagnosticSeverity severity, string title, string messageFormat)
    {
        var descriptor = DraftDiagnostics.All.Single(d => d.Id == id);

        descriptor.ShouldSatisfyAllConditions(
            d => d.DefaultSeverity.ShouldBe(severity),
            d => d.Title.ToString(null).ShouldBe(title),
            d => d.MessageFormat.ToString(null).ShouldBe(messageFormat),
            d => d.Category.ShouldBe("Ducky"),
            d => d.IsEnabledByDefault.ShouldBeTrue(),
            d => d.HelpLinkUri.ShouldBe($"https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{id}.md"));
    }

    [Fact]
    public void Descriptors_EveryDraftDescriptorCatalogued() =>
        DraftDiagnostics.All.Select(d => d.Id).ShouldBe(Catalogue.Select(row => row.Data.Item1));
}
