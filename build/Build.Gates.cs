using System.Text.RegularExpressions;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

internal sealed partial class Build
{
    // §17.1: `// Stryker disable [once] <mutators> : <reason>`, `#pragma warning disable <ids> // justification: <why>`.
    private static readonly (Regex Pattern, string Rule)[] ExclusionRules =
    [
        // Stryker's own parser takes zero spaces and any case; '*' stops the reason search at a block comment's end.
        (new Regex(@"Stryker\s*disable(?![^:*\r\n]*:\s*[^\s*])", RegexOptions.IgnoreCase), "Stryker disable without ': <reason>'"),
        (new Regex(@"#\s*pragma\s+warning\s+disable(?!.*//\s*justification:\s*\S)"), "#pragma warning disable without '// justification: <why>'"),
    ];

    // Coverage exclusions are banned APIs (BannedSymbols.txt); the coverage-settings and stryker-config.json checks
    // arrive with those files (M0-04, M0-05).
    private Target ExclusionGate => _ => _
        .Executes(() =>
        {
            var violations =
                from file in (RootDirectory / "src").GlobFiles("**/*.cs")
                let relative = RootDirectory.GetRelativePathTo(file).ToString().Replace('\\', '/')
                where !relative.Contains("/obj/", StringComparison.Ordinal) && !relative.Contains("/bin/", StringComparison.Ordinal)
                from line in file.ReadAllLines().Select((text, index) => (text, number: index + 1))
                from rule in ExclusionRules
                where rule.Pattern.IsMatch(line.text)
                select $"{relative}:{line.number}: {rule.Rule}: {line.text.Trim()}";

            var list = violations.ToList();
            list.ForEach(v => Log.Error(v));
            Assert.True(list.Count == 0, $"ExclusionGate: {list.Count} unjustified exclusion(s) in src/");
        });
}
