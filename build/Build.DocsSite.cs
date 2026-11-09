using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fallout.Common;
using Fallout.Common.IO;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Serilog;
using YamlDotNet.RepresentationModel;

// The documentation site (SPEC §19 Docs): docs/**/*.md through docs/nav.yml and site/docs-theme/page.html into static
// pages. Every code include becomes the region's text with a link to its source lines; every link, anchor and nav entry
// is checked, so a broken page fails the build instead of shipping.
internal sealed partial class Build
{
    private const string DocsRepositoryUrl = "https://github.com/phmatray/Ducky";
    private const string DocsBranch = "v2";
    private const string DocsSiteUrl = "https://phmatray.github.io/Ducky/docs/";

    // Pages under these prefixes are design records or sample projects, not user docs.
    private static readonly string[] DocsSiteExcluded = ["spec/", "adr/", "migration/samples/"];

    internal sealed record DocsSiteInput(
        IReadOnlyDictionary<string, string> Pages,
        string Nav,
        string Template,
        Func<string, string?> ReadRepoFile,
        Func<string, bool> RepoPathExists);

    internal sealed record DocsSiteOutput(IReadOnlyDictionary<string, string> Files, IReadOnlyList<string> Violations);

    private sealed record NavGroup(string? Title, List<string> Pages);

    private sealed record NavSection(string Tab, List<NavGroup> Groups);

    private sealed record DocHeading(int Level, string Id, string Html, string Text);

    private sealed record DocPage(
        string Path,
        string TitleHtml,
        string TitleText,
        string Description,
        string BodyHtml,
        List<DocHeading> Headings,
        HashSet<string> Ids,
        List<(string Id, string Heading, string Text)> SearchSections,
        string Markdown);

    private sealed record PendingAnchor(string From, int Line, string Target, string Anchor);

    // Pages: docs-relative path ("guides/core/effects.md") -> Markdown. Returns output-relative files (".html" pages and
    // "_assets/search.json") and the violations; the caller copies the static assets and writes the files.
    internal static DocsSiteOutput RenderDocsSite(DocsSiteInput input)
    {
        var violations = new List<string>();
        var (sections, unlisted) = ParseDocsNav(input.Nav, input.Pages.Keys, violations);
        var listed = sections.SelectMany(s => s.Groups).SelectMany(g => g.Pages).ToList();
        foreach (var path in input.Pages.Keys.Order(StringComparer.Ordinal))
        {
            if (!listed.Contains(path) && !unlisted.Contains(path))
            {
                violations.Add($"docs/nav.yml: docs/{path} is neither on a tab nor unlisted");
            }
        }

        var pending = new List<PendingAnchor>();
        var pages = new Dictionary<string, DocPage>(StringComparer.Ordinal);
        foreach (var path in listed.Concat(unlisted).Distinct().Where(input.Pages.ContainsKey))
        {
            pages[path] = RenderDocPage(path, input.Pages[path], input, pending, violations);
        }
        foreach (var p in pending)
        {
            if (pages.TryGetValue(p.Target, out var target) && !target.Ids.Contains(p.Anchor))
            {
                violations.Add($"docs/{p.From}:{p.Line}: anchor '#{p.Anchor}' does not exist in docs/{p.Target}");
            }
        }

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var page in pages.Values)
        {
            files[DocsHtmlPath(page.Path)] = FillDocsTemplate(input.Template, page, sections, unlisted, listed, pages);
            files[page.Path] = page.Markdown;
        }
        files["_assets/search.json"] = DocsSearchIndex(sections, pages);
        files["llms-full.txt"] = DocsLlmsFull(listed, pages);
        return new DocsSiteOutput(files, violations);
    }

    private static DocsSiteInput DocsSiteInputFrom(AbsolutePath root, IEnumerable<(string Path, string Text)> docs) => new(
        docs.Where(d => !DocsSiteExcluded.Any(e => d.Path.StartsWith("docs/" + e, StringComparison.Ordinal)))
            .ToDictionary(d => d.Path["docs/".Length..], d => d.Text, StringComparer.Ordinal),
        (root / "docs" / "nav.yml").ReadAllText(),
        (root / "site" / "docs-theme" / "page.html").ReadAllText(),
        path => root / path is var file && file.FileExists() ? file.ReadAllText() : null,
        path => root / path is var entry && (entry.FileExists() || entry.DirectoryExists()));

    // The pages and search index, then the theme (docs.css, docs.js), the shared tokens, faces and mark of site/.
    private void WriteDocsSite(DocsSiteOutput site, AbsolutePath output)
    {
        output.CreateOrCleanDirectory();
        foreach (var (path, content) in site.Files)
        {
            (output / path).WriteAllText(content);
        }
        var assets = output / "_assets";
        var theme = RootDirectory / "site" / "docs-theme";
        foreach (var file in theme.GlobFiles("*.css", "*.js"))
        {
            file.Copy(assets / file.Name, ExistsPolicy.FileOverwrite);
        }
        (RootDirectory / "site" / "tokens.css").Copy(assets / "tokens.css", ExistsPolicy.FileOverwrite);
        (RootDirectory / "site" / "duck.svg").Copy(assets / "duck.svg", ExistsPolicy.FileOverwrite);
        foreach (var font in (RootDirectory / "site" / "fonts").GlobFiles("*.woff2", "OFL.txt"))
        {
            font.Copy(assets / "fonts" / font.Name, ExistsPolicy.FileOverwrite);
        }
        // What page.html and tokens.css load: a missing one would ship a page without its face, script or style.
        string[] required =
        [
            "tokens.css", "docs.css", "docs.js", "duck.svg", "search.json",
            "fonts/anybody.woff2", "fonts/atkinson-next.woff2", "fonts/jetbrains-mono.woff2", "fonts/OFL.txt",
        ];
        var missing = required.Where(f => !(assets / f).FileExists()).ToList();
        Assert.True(missing.Count == 0, $"Docs: missing site assets: {string.Join(", ", missing)}");
        Log.Information("Docs: {Count} pages in {Output}", site.Files.Count(f => f.Key.EndsWith(".html", StringComparison.Ordinal)), output);
    }

    // The site checks, planted on miniature docs: each planted defect must be reported, the clean set must pass, and the
    // include, link and nav rewrites must produce what the pages rely on.
    private static IEnumerable<string> DocsSiteSelfCheck()
    {
        const string template = "<title>{{title-text}}</title><nav>{{tabs}}</nav><aside>{{ledger}}</aside>{{toc}}<main>{{title}}{{body}}</main>{{pager}}";
        const string nav = "sections:\n  - tab: Start\n    groups:\n      - pages:\n          - index.md\n          - guides/a.md\n  - tab: Codes\n    groups:\n      - title: All\n        pages:\n          - diagnostics/D*.md\nunlisted:\n  - old.md\n";
        var repo = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test/Ducky.Docs.Tests/A.cs"] = "class A\n{\n    #region a-region\n    public int X => 1;\n    #region inner\n    // inner\n    #endregion\n    #endregion\n    #region two-raw\n    var a = \"\"\"\n        one\n        \"\"\";\n    var b = \"\"\"\n        two\n        \"\"\";\n    #endregion\n}\n",
            ["README.md"] = "# Readme\n\n## Install\n",
        };
        Dictionary<string, string> Clean() => new(StringComparer.Ordinal)
        {
            ["index.md"] = "# Home\n\nSee [the guide](guides/a.md#usage) and [DUCKY001](diagnostics/DUCKY001.md).\n",
            ["guides/a.md"] = "# Guide `A`\n\nIntro.\n\n## Usage\n\n[!code-csharp[](../../test/Ducky.Docs.Tests/A.cs#a-region)]\n\n## More\n\nSee [the test](../../test/Ducky.Docs.Tests/A.cs) and [home](../index.md).\n",
            ["diagnostics/DUCKY001.md"] = "# DUCKY001: Something\n\nText.\n",
            ["old.md"] = "# Old &amp; gone\n\nBack [home](index.md), to [the docs](./) and to [install](../README.md#install) or [line 3](../test/Ducky.Docs.Tests/A.cs#L3).\n\n````markdown\n[!code-csharp[](test/Ducky.Docs.Tests/A.cs#a-region)]\n````\n\n[!code-csharp[](../test/Ducky.Docs.Tests/A.cs#two-raw)]\n",
        };
        DocsSiteOutput Run(Dictionary<string, string> pages, string navText = nav) => RenderDocsSite(new DocsSiteInput(
            pages, navText, template, path => repo.GetValueOrDefault(path),
            path => repo.ContainsKey(path) || repo.Keys.Any(k => k.StartsWith(path + "/", StringComparison.Ordinal))));

        var clean = Run(Clean());
        foreach (var violation in clean.Violations)
        {
            yield return $"DocsSiteSelfCheck: the clean docs must pass, got: {violation}";
        }
        var guide = clean.Files.GetValueOrDefault("guides/a.html") ?? "";
        (string Expected, string Why)[] rendered =
        [
            ("<span class=\"tk-kw\">public</span> <span class=\"tk-kw\">int</span> <span class=\"tk-type\">X</span> =&gt; 1;\n<span class=\"tk-com\">// inner</span>", "the region's text, dedented, nested region lines dropped, highlighted"),
            ("https://github.com/phmatray/Ducky/blob/v2/test/Ducky.Docs.Tests/A.cs#L4-L6", "the snippet's source lines"),
            ("href=\"https://github.com/phmatray/Ducky/blob/v2/test/Ducky.Docs.Tests/A.cs\"", "a repository file as its GitHub URL"),
            ("href=\"../index.html\"", "a page link as its .html twin"),
            ("aria-current=\"page\">Guide <code>A</code>", "the current page in the ledger"),
            ("<a href=\"../diagnostics/DUCKY001.html\">Codes</a>", "a tab linking its first page"),
            ("<li class=\"l2\"><a href=\"#usage\">Usage</a>", "the on-this-page list"),
            ("rel=\"prev\" href=\"../index.html\"", "the previous page"),
            ("<title>Guide A</title>", "the plain-text title"),
        ];
        foreach (var (expected, why) in rendered)
        {
            if (!guide.Contains(expected, StringComparison.Ordinal))
            {
                yield return $"DocsSiteSelfCheck: guides/a.html must show {why} ('{expected}')";
            }
        }
        var twin = clean.Files.GetValueOrDefault("guides/a.md") ?? "";
        (string Expected, string Why)[] markdownTwin =
        [
            ("Source: https://github.com/phmatray/Ducky/blob/v2/test/Ducky.Docs.Tests/A.cs#L4-L6\n\n```csharp\npublic int X => 1;", "the snippet's source link, then the region's text"),
            ("[the test](https://github.com/phmatray/Ducky/blob/v2/test/Ducky.Docs.Tests/A.cs)", "a repository file as its GitHub URL"),
            ("[home](../index.md)", "a page link kept as its relative .md twin"),
        ];
        foreach (var (expected, why) in markdownTwin)
        {
            if (!twin.Contains(expected, StringComparison.Ordinal))
            {
                yield return $"DocsSiteSelfCheck: guides/a.md must show {why} ('{expected}')";
            }
        }
        var full = clean.Files.GetValueOrDefault("llms-full.txt") ?? "";
        if (!full.Contains("# Guide `A`", StringComparison.Ordinal) || full.Contains("# Old", StringComparison.Ordinal))
        {
            yield return "DocsSiteSelfCheck: llms-full.txt holds every listed page and no unlisted one";
        }
        var llms = LlmsTxtViolations("- [a](docs/guides/a.md)\n- [b](docs/guides/a.html#x)\n- [p](playground/)\n- [gone](docs/gone.md)\n- [x](https://x.org/y)\n",
            clean.Files.Keys.ToList(), _ => false).ToList();
        if (llms.Count != 1 || !llms[0].Contains("'docs/gone.md'", StringComparison.Ordinal))
        {
            yield return $"DocsSiteSelfCheck: llms.txt must report only its missing page, got [{string.Join("; ", llms)}]";
        }
        var old = clean.Files.GetValueOrDefault("old.html") ?? "";
        (string Expected, string Why)[] unlistedPage =
        [
            ("<title>Old &amp; gone</title>", "an unlisted page, rendered, its entity decoded once in the plain-text title"),
            ("href=\"index.html\">the docs</a>", "a link to the docs folder as its index page"),
            ("href=\"https://github.com/phmatray/Ducky/blob/v2/README.md#install\"", "a checked heading anchor into a repository Markdown file"),
            ("[!code-csharp[](test/Ducky.Docs.Tests/A.cs#a-region)]", "an include inside a fenced block shown as written"),
            ("<span class=\"tk-kw\">var</span> a = <span class=\"tk-str\">&quot;&quot;&quot;\n    one", "a region of two raw strings shown whole, not unwrapped"),
        ];
        foreach (var (expected, why) in unlistedPage)
        {
            if (!old.Contains(expected, StringComparison.Ordinal))
            {
                yield return $"DocsSiteSelfCheck: old.html must show {why} ('{expected}')";
            }
        }
        if (!(clean.Files.GetValueOrDefault("diagnostics/DUCKY001.html") ?? "").Contains("<span class=\"id\">DUCKY001</span> Something", StringComparison.Ordinal))
        {
            yield return "DocsSiteSelfCheck: a diagnostic reads as its ID and summary in the ledger";
        }
        var search = clean.Files.GetValueOrDefault("_assets/search.json") ?? "";
        if (!search.Contains("\"u\":\"guides/a.html#usage\"", StringComparison.Ordinal))
        {
            yield return "DocsSiteSelfCheck: the search index has one entry per H2";
        }
        if (search.Contains("old.html", StringComparison.Ordinal))
        {
            yield return "DocsSiteSelfCheck: an unlisted page stays out of the search index";
        }

        (string Page, string Text, string Expected)[] planted =
        [
            ("guides/a.md", "# Guide\n\nA [missing page](missing.md).\n", "docs/guides/a.md:3: link 'missing.md' names 'docs/guides/missing.md', which does not exist"),
            ("guides/a.md", "# Guide\n\nA [bad anchor](../index.md#nope).\n", "docs/guides/a.md:3: anchor '#nope' does not exist in docs/index.md"),
            ("guides/a.md", "# Guide\n\nA [local anchor](#gone).\n", "docs/guides/a.md:3: anchor '#gone' does not exist in docs/guides/a.md"),
            ("guides/a.md", "# Guide\n\n[escape](../../../outside.md)\n", "docs/guides/a.md:3: link '../../../outside.md' leaves the repository"),
            ("guides/a.md", "Intro first.\n\n# Guide\n", "docs/guides/a.md:1: a page starts with its one H1 title"),
            ("guides/a.md", "# Guide\n\n# Second\n", "docs/guides/a.md:1: a page starts with its one H1 title (found 2)"),
            ("guides/a.md", "# Guide\n\n![shot](shot.png)\n", "docs/guides/a.md:3: image 'shot.png'"),
            ("guides/a.md", "# Guide\n\n![shot](https://example.com/shot.png)\n", "docs/guides/a.md:3: image 'https://example.com/shot.png'"),
            ("guides/a.md", "# Guide\n\nSee <a href=\"b.md\">b</a>.\n", "docs/guides/a.md:3: raw HTML link or image"),
            ("guides/a.md", "# Guide\n\n<img src=\"x.png\">\n", "docs/guides/a.md:3: raw HTML link or image"),
            ("guides/a.md", "# Guide\n\nSee [install](../../README.md#setup).\n", "docs/guides/a.md:3: anchor '#setup' does not exist in 'README.md'"),
            ("guides/a.md", "# Guide\n\nSee [the test](../../test/Ducky.Docs.Tests/A.cs#a-region).\n", "docs/guides/a.md:3: anchor '#a-region' does not exist in 'test/Ducky.Docs.Tests/A.cs'"),
            ("guides/a.md", "# Guide\n\nText [!code-csharp[](../../test/Ducky.Docs.Tests/A.cs#a-region)]\n", "docs/guides/a.md:3: a code include stands on its own line"),
            ("guides/a.md", "# Guide\n\n[!code-csharp[](../../test/Ducky.Docs.Tests/A.cs#nope)]\n", "docs/guides/a.md:3: code include '../../test/Ducky.Docs.Tests/A.cs#nope' resolves to no region"),
            ("stray.md", "# Stray\n", "docs/nav.yml: docs/stray.md is neither on a tab nor unlisted"),
        ];
        foreach (var (page, text, expected) in planted)
        {
            var pages = Clean();
            pages[page] = text;
            var violations = Run(pages).Violations;
            if (!violations.Any(v => v.Contains(expected, StringComparison.Ordinal)))
            {
                yield return $"DocsSiteSelfCheck: planted {page} must report '{expected}', got [{string.Join("; ", violations)}]";
            }
        }
        (string Nav, string Expected)[] plantedNav =
        [
            (nav.Replace("          - guides/a.md\n", "          - guides/a.md\n          - guides/a.md\n", StringComparison.Ordinal), "docs/nav.yml: docs/guides/a.md is listed more than once"),
            (nav.Replace("diagnostics/D*.md", "diagnostics/X*.md", StringComparison.Ordinal), "docs/nav.yml: tab 'Codes' entry 'diagnostics/X*.md' matches no page"),
        ];
        foreach (var (navText, expected) in plantedNav)
        {
            var violations = Run(Clean(), navText).Violations;
            if (!violations.Any(v => v.Contains(expected, StringComparison.Ordinal)))
            {
                yield return $"DocsSiteSelfCheck: a planted nav must report '{expected}', got [{string.Join("; ", violations)}]";
            }
        }
    }

    private static (List<NavSection> Sections, List<string> Unlisted) ParseDocsNav(
        string nav, IEnumerable<string> pagePaths, List<string> violations)
    {
        var all = pagePaths.Order(StringComparer.Ordinal).ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stream = new YamlStream();
        stream.Load(new StringReader(nav));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;

        List<string> Expand(YamlNode node, string where)
        {
            var result = new List<string>();
            foreach (var entry in ((YamlSequenceNode)node).Children.Select(c => ((YamlScalarNode)c).Value!))
            {
                var matches = entry.Contains('*', StringComparison.Ordinal)
                    ? all.Where(p => DocsGlob(entry).IsMatch(p)).ToList()
                    : all.Contains(entry) ? [entry] : [];
                if (matches.Count == 0)
                {
                    violations.Add($"docs/nav.yml: {where} entry '{entry}' matches no page");
                }
                foreach (var match in matches)
                {
                    if (!seen.Add(match))
                    {
                        violations.Add($"docs/nav.yml: docs/{match} is listed more than once");
                        continue;
                    }
                    result.Add(match);
                }
            }
            return result;
        }

        var sections = new List<NavSection>();
        foreach (var s in ((YamlSequenceNode)root.Children[new YamlScalarNode("sections")]).Children.Cast<YamlMappingNode>())
        {
            var tab = ((YamlScalarNode)s.Children[new YamlScalarNode("tab")]).Value!;
            var groups = ((YamlSequenceNode)s.Children[new YamlScalarNode("groups")]).Children.Cast<YamlMappingNode>()
                .Select(g => new NavGroup(
                    g.Children.TryGetValue(new YamlScalarNode("title"), out var title) ? ((YamlScalarNode)title).Value : null,
                    Expand(g.Children[new YamlScalarNode("pages")], $"tab '{tab}'")))
                .Where(g => g.Pages.Count > 0)
                .ToList();
            if (groups.Count == 0)
            {
                violations.Add($"docs/nav.yml: tab '{tab}' has no page");
                continue;
            }
            sections.Add(new NavSection(tab, groups));
        }
        var unlisted = root.Children.TryGetValue(new YamlScalarNode("unlisted"), out var u) ? Expand(u, "unlisted") : [];
        return (sections, unlisted);
    }

    private static Regex DocsGlob(string glob) =>
        new("^" + Regex.Escape(glob).Replace(@"\*", "[^/]*", StringComparison.Ordinal) + "$");

    private static string DocsHtmlPath(string mdPath) => mdPath[..^3] + ".html";

    // ---------- one page ----------

    private static DocPage RenderDocPage(
        string path, string markdown, DocsSiteInput input, List<PendingAnchor> pending, List<string> violations)
    {
        var (expanded, lineMap) = ExpandCodeIncludes(path, markdown, input.ReadRepoFile, violations);
        var pipeline = DocsPipeline();
        var document = Markdown.Parse(expanded, pipeline);
        int SourceLine(int expandedLine) => expandedLine < lineMap.Count ? lineMap[expandedLine] : lineMap[^1];

        var h1s = document.Descendants<HeadingBlock>().Where(h => h.Level == 1).ToList();
        if (h1s.Count != 1 || document.FirstOrDefault() != h1s.FirstOrDefault())
        {
            violations.Add($"docs/{path}:1: a page starts with its one H1 title (found {h1s.Count})");
        }

        foreach (var link in document.Descendants<LinkInline>())
        {
            link.Url = RewriteDocLink(path, link.Url ?? "", SourceLine(link.Line), link.IsImage, input, pending, violations);
        }
        foreach (var (html, line) in document.Descendants<HtmlInline>().Select(h => (h.Tag ?? "", h.Line))
                     .Concat(document.Descendants<HtmlBlock>().Select(b => (b.Lines.ToString(), b.Line))))
        {
            if (RawHtmlReference.IsMatch(html))
            {
                violations.Add($"docs/{path}:{SourceLine(line)}: raw HTML link or image; use a Markdown link, which this check follows");
            }
        }

        var headings = new List<DocHeading>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            var id = heading.GetAttributes().Id ?? "";
            ids.Add(id);
            headings.Add(new DocHeading(heading.Level, id, RenderInlines(heading, pipeline), InlineText(heading.Inline)));
        }

        var title = h1s.FirstOrDefault();
        if (title is not null)
        {
            document.Remove(title);
        }
        var description = document.OfType<ParagraphBlock>().Select(p => InlineText(p.Inline)).FirstOrDefault() ?? "";

        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var renderer = new HtmlRenderer(writer);
        pipeline.Setup(renderer);
        renderer.ObjectRenderers.Replace<CodeBlockRenderer>(new DocsCodeBlockRenderer());
        renderer.Render(document);
        var body = writer.ToString()
            .Replace("<table>", "<div class=\"table\" tabindex=\"0\"><table>", StringComparison.Ordinal)
            .Replace("</table>", "</table></div>", StringComparison.Ordinal);

        return new DocPage(
            path,
            title is null ? WebUtility.HtmlEncode(path) : RenderInlines(title, pipeline),
            title is null ? path : InlineText(title.Inline),
            Clip(description, 160),
            body,
            headings,
            ids,
            DocsSearchSections(document),
            ReaderMarkdown(path, expanded, input));
    }

    // The page as Markdown for readers and language models (llmstxt.org): includes already expanded, each snippet preceded
    // by its source link, links to repository files as GitHub URLs; links between pages stay relative .md links.
    private static string ReaderMarkdown(string page, string expanded, DocsSiteInput input)
    {
        var output = new StringBuilder();
        string? openFence = null;
        foreach (var line in expanded.Split('\n'))
        {
            var marker = FenceMarker.Match(line);
            if (marker.Success && openFence is null)
            {
                openFence = marker.Groups[1].Value;
                var source = Regex.Match(line, @"\bsrc=(\S+):(\d+)-(\d+)");
                if (source.Success)
                {
                    var prefix = line[..marker.Index] + line[marker.Index..(marker.Index + marker.Length - marker.Groups[1].Length)];
                    var url = $"{DocsRepositoryUrl}/blob/{DocsBranch}/{source.Groups[1].Value}#L{source.Groups[2].Value}-L{source.Groups[3].Value}";
                    output.Append(prefix).Append("Source: ").Append(url).Append('\n').Append(prefix).Append('\n');
                    output.Append(line[..source.Index].TrimEnd()).Append('\n');
                    continue;
                }
            }
            else if (marker.Success && marker.Groups[1].Value[0] == openFence![0] && marker.Groups[1].Value.Length >= openFence.Length
                && line[(marker.Index + marker.Length)..].Trim().Length == 0)
            {
                openFence = null;
            }
            output.Append(openFence is null ? ReaderLinks(page, line, input) : line).Append('\n');
        }
        return output.ToString().TrimEnd() + "\n";
    }

    private static readonly Regex MarkdownLink = new(@"(\]\()([^)\s]+)(\))");

    private static string ReaderLinks(string page, string line, DocsSiteInput input) => MarkdownLink.Replace(line, m =>
    {
        var url = m.Groups[2].Value;
        if (url.StartsWith('#') || Regex.IsMatch(url, "^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase))
        {
            return m.Value;
        }
        var hash = url.IndexOf('#', StringComparison.Ordinal);
        var target = hash < 0 ? url : url[..hash];
        var repoPath = ResolveDocLink("docs/" + page, target);
        if (repoPath is null || repoPath.StartsWith("docs/", StringComparison.Ordinal) && input.Pages.ContainsKey(repoPath["docs/".Length..]))
        {
            return m.Value;
        }
        var file = repoPath.TrimEnd('/');
        var kind = input.ReadRepoFile(file) is null ? "tree" : "blob";
        return $"{m.Groups[1].Value}{DocsRepositoryUrl}/{kind}/{DocsBranch}/{file}{(hash < 0 ? "" : url[hash..])}{m.Groups[3].Value}";
    });

    // Every listed page, in nav order, as one Markdown file (llmstxt.org's llms-full.txt).
    private static string DocsLlmsFull(List<string> listed, Dictionary<string, DocPage> pages)
    {
        var text = new StringBuilder("# Ducky documentation\n\n> Every page of the Ducky 2.0 documentation in reading order, as Markdown. Every snippet is a region of a compiled, tested file; its source link precedes it.\n");
        foreach (var path in listed.Where(pages.ContainsKey))
        {
            text.Append("\n---\n\n<!-- ").Append(DocsSiteUrl).Append(DocsHtmlPath(path)).Append(" -->\n\n").Append(pages[path].Markdown);
        }
        return text.ToString();
    }

    // The relative links of site/llms.txt name pages and files the published site has: a docs page or its .md twin (the
    // Docs output), or a file or folder of site/ (playground/ is the published sample, checked by docs.yml).
    internal static IEnumerable<string> LlmsTxtViolations(string llms, ICollection<string> docsFiles, Func<string, bool> siteHas) =>
        from m in MarkdownLink.Matches(llms)
        let url = m.Groups[2].Value
        where !Regex.IsMatch(url, "^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase) && !url.StartsWith('#')
        let path = url.Split('#')[0]
        let ok = path.StartsWith("docs/", StringComparison.Ordinal) ? docsFiles.Contains(path["docs/".Length..]) : path == "playground/" || siteHas(path)
        where !ok
        select $"site/llms.txt: link '{url}' names nothing the published site has";

    private static MarkdownPipeline DocsPipeline() => new MarkdownPipelineBuilder()
        .UseAlertBlocks()
        .UsePipeTables()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseEmphasisExtras()
        .UseListExtras()
        .UseTaskLists()
        .UseAutoLinks()
        .Build();

    // A link to another page becomes its .html twin (and its anchor is checked once every page is known); a link to a
    // repository file outside the site becomes its GitHub URL; a link that leaves the repository, or names a missing
    // file, page or anchor, is a violation.
    private static string RewriteDocLink(
        string page, string url, int line, bool image, DocsSiteInput input, List<PendingAnchor> pending, List<string> violations)
    {
        var at = $"docs/{page}:{line}";
        if (image)
        {
            violations.Add($"{at}: image '{url}': the docs site ships no images; use a diagram in text or link to the file");
            return url;
        }
        if (url.Length == 0 || Regex.IsMatch(url, "^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase))
        {
            return url;
        }
        var hash = url.IndexOf('#', StringComparison.Ordinal);
        var target = hash < 0 ? url : url[..hash];
        var anchor = hash < 0 ? null : url[(hash + 1)..];
        if (target.Length == 0)
        {
            pending.Add(new PendingAnchor(page, line, page, anchor!));
            return url;
        }
        var repoPath = ResolveDocLink("docs/" + page, target);
        if (repoPath is null)
        {
            violations.Add($"{at}: link '{url}' leaves the repository");
            return url;
        }
        var docsPath = repoPath == "docs" ? "" : repoPath.StartsWith("docs/", StringComparison.Ordinal) ? repoPath["docs/".Length..] : null;
        // A link to a docs folder that has a page of its own (docs/, docs/x/) is a link to its index page.
        var folderIndex = docsPath is null ? null : docsPath.TrimEnd('/') is "" ? "index.md" : docsPath.TrimEnd('/') + "/index.md";
        if (folderIndex is not null && (docsPath!.Length == 0 || target.EndsWith('/')) && input.Pages.ContainsKey(folderIndex))
        {
            docsPath = folderIndex;
        }
        if (docsPath is not null && docsPath.EndsWith(".md", StringComparison.Ordinal) && input.Pages.ContainsKey(docsPath)
            && !DocsSiteExcluded.Any(e => docsPath.StartsWith(e, StringComparison.Ordinal)))
        {
            if (anchor is not null)
            {
                pending.Add(new PendingAnchor(page, line, docsPath, anchor));
            }
            return RelativeDocsUrl(page, DocsHtmlPath(docsPath)) + (anchor is null ? "" : "#" + anchor);
        }
        var file = repoPath.TrimEnd('/');
        if (!input.RepoPathExists(file))
        {
            violations.Add($"{at}: link '{url}' names '{repoPath}', which does not exist");
            return url;
        }
        var text = input.ReadRepoFile(file);
        // An anchor into a repository file is a GitHub line (#L12, #L12-L30) or, in Markdown, one of its headings.
        if (anchor is not null && !Regex.IsMatch(anchor, "^L[0-9]+(-L[0-9]+)?$")
            && !(text is not null && file.EndsWith(".md", StringComparison.Ordinal) && MarkdownHeadingIds(text).Contains(anchor)))
        {
            violations.Add($"{at}: anchor '#{anchor}' does not exist in '{file}'");
        }
        return $"{DocsRepositoryUrl}/{(text is null ? "tree" : "blob")}/{DocsBranch}/{file}" + (anchor is null ? "" : "#" + anchor);
    }

    // The GitHub-style heading ids of a Markdown file (the ids GitHub renders, which a #anchor into it must name).
    private static HashSet<string> MarkdownHeadingIds(string markdown) =>
        Markdown.Parse(markdown, DocsPipeline()).Descendants<HeadingBlock>()
            .Select(h => h.GetAttributes().Id ?? "").ToHashSet(StringComparer.Ordinal);

    // Raw HTML escapes the link, anchor and image checks, so a page links and shows with Markdown only.
    private static readonly Regex RawHtmlReference = new(@"<\s*img\b|\b(?:href|src)\s*=", RegexOptions.IgnoreCase);

    private static string RelativeDocsUrl(string fromPage, string toPath)
    {
        var depth = fromPage.Count(c => c == '/');
        return string.Concat(Enumerable.Repeat("../", depth)) + toPath;
    }

    private static string RenderInlines(LeafBlock block, MarkdownPipeline pipeline)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var renderer = new HtmlRenderer(writer) { ImplicitParagraph = true };
        pipeline.Setup(renderer);
        renderer.WriteLeafInline(block);
        return writer.ToString();
    }

    private static string InlineText(ContainerInline? inline)
    {
        if (inline is null)
        {
            return "";
        }
        var text = new StringBuilder();
        foreach (var node in inline.Descendants())
        {
            switch (node)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case HtmlEntityInline entity:
                    text.Append(entity.Transcoded.ToString());
                    break;
                case AutolinkInline autolink:
                    text.Append(autolink.Url);
                    break;
                case LineBreakInline:
                    text.Append(' ');
                    break;
            }
        }
        return text.ToString();
    }

    private static string Clip(string text, int max) =>
        text.Length <= max ? text : text[..text.LastIndexOf(' ', max - 1)].TrimEnd(',', ';', ':') + "…";

    // The page's prose split at each H2 (the part before the first H2 under the page title), for the search index.
    private static List<(string Id, string Heading, string Text)> DocsSearchSections(MarkdownDocument document)
    {
        var sections = new List<(string Id, string Heading, string Text)>();
        var id = "";
        var heading = "";
        var text = new StringBuilder();
        void Flush()
        {
            if (text.Length > 0 || heading.Length > 0)
            {
                sections.Add((id, heading, Clip(Regex.Replace(text.ToString(), @"\s+", " ").Trim(), 600)));
            }
            text.Clear();
        }
        foreach (var block in document.Descendants<LeafBlock>())
        {
            if (block is HeadingBlock { Level: 2 } h2)
            {
                Flush();
                id = h2.GetAttributes().Id ?? "";
                heading = InlineText(h2.Inline);
            }
            else if (block is not CodeBlock)
            {
                text.Append(InlineText(block.Inline)).Append(' ');
            }
        }
        Flush();
        return sections;
    }

    // ---------- code includes ----------

    // `[!code-csharp[](path#region)]` on its own line becomes a fenced block of the region's text (its nested region
    // lines dropped, dedented, a raw string literal's quotes unwrapped) whose info string carries the source lines. The
    // returned map gives each expanded line (0-based) its line in the original page (1-based).
    private static (string Text, List<int> LineMap) ExpandCodeIncludes(
        string page, string markdown, Func<string, string?> read, List<string> violations)
    {
        var output = new List<string>();
        var map = new List<int>();
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        string? openFence = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            // Inside a openFenced block an include is the syntax being shown, not an include.
            var marker = FenceMarker.Match(line);
            if (marker.Success && openFence is null)
            {
                openFence = marker.Groups[1].Value;
            }
            else if (marker.Success && marker.Groups[1].Value[0] == openFence![0] && marker.Groups[1].Value.Length >= openFence.Length
                && line[(marker.Index + marker.Length)..].Trim().Length == 0)
            {
                openFence = null;
                output.Add(line);
                map.Add(i + 1);
                continue;
            }
            var match = openFence is null ? CodeInclude.Match(line) : Match.Empty;
            if (!match.Success)
            {
                output.Add(line);
                map.Add(i + 1);
                continue;
            }
            var at = $"docs/{page}:{i + 1}";
            var prefix = line[..match.Index];
            if (prefix.Trim().Length > 0 || line[(match.Index + match.Length)..].Trim().Length > 0)
            {
                violations.Add($"{at}: a code include stands on its own line");
                output.Add(line);
                map.Add(i + 1);
                continue;
            }
            var language = match.Groups[1].Value.ToLowerInvariant() is "" or "cs" ? "csharp" : match.Groups[1].Value.ToLowerInvariant();
            var target = ResolveDocLink("docs/" + page, match.Groups[2].Value);
            var region = match.Groups[3].Value.TrimStart('#');
            var source = target is null ? null : read(target);
            var snippet = source is null || region.Length == 0 ? null : ExtractRegion(source, region);
            if (snippet is null)
            {
                // DocsRegionViolations names the exact cause; this keeps the page renderable.
                violations.Add($"{at}: code include '{match.Groups[2].Value}#{region}' resolves to no region");
                continue;
            }
            var fence = new string('`', Math.Max(3, LongestRun(snippet.Value.Text, '`') + 1));
            output.Add($"{prefix}{fence}{language} src={target}:{snippet.Value.First}-{snippet.Value.Last}");
            map.Add(i + 1);
            foreach (var codeLine in snippet.Value.Text.Split('\n'))
            {
                output.Add(codeLine.Length == 0 ? "" : prefix + codeLine);
                map.Add(i + 1);
            }
            output.Add(prefix + fence);
            map.Add(i + 1);
        }
        return (string.Join('\n', output), map);
    }

    // An opening or closing code fence: up to three spaces (any prefix inside a list), then three or more ` or ~.
    private static readonly Regex FenceMarker = new(@"^[ \t>]*(`{3,}|~{3,})");

    private static int LongestRun(string text, char c)
    {
        int longest = 0, run = 0;
        foreach (var ch in text)
        {
            run = ch == c ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }
        return longest;
    }

    private static readonly Regex RegionStart = new(@"^[ \t]*#region\b");
    private static readonly Regex RegionEnd = new(@"^[ \t]*#endregion\b");

    // The lines inside `#region name` up to its own `#endregion` (1-based first and last source line of the text).
    internal static (string Text, int First, int Last)? ExtractRegion(string source, string region)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var open = new Regex($@"^[ \t]*#region[ \t]+{Regex.Escape(region)}[ \t]*$");
        var start = Array.FindIndex(lines, l => open.IsMatch(l));
        if (start < 0)
        {
            return null;
        }
        var body = new List<(string Line, int Number)>();
        var depth = 1;
        var end = start + 1;
        for (; end < lines.Length; end++)
        {
            if (RegionStart.IsMatch(lines[end]))
            {
                depth++;
                continue;
            }
            if (RegionEnd.IsMatch(lines[end]) && --depth == 0)
            {
                break;
            }
            if (!RegionEnd.IsMatch(lines[end]))
            {
                body.Add((lines[end], end + 1));
            }
        }
        if (depth != 0)
        {
            return null;
        }
        while (body.Count > 0 && body[0].Line.Trim().Length == 0)
        {
            body.RemoveAt(0);
        }
        while (body.Count > 0 && body[^1].Line.Trim().Length == 0)
        {
            body.RemoveAt(body.Count - 1);
        }
        if (body.Count == 0)
        {
            return null;
        }

        // A region around one raw string literal (the analyzer examples) shows the literal's content.
        if (body.Count >= 2 && body[0].Line.TrimEnd().EndsWith("\"\"\"", StringComparison.Ordinal)
            && body[^1].Line.Trim() is "\"\"\";" or "\"\"\"" or "\"\"\")" or "\"\"\");" or "\"\"\","
            && !body.Skip(1).SkipLast(1).Any(b => b.Line.Contains("\"\"\"", StringComparison.Ordinal)))
        {
            body = body.Skip(1).SkipLast(1).ToList();
        }

        var indent = body.Where(b => b.Line.Trim().Length > 0).Min(b => b.Line.Length - b.Line.TrimStart().Length);
        var text = string.Join('\n', body.Select(b => b.Line.Length >= indent ? b.Line[indent..].TrimEnd() : b.Line.Trim()));
        return (text, body[0].Number, body[^1].Number);
    }

    // ---------- template ----------

    private static string FillDocsTemplate(
        string template, DocPage page, List<NavSection> sections, List<string> unlisted, List<string> order,
        Dictionary<string, DocPage> pages)
    {
        var root = string.Concat(Enumerable.Repeat("../", page.Path.Count(c => c == '/')));
        var section = sections.FirstOrDefault(s => s.Groups.Any(g => g.Pages.Contains(page.Path)));
        string Href(string path) => root + DocsHtmlPath(path);

        var tabs = new StringBuilder();
        foreach (var s in sections)
        {
            var current = s == section ? " aria-current=\"true\"" : "";
            tabs.Append(CultureInfo.InvariantCulture, $"<li><a href=\"{Href(s.Groups[0].Pages[0])}\"{current}>{WebUtility.HtmlEncode(s.Tab)}</a></li>");
        }

        var ledger = new StringBuilder();
        var ledgerGroups = section?.Groups ?? [new NavGroup(null, unlisted)];
        foreach (var group in ledgerGroups)
        {
            if (group.Title is not null)
            {
                ledger.Append(CultureInfo.InvariantCulture, $"<p class=\"group\">{WebUtility.HtmlEncode(group.Title)}</p>");
            }
            ledger.Append("<ol>");
            foreach (var path in group.Pages.Where(pages.ContainsKey))
            {
                var current = path == page.Path ? " aria-current=\"page\"" : "";
                ledger.Append(CultureInfo.InvariantCulture, $"<li><a href=\"{Href(path)}\"{current}>{LedgerLabel(pages[path].TitleHtml)}</a></li>");
            }
            ledger.Append("</ol>");
        }

        var toc = new StringBuilder();
        var tocHeadings = page.Headings.Where(h => h.Level is 2 or 3).ToList();
        if (tocHeadings.Count(h => h.Level == 2) >= 2)
        {
            toc.Append("<nav class=\"toc\" aria-labelledby=\"toc-title\"><p id=\"toc-title\">On this page</p><ol>");
            foreach (var h in tocHeadings)
            {
                toc.Append(CultureInfo.InvariantCulture, $"<li class=\"l{h.Level}\"><a href=\"#{h.Id}\">{StripLinks(h.Html)}</a></li>");
            }
            toc.Append("</ol></nav>");
        }

        var pager = new StringBuilder();
        var index = order.IndexOf(page.Path);
        if (index >= 0)
        {
            if (index > 0 && pages.TryGetValue(order[index - 1], out var previous))
            {
                pager.Append(CultureInfo.InvariantCulture, $"<a class=\"prev\" rel=\"prev\" href=\"{Href(previous.Path)}\"><span>Previous</span>{StripLinks(previous.TitleHtml)}</a>");
            }
            if (index < order.Count - 1 && pages.TryGetValue(order[index + 1], out var next))
            {
                pager.Append(CultureInfo.InvariantCulture, $"<a class=\"next\" rel=\"next\" href=\"{Href(next.Path)}\"><span>Next</span>{StripLinks(next.TitleHtml)}</a>");
            }
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["root"] = root,
            ["home"] = root + "../",
            ["title"] = page.TitleHtml,
            ["title-text"] = WebUtility.HtmlEncode(page.TitleText),
            ["description"] = WebUtility.HtmlEncode(page.Description),
            ["canonical"] = DocsSiteUrl + DocsHtmlPath(page.Path),
            ["section"] = WebUtility.HtmlEncode(section?.Tab ?? "Other guides"),
            ["tabs"] = tabs.ToString(),
            ["ledger"] = ledger.ToString(),
            ["toc"] = toc.ToString(),
            ["body"] = page.BodyHtml,
            ["pager"] = pager.ToString(),
            ["edit"] = $"{DocsRepositoryUrl}/edit/{DocsBranch}/docs/{page.Path}",
            ["markdown"] = page.Path[(page.Path.LastIndexOf('/') + 1)..],
        };
        return Regex.Replace(template, @"\{\{([a-z-]+)\}\}", m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
    }

    // A diagnostic's title "DUCKY005: Handler…" reads as its ID in mono and its summary.
    private static string LedgerLabel(string titleHtml)
    {
        var html = StripLinks(titleHtml);
        var m = Regex.Match(html, "^(DUCKYM?[0-9]+): (.*)$", RegexOptions.Singleline);
        return m.Success ? $"<span class=\"id\">{m.Groups[1].Value}</span> {m.Groups[2].Value}" : html;
    }

    private static string StripLinks(string html) => Regex.Replace(html, "</?a\\b[^>]*>", "");

    private static string DocsSearchIndex(List<NavSection> sections, Dictionary<string, DocPage> pages)
    {
        var entries = new List<object>();
        // Unlisted pages stay out of search as they stay off the tabs: reachable by their links only.
        foreach (var (path, tab) in sections.SelectMany(s => s.Groups.SelectMany(g => g.Pages).Select(p => (p, s.Tab))))
        {
            if (!pages.TryGetValue(path, out var page))
            {
                continue;
            }
            foreach (var (id, heading, text) in page.SearchSections)
            {
                entries.Add(new { t = page.TitleText, s = tab, u = DocsHtmlPath(path) + (id.Length > 0 ? "#" + id : ""), h = heading, x = text });
            }
        }
        return JsonSerializer.Serialize(entries);
    }

    // ---------- code blocks ----------

    private sealed class DocsCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
    {
        private static readonly Regex Source = new(@"\bsrc=(\S+):(\d+)-(\d+)");

        protected override void Write(HtmlRenderer renderer, CodeBlock obj)
        {
            var fenced = obj as FencedCodeBlock;
            var language = (fenced?.Info ?? "").ToLowerInvariant();
            var code = string.Join('\n', obj.Lines.Lines.Take(obj.Lines.Count).Select(l => l.Slice.ToString()));
            var source = Source.Match(fenced?.Arguments ?? "");
            renderer.EnsureLine();
            renderer.Write("<figure class=\"code\">");
            if (source.Success)
            {
                var path = source.Groups[1].Value;
                var file = path[(path.LastIndexOf('/') + 1)..];
                var url = $"{DocsRepositoryUrl}/blob/{DocsBranch}/{path}#L{source.Groups[2].Value}-L{source.Groups[3].Value}";
                renderer.Write($"<figcaption><a href=\"{url}\" title=\"{WebUtility.HtmlEncode(path)}\">{WebUtility.HtmlEncode(file)}</a><span>from a tested file</span></figcaption>");
            }
            renderer.Write($"<pre tabindex=\"0\"><code data-lang=\"{WebUtility.HtmlEncode(language)}\">");
            renderer.Write(HighlightCode(code, language));
            renderer.Write("</code></pre></figure>");
            renderer.WriteLine();
        }
    }
}
