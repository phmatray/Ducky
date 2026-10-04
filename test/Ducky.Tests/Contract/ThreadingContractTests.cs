using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Ducky.Tests.Contract;

// SPEC §7: "This text appears verbatim in the XML docs of IDispatcher". Non-normative check: each of the ten rules of §7,
// with its Markdown (backticks, bold) and its number dropped, is in the remarks of IDispatcher in Ducky.xml, compared
// with whitespace collapsed.
public sealed partial class ThreadingContractTests
{
    [Fact]
    public void IDispatcher_XmlDocs_CarryThreadingContractVerbatim()
    {
        var docs = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Ducky.xml"), LoadOptions.PreserveWhitespace);
        var remarks = docs.Descendants("member")
            .Single(m => (string?)m.Attribute("name") == "T:Ducky.IDispatcher")
            .Element("remarks").ShouldNotBeNull();
        var xml = Normalize(remarks.Value);

        var rules = ThreadingRules().ToList();

        rules.Count.ShouldBe(10);
        rules.ShouldAllBe(rule => xml.Contains(rule, StringComparison.Ordinal));
    }

    private static IEnumerable<string> ThreadingRules()
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), "docs", "spec", "SPEC.md"));
        return lines
            .SkipWhile(line => line != "## 7. Threading contract")
            .TakeWhile(line => !line.StartsWith("Known liveness ceilings", StringComparison.Ordinal))
            .Where(line => RuleLine().IsMatch(line))
            .Select(line => Normalize(RuleLine().Replace(line, string.Empty).Replace("`", string.Empty, StringComparison.Ordinal).Replace("**", string.Empty, StringComparison.Ordinal)));
    }

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
    private static partial Regex RuleLine();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
