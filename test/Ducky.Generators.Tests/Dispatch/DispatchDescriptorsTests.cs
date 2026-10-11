using Ducky.Generators.Dispatch;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Tests.Dispatch;

// Descriptors_MatchCatalogue (SPEC §16.2) for the GEN-01 descriptors, so a descriptor string mutant dies here instead
// of surviving the verifier tests, which check only IDs, locations and arguments.
public sealed class DispatchDescriptorsTests
{
    public static TheoryData<string, DiagnosticSeverity, string, string> Catalogue => new()
    {
        { "DUCKY020", DiagnosticSeverity.Error, "Invalid [DuckyAction] type", "'{0}' gets no dispatch helper: {1}" },
        { "DUCKY021", DiagnosticSeverity.Warning, "Dispatch helper name collides with a store member", "The dispatch helpers of '{0}' are skipped: '{1}' or '{1}Async' is a member of IDispatcher, IStore, EffectContext or object, which C# binds before an extension method; set MethodName" },
        { "DUCKY024", DiagnosticSeverity.Info, "Ambiguous dispatch helpers", "The dispatch helper '{0}' of '{1}' has the same name and parameter types as the one of '{2}': a call that sees both is ambiguous (CS0121); set MethodName on one of them" },
    };

    [Theory]
    [MemberData(nameof(Catalogue))]
    public void Descriptors_MatchCatalogue(string id, DiagnosticSeverity severity, string title, string messageFormat)
    {
        var descriptor = DispatchDiagnostics.All.Single(d => d.Id == id);

        descriptor.ShouldSatisfyAllConditions(
            d => d.DefaultSeverity.ShouldBe(severity),
            d => d.Title.ToString(null).ShouldBe(title),
            d => d.MessageFormat.ToString(null).ShouldBe(messageFormat),
            d => d.Category.ShouldBe("Ducky"),
            d => d.IsEnabledByDefault.ShouldBeTrue(),
            d => d.HelpLinkUri.ShouldBe($"https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{id}.md"));
    }

    [Fact]
    public void Descriptors_EveryDispatchDescriptorCatalogued() =>
        DispatchDiagnostics.All.Select(d => d.Id).ShouldBe(Catalogue.Select(row => row.Data.Item1));
}
