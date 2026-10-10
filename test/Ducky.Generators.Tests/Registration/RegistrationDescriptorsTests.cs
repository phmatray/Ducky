using Ducky.Generators.Registration;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Tests.Registration;

// Descriptors_MatchCatalogue (SPEC §16.2) for the GEN-02 descriptors, so a descriptor string mutant dies here instead
// of surviving the diagnostic tests.
public sealed class RegistrationDescriptorsTests
{
    public static TheoryData<string, DiagnosticSeverity, string, string> Catalogue => new()
    {
        { "DUCKY022", DiagnosticSeverity.Warning, "Registration call not emitted", "[{0}] on '{1}' is ignored: {2}" },
        { "DUCKY023", DiagnosticSeverity.Warning, "Type excluded from generated registration", "'{0}' is not registered by AddDuckyGenerated: {1}" },
    };

    [Theory]
    [MemberData(nameof(Catalogue))]
    public void Descriptors_MatchCatalogue(string id, DiagnosticSeverity severity, string title, string messageFormat)
    {
        var descriptor = RegistrationDiagnostics.All.Single(d => d.Id == id);

        descriptor.ShouldSatisfyAllConditions(
            d => d.DefaultSeverity.ShouldBe(severity),
            d => d.Title.ToString(null).ShouldBe(title),
            d => d.MessageFormat.ToString(null).ShouldBe(messageFormat),
            d => d.Category.ShouldBe("Ducky"),
            d => d.IsEnabledByDefault.ShouldBeTrue(),
            d => d.HelpLinkUri.ShouldBe($"https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{id}.md"));
    }

    [Fact]
    public void Descriptors_EveryRegistrationDescriptorCatalogued() =>
        RegistrationDiagnostics.All.Select(d => d.Id).ShouldBe(Catalogue.Select(row => row.Data.Item1));
}
