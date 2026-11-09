using System.Text.RegularExpressions;
using Fallout.Common.IO;

internal sealed partial class Build
{
    // The Ducky Playground's X-ray drawer shows the app's own .cs and .razor files in about 95
    // monospace columns on desktop: 100 is the hard limit, a tab counting as 4.
    private const int PlaygroundMaxColumns = 100;

    private const string PlaygroundDirectory = "samples/Ducky.Playground";

    // A drawer file in Xray/Features.cs, its arguments on one line or wrapped:
    // Source("Shop/Search/SearchEffect.cs", "Concurrency.Switch").
    private static readonly Regex PlaygroundFocusSource = new(@"\bSource\(\s*""([^""]+)"",\s*""((?:[^""\\]|\\.)*)""\)");

    // Every line of samples/Ducky.Playground/**/*.cs and *.razor (obj/ and bin/ excluded) wider than
    // PlaygroundMaxColumns. Also every Source(path, focus) of Xray/Features.cs whose file is missing or has no
    // line of code containing the focus: the drawer marks the first such line, and would otherwise mark none.
    private static IEnumerable<string> PlaygroundSourceViolations(AbsolutePath root)
    {
        var playground = root / "samples" / "Ducky.Playground";
        var sources = playground.GlobFiles("**/*.cs", "**/*.razor")
            .Select(file => (File: file, Path: playground.GetUnixRelativePathTo(file).ToString()))
            .Where(source => !source.Path.Split('/').Any(segment => segment is "bin" or "obj"))
            .OrderBy(source => source.Path, StringComparer.Ordinal);
        foreach (var (file, path) in sources)
        {
            var lines = file.ReadAllLines();
            for (var i = 0; i < lines.Length; i++)
            {
                var columns = lines[i].Sum(c => c == '\t' ? 4 : 1);
                if (columns > PlaygroundMaxColumns)
                {
                    yield return $"{PlaygroundDirectory}/{path}:{i + 1}: {columns} columns, over the drawer's " +
                        $"{PlaygroundMaxColumns} (tabs count 4)";
                }
            }
        }

        var features = playground / "Xray" / "Features.cs";
        if (!features.FileExists())
        {
            yield return $"{PlaygroundDirectory}/Xray/Features.cs: missing; the X-ray focus check reads its Source(path, focus) list";
            yield break;
        }
        var focuses = PlaygroundFocusSource.Matches(features.ReadAllText());
        if (focuses.Count == 0)
        {
            yield return $"{PlaygroundDirectory}/Xray/Features.cs: lists no Source(path, focus); the X-ray focus check found nothing to check";
        }
        foreach (Match focus in focuses)
        {
            var path = focus.Groups[1].Value;
            var text = focus.Groups[2].Value.Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
            var file = playground / path;
            if (!file.FileExists())
            {
                yield return $"{PlaygroundDirectory}/Xray/Features.cs: Source(\"{path}\", ...) names a file that does not exist";
            }
            else if (!PlaygroundCodeLines(file.ReadAllLines()).Any(line => line.Contains(text, StringComparison.Ordinal)))
            {
                yield return $"{PlaygroundDirectory}/Xray/Features.cs: no line of code in {path} contains the focus \"{text}\" " +
                    "(a comment does not count), so the drawer would mark no line";
            }
        }
    }

    // The lines that are not comments: // lines, and the lines of /* */ and Razor @* *@ blocks, are skipped, as the
    // drawer skips them when it looks for the focus.
    private static IEnumerable<string> PlaygroundCodeLines(IEnumerable<string> lines)
    {
        string? blockEnd = null;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (blockEnd is not null)
            {
                if (trimmed.Contains(blockEnd, StringComparison.Ordinal))
                {
                    blockEnd = null;
                }
                continue;
            }
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }
            var opener = trimmed.StartsWith("@*", StringComparison.Ordinal) ? "*@"
                : trimmed.StartsWith("/*", StringComparison.Ordinal) ? "*/"
                : null;
            if (opener is not null)
            {
                if (!trimmed[2..].Contains(opener, StringComparison.Ordinal))
                {
                    blockEnd = opener;
                }
                continue;
            }
            yield return line;
        }
    }

    // The rule planted in a throwaway playground: an over-long line (a tab counting 4) is reported with its file, line
    // and width; a clean file, a 100-column line and obj/, bin/ and wwwroot copies are not; a focus found only in a
    // comment, a missing file and a missing Features.cs are reported.
    private static IEnumerable<string> PlaygroundSourceSelfCheck()
    {
        static List<string> Run(Action<AbsolutePath> plant)
        {
            var root = (AbsolutePath)Path.Combine(Path.GetTempPath(), $"ducky-playground-lint-{Guid.NewGuid():N}");
            try
            {
                plant(root / "samples" / "Ducky.Playground");
                return [.. PlaygroundSourceViolations(root)];
            }
            finally
            {
                root.DeleteDirectory();
            }
        }

        var wide = new string('x', 101);
        var violations = Run(playground =>
        {
            (playground / "Clean.cs").WriteAllText(
                $"// {new string('x', 97)}\n{new string('x', 100)}\n\t{new string('x', 96)}\nclass Clean\n{{\n}}\n");
            (playground / "Shop" / "Wide.razor").WriteAllText($"@* only in a comment *@\n<p>\n{wide}\n</p>\n");
            (playground / "Tabbed.cs").WriteAllText($"\t\t{new string('x', 93)}\n");
            (playground / "obj" / "Generated.cs").WriteAllText($"{wide}\n");
            (playground / "bin" / "Release" / "Copy.razor").WriteAllText($"{wide}\n");
            (playground / "wwwroot" / "_source" / "Clean.cs.txt").WriteAllText($"{wide}\n");
            (playground / "Xray" / "Features.cs").WriteAllText(
                "[\n" +
                "    Source(\"Clean.cs\", \"class Clean\"),\n" +
                "    Source(\"Shop/Wide.razor\", \"only in a comment\"),\n" +
                "    Source(\"Gone.cs\", \"anything\"),\n" +
                "]\n");
        });
        string[] expected =
        [
            $"{PlaygroundDirectory}/Shop/Wide.razor:3: 101 columns",
            $"{PlaygroundDirectory}/Tabbed.cs:1: 101 columns",
            $"{PlaygroundDirectory}/Xray/Features.cs: no line of code in Shop/Wide.razor contains the focus \"only in a comment\"",
            $"{PlaygroundDirectory}/Xray/Features.cs: Source(\"Gone.cs\", ...) names a file that does not exist",
        ];
        foreach (var report in expected.Where(e => !violations.Any(v => v.StartsWith(e, StringComparison.Ordinal))))
        {
            yield return $"PlaygroundSourceSelfCheck: the planted playground must report '{report}', got [{string.Join("; ", violations)}]";
        }
        foreach (var violation in violations.Where(v => !expected.Any(e => v.StartsWith(e, StringComparison.Ordinal))))
        {
            yield return $"PlaygroundSourceSelfCheck: the clean files, the 100-column line and obj/, bin/ and wwwroot/ must pass, got: {violation}";
        }

        var withoutFeatures = Run(playground => (playground / "Clean.cs").WriteAllText("class Clean\n{\n}\n"));
        if (withoutFeatures.Count != 1 || !withoutFeatures[0].StartsWith($"{PlaygroundDirectory}/Xray/Features.cs: missing", StringComparison.Ordinal))
        {
            yield return $"PlaygroundSourceSelfCheck: a playground without Xray/Features.cs must report it once, got [{string.Join("; ", withoutFeatures)}]";
        }
    }
}
