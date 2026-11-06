using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Ducky.Docs.Tests.Core;

// docs/threading.md (M15-02): SPEC §7 "appears verbatim in the XML docs of IDispatcher and in docs/threading.md", and the
// page states the known liveness ceilings.
public sealed partial class ThreadingDocTests
{
    private const string SpecHeading = "## 7. Threading contract";
    private const string CeilingsLead = "Known liveness ceilings";

    // Non-normative: the page's ten rules are the IDispatcher remarks' list items in Ducky.xml, one for one and in order
    // (Markdown dropped, whitespace collapsed).
    [Fact]
    public void ThreadingDoc_MatchesIDispatcherXmlDocs()
    {
        var docs = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Ducky.xml"), LoadOptions.PreserveWhitespace);
        var remarks = docs.Descendants("member")
            .Single(m => (string?)m.Attribute("name") == "T:Ducky.IDispatcher")
            .Element("remarks").ShouldNotBeNull();
        var items = remarks.Descendants("item").Select(item => Normalize(item.Value)).ToList();

        var rules = RuleLines(ThreadingDoc()).Select(line => Normalize(StripMarkdown(RuleNumber().Replace(line, string.Empty)))).ToList();

        rules.Count.ShouldBe(10);
        items.ShouldBe(rules);
    }

    // Non-normative: the rules are SPEC §7's lines verbatim, in order, and each liveness ceiling is stated as the spec words it.
    [Fact]
    public void ThreadingDoc_CarriesSpecTextAndLivenessCeilings()
    {
        var page = ThreadingDoc();
        var section = File.ReadAllLines(Path.Combine(RepositoryRoot(), "docs", "spec", "SPEC.md"))
            .SkipWhile(line => line != SpecHeading).Skip(1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal) && line != "---")
            .ToList();
        var ceilings = section.SkipWhile(line => !line.StartsWith(CeilingsLead, StringComparison.Ordinal))
            .Where(line => line.StartsWith("- ponytail: ", StringComparison.Ordinal))
            .Select(line => "- " + line["- ponytail: ".Length..])
            .ToList();

        RuleLines(page).ShouldBe(RuleLines(section));
        ceilings.Count.ShouldBe(2);
        ceilings.ShouldAllBe(ceiling => page.Contains(ceiling));
    }

    private static List<string> ThreadingDoc() => [.. File.ReadAllLines(Path.Combine(RepositoryRoot(), "docs", "threading.md"))];

    private static List<string> RuleLines(IEnumerable<string> lines) =>
        [.. lines.TakeWhile(line => !line.StartsWith(CeilingsLead, StringComparison.Ordinal)).Where(line => RuleNumber().IsMatch(line))];

    private static string StripMarkdown(string text) =>
        text.Replace("`", string.Empty, StringComparison.Ordinal).Replace("**", string.Empty, StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "docs", "spec", "SPEC.md")))
        {
            directory = directory.Parent.ShouldNotBeNull("docs/spec/SPEC.md not found above the test output");
        }

        return directory.FullName;
    }

    private static string Normalize(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"^\d+\. ")]
    private static partial Regex RuleNumber();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
