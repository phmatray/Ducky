using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Solutions;
using Serilog;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

internal sealed partial class Build
{
    // §17.1: `// Stryker disable once <mutators> : <reason>`, `#pragma warning disable <ids> // justification: <why>`.
    private static readonly (Regex Pattern, string Rule)[] ExclusionRules =
    [
        // Stryker's own parser takes any case. Only `once` with named mutators (never `all`) and a reason: a non-once
        // disable, or `all`, ignores every later mutant of the file, which a --since run then scores as zero mutants
        // (S-6); `restore` only pairs with a non-once disable. '*' stops the reason search at a block comment's end. The
        // reason must not start like Stryker's own ("Removed by <filter>", "Mutant …", "Ignored via code comment."):
        // Mutate tells a comment's Ignored mutants from Stryker's filters by that prefix alone.
        (new Regex(@"Stryker\s*disable(?!\s+once\s+(?!all\b)\w+(?:\s*,\s*(?!all\b)\w+)*\s*:\s*(?!removed by|mutant|ignored via)[^\s*])", RegexOptions.IgnoreCase),
            "Stryker disable not of the form 'Stryker disable once <mutators> : <reason>' (no 'all'; the reason not starting 'Removed by', 'Mutant' or 'Ignored via', Stryker's own)"),
        (new Regex(@"Stryker\s*restore", RegexOptions.IgnoreCase), "Stryker restore (only 'Stryker disable once' is allowed)"),
        (new Regex(@"#\s*pragma\s+warning\s+disable(?!.*//\s*justification:\s*\S)"), "#pragma warning disable without '// justification: <why>'"),
        // `#line hidden` hides sequence points and `#line N "x.g.cs"` moves them onto the path exclusion.
        (new Regex(@"^\s*#\s*line\b"), "#line directive (hides or remaps sequence points: a coverage exclusion)"),
    ];

    // S-6: `disable once` covers the next syntax node, not the next line. Above a member or a type it ignores every
    // mutant inside, which then counts in no score, so the next code line (comments, attributes and directives skipped)
    // must not open a type, namespace or member. Text heuristics: Mutate backstops what they miss from the report
    // (DisableReach, Build.Mutation.cs).
    private static readonly Regex StrykerDisableOnce = new(@"Stryker\s*disable\s+once\b", RegexOptions.IgnoreCase);

    private const string Modifiers =
        "(?:public|private|protected|internal|file|static|async|override|virtual|abstract|sealed|extern|unsafe|partial|readonly|required|const|volatile)";

    private static readonly Regex TypeDeclaration = new($@"^(?:{Modifiers}\s+)*(?:class|struct|record|interface|enum|namespace|delegate)\s+@?\w");

    private static readonly Regex ModifierLed = new($@"^{Modifiers}\s");

    // Type arguments, which may hold tuples, nested type arguments and global:: names.
    private const string TypeArguments = @"<[\w\s,.?:()\[\]<>]*>";

    // A local function or a member without modifiers, its return type possibly a tuple or global::-qualified: `Type Name(`,
    // `Type Name<T>(`, `Type I.Name(`, and an explicit interface property or indexer (`Type I.Name`, then `{`, `=>`, `[` or
    // the end of the line); never a statement or query keyword.
    private static readonly Regex UnmodifiedSignature = new(
        $@"^(?!(?:return|await|else|throw|yield|using|case|when|in|is|as|not|and|or|var|new|goto|lock|fixed|checked|unchecked|from|where|select|group|into|orderby|join|let|on|equals|by)\b)"
        + $@"(?:(?:global::)?[\w.]+(?:{TypeArguments})?|\([^()]*\))[?\[\]]*\s+"
        + $@"(?:(?:@?\w+(?:{TypeArguments})?\.)+@?\w+\s*(?:[\[{{]|=>|$)|(?:@?\w+(?:{TypeArguments})?\.)*@?\w+\s*(?:{TypeArguments})?\s*\()");

    // An accessor (`get`, `set`, `init`, `add`, `remove` opening a block or a body on a later line) or a finalizer.
    private static readonly Regex AccessorOrFinalizer = new(@"^(?:(?:(?:private|protected|internal)\s+)*(?:get|set|init|add|remove)\s*(?:\{|=>|$)|~\w+\s*\()");

    private IEnumerable<string> StrykerDisablePlacementViolations(List<string> srcFiles) =>
        from f in srcFiles
        let lines = (RootDirectory / f).ReadAllLines()
        from index in Enumerable.Range(0, lines.Length)
        where StrykerDisableOnce.IsMatch(lines[index])
        let next = NextCodeLine(lines.Skip(index + 1))
        where next is not null && IsDeclarationStart(next)
        select $"{f}:{index + 1}: Stryker disable once above a declaration ignores every mutant inside it; put it above the one statement: {next}";

    // The first line that is neither blank, a comment (`//`, `///`, a block comment's lines), an attribute nor a directive,
    // without its trailing comment.
    private static string? NextCodeLine(IEnumerable<string> lines) => lines
        .Select(l => l.Trim())
        .Where(l => !l.StartsWith("//", StringComparison.Ordinal))
        .Select(l => Regex.Replace(l, @"//[^""]*$", "").Trim())
        .FirstOrDefault(l => l.Length > 0 && !l.StartsWith("/*", StringComparison.Ordinal) && !l.StartsWith('*') && !l.StartsWith('[') && !l.StartsWith('#'));

    // ExclusionGate checks its own text heuristics, so loosening a regex fails the gate.
    private static IEnumerable<string> DisablePlacementSelfCheck()
    {
        string[] declarations =
        [
            "public void Dispatch(object action)", "void IDisposable.Dispose()", "ValueTask IAsyncDisposable.DisposeAsync()",
            "int IReadOnlyCollection<T>.Count =>", "int IReadOnlyCollection<T>.Count", "T IList<T>.this[int index]",
            "get", "set {", "init", "add", "remove =>", "private set", "~Store()", "(int A, int B) Split(int x)",
            "IEnumerable<(int, string)> Pairs()", "global::System.Threading.Tasks.ValueTask DisposeAsync()",
            "Dictionary<string, List<int>> Map<T>(T seed)", "static int Local(int x)", "internal sealed class Store",
            "namespace Ducky", "public int Count", "public int Count => Compute(",
        ];
        string[] statements =
        [
            "return x", "return value switch", "foo.Bar(x);", "var y = Foo(", "if (a < b)", "await Task.Delay(1);",
            "select x", "x = Compute(", "get => _x;", "int IReadOnlyCollection<T>.Count => _items.Count;", "_ = value switch",
            "(a, b) = Split(", "public int X = Compute(", "Foo<int>(x)", "using var s = Open(", "throw new InvalidOperationException(",
            "yield return x;", "lock (_gate)", "foreach (var item in items)", "orderby x", "group x by y", "x with {",
        ];
        foreach (var line in declarations.Where(l => !IsDeclarationStart(l)))
        {
            yield return $"DisablePlacementSelfCheck: '{line}' must count as a declaration";
        }
        foreach (var line in statements.Where(IsDeclarationStart))
        {
            yield return $"DisablePlacementSelfCheck: '{line}' must count as a statement";
        }
        var next = NextCodeLine(["/// <inheritdoc cref=\"IStore.Dispatch\"/>", "/// <see cref=\"X\"/>", "  ", "[Pure]", "public void Dispatch(object a)"]);
        if (next != "public void Dispatch(object a)")
        {
            yield return $"DisablePlacementSelfCheck: the code line below doc comments is 'public void Dispatch(object a)', not '{next}'";
        }
    }

    // Stryker skips a *.designer.cs, or a file whose leading comments hold `<auto-generated` or `<autogenerated`, as generated
    // code: it gets no mutant, so it counts in no score and no --since check sees it. Roslyn treats those, *.generated.cs,
    // *.g.i.cs and TemporaryGeneratedFile_* as generated code too, which analyzers that skip it (CA2007, the banned APIs)
    // never report. Leading comments only: the generators' raw-string templates under src/ hold the header as text.
    private static readonly Regex GeneratedFileName = new(@"\.(?:designer|generated|g\.i)\.cs$|TemporaryGeneratedFile_", RegexOptions.IgnoreCase);

    // The lines before the first code line (blank, comment or directive lines) hold `<auto-generated` or `<autogenerated`.
    private static bool HasGeneratedHeader(IEnumerable<string> lines) => lines
        .Select(l => l.Trim())
        .TakeWhile(l => l.Length == 0 || l.StartsWith("//", StringComparison.Ordinal) || l.StartsWith("/*", StringComparison.Ordinal)
            || l.StartsWith('*') || l.StartsWith('#'))
        .Any(l => l.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase) || l.Contains("<autogenerated", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> GeneratedMarkerSelfCheck()
    {
        string[] generatedNames = ["src/Ducky/Reducer.Designer.cs", "src/Ducky/Store.generated.cs", "src/Ducky/Store.g.i.cs", "src/Ducky/TemporaryGeneratedFile_1.cs"];
        string[] plainNames = ["src/Ducky/Designer.cs", "src/Ducky/Generated.cs", "src/Ducky/StoreDesigner.cs"];
        string[][] generatedHeaders =
        [
            ["// <auto-generated/>", "namespace Ducky;"], ["", "#nullable enable", "/*", " * <AutoGenerated />", " */", "namespace Ducky;"],
        ];
        string[][] plainHeaders =
        [
            ["namespace Ducky;", "// <auto-generated/>"], ["[assembly: Foo]", "// <auto-generated/>"],
            ["using System;", "const string Header = \"// <auto-generated/>\";"],
        ];
        foreach (var name in generatedNames.Where(n => !GeneratedFileName.IsMatch(n)).Concat(plainNames.Where(n => GeneratedFileName.IsMatch(n))))
        {
            yield return $"GeneratedMarkerSelfCheck: '{name}' is misjudged as {(GeneratedFileName.IsMatch(name) ? "" : "not ")}a generated file name";
        }
        foreach (var lines in generatedHeaders.Where(l => !HasGeneratedHeader(l)).Concat(plainHeaders.Where(HasGeneratedHeader)))
        {
            yield return $"GeneratedMarkerSelfCheck: [{string.Join(" | ", lines)}] is misjudged as {(HasGeneratedHeader(lines) ? "" : "not ")}a generated header";
        }
    }

    // A one-line member ending in ';' (a field, an expression body) holds one expression, like a statement; a modifier-led
    // line with '=' before any '(' is a field whose initializer continues.
    private static bool IsDeclarationStart(string code) =>
        TypeDeclaration.IsMatch(code)
        || (!code.EndsWith(';')
            && (UnmodifiedSignature.IsMatch(code)
                || AccessorOrFinalizer.IsMatch(code)
                || (ModifierLed.IsMatch(code) && !code.Split('(')[0].Replace("=>", "", StringComparison.Ordinal).Contains('=', StringComparison.Ordinal))));

    // §17.1: the one committed coverage settings file (the Test target passes it to dotnet test), its only exclusion
    // (generated sources, by path) and the only elements it may hold; anything else narrows, or may narrow, what is
    // measured. The tool's defaults stay as spike S-2 recorded them (docs/spec/spikes.md).
    private AbsolutePath CoverageSettings => RootDirectory / "build" / "coverage.settings.xml";

    private const string GeneratedSources = @".*\.g\.cs$";

    private static readonly string[] CoverageSettingsShape =
    [
        "Configuration",
        "Configuration/CodeCoverage",
        "Configuration/CodeCoverage/Sources",
        "Configuration/CodeCoverage/Sources/Exclude",
        "Configuration/CodeCoverage/Sources/Exclude/Source",
    ];

    private string Relative(AbsolutePath path) => RootDirectory.GetRelativePathTo(path).ToString().Replace('\\', '/');

    // Repository files the gates see, relative: only the top-level spikes/ and artifacts/ are outside the gates (§18);
    // build output is skipped at any depth.
    private List<string> GatedFiles(params string[] patterns)
    {
        string[] outsideGates = ["artifacts/", "spikes/", "node_modules/", ".git/"];
        return RootDirectory.GlobFiles(patterns)
            .Select(Relative)
            .Where(f => !outsideGates.Any(prefix => f.StartsWith(prefix, StringComparison.Ordinal)))
            .Where(f => !f.Split('/').SkipLast(1).Intersect(["bin", "obj"]).Any())
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    // Coverage exclusions in code are banned APIs (BannedSymbols.txt), except [GeneratedCode], which generated sources
    // carry: it and the *.g.cs path exclusion are checked here, and so are the stryker-config.json keys
    // (StrykerConfigViolations, Build.Mutation.cs).
    private Target ExclusionGate => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var csFiles = GatedFiles("**/*.cs");
            var srcFiles = csFiles.Where(f => f.StartsWith("src/", StringComparison.Ordinal)).ToList();
            var violations =
                from f in srcFiles
                from line in (RootDirectory / f).ReadAllLines().Select((text, index) => (text, number: index + 1))
                from rule in ExclusionRules
                where rule.Pattern.IsMatch(line.text)
                select $"{f}:{line.number}: {rule.Rule}: {line.text.Trim()}";
            // Repository-wide: a *.g.cs anywhere can be linked into a src/ project. Generator output stays in memory or obj/.
            var committedGenerated = csFiles
                .Where(IsGeneratedSource)
                .Select(f => $"{f}: a committed *.g.cs is excluded from coverage by path; generated sources come from generators only");
            // Repository-wide for the same reason.
            var markedGenerated = csFiles
                .Where(f => GeneratedFileName.IsMatch(f) || HasGeneratedHeader(File.ReadLines(RootDirectory / f)))
                .Select(f => $"{f}: named or headed as generated code (*.designer.cs, *.generated.cs, *.g.i.cs, TemporaryGeneratedFile_*, a leading <auto-generated> comment): Stryker mutates none of it and analyzers skip it");

            var list = violations.Concat(StrykerDisablePlacementViolations(srcFiles)).Concat(committedGenerated).Concat(markedGenerated).Concat(GeneratedCodeViolations())
                .Concat(CoverageSettingsViolations()).Concat(StrykerConfigViolations()).Concat(DisablePlacementSelfCheck())
                .Concat(GeneratedMarkerSelfCheck()).ToList();
            list.ForEach(v => Log.Error(v));
            Assert.True(list.Count == 0, $"ExclusionGate: {list.Count} violation(s)");
        });

    // Every file that can configure the coverage tool counts (a .runsettings, an MTP testconfig.json with a
    // codeCoverage section, a coverage XML); exactly one may exist, and it must have the pinned shape.
    private IEnumerable<string> CoverageSettingsViolations()
    {
        var files = GatedFiles("**/*.runsettings", "**/*testconfig.json", "**/*.xml", "**/*.config")
            .Where(f => f.EndsWith(".runsettings", StringComparison.Ordinal)
                || (RootDirectory / f).ReadAllText().Contains("CodeCoverage", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var expected = Relative(CoverageSettings);
        if (files is not [var only] || only != expected)
        {
            yield return $"exactly one coverage settings file ({expected}) expected; found [{string.Join(", ", files)}]";
        }
        if (!files.Contains(expected))
        {
            yield break;
        }

        var root = XDocument.Load(CoverageSettings, LoadOptions.SetLineInfo).Root!;
        foreach (var element in root.DescendantsAndSelf())
        {
            var path = string.Join('/', element.AncestorsAndSelf().Reverse().Select(e => e.Name.LocalName));
            var isExclusion = path == CoverageSettingsShape[^1];
            if (!CoverageSettingsShape.Contains(path) || element.HasAttributes || (isExclusion && element.Value != GeneratedSources))
            {
                yield return $"{expected}:{((IXmlLineInfo)element).LineNumber}: <{string.Join(' ', element.Attributes().Prepend<object>(path))}> " +
                    $"narrows what is measured; the only allowed exclusion is Sources/Exclude/Source = {GeneratedSources}";
            }
        }
        if (root.Descendants().Count(e => e.Name.LocalName == "Source") != 1)
        {
            yield return $"{expected}: exactly one Source exclusion ({GeneratedSources}) expected";
        }
    }

    // §19: merges the deterministic runs' cobertura files and requires 100% line and branch for every src/ assembly
    // of Ducky.slnx. The only ReportGenerator filter is that assembly list (§17.1).
    private Target CoverageGate => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            var projects = SrcAssemblyProjects().ToDictionary(p => p.Name);
            Assert.NotEmpty((Artifacts / "test").GlobFiles("**/*.cobertura.xml"), "CoverageGate: no cobertura file under artifacts/test");

            var coverage = Artifacts / "coverage";
            coverage.CreateOrCleanDirectory();
            var filters = string.Join(';', projects.Keys.Select(name => $"+{name}"));
            DotNet($"reportgenerator -reports:{Artifacts / "test"}/**/*.cobertura.xml -targetdir:{coverage} -reporttypes:Cobertura;Html -assemblyfilters:{filters}");

            var packages = XDocument.Load(coverage / "Cobertura.xml").Descendants("package").ToDictionary(p => (string)p.Attribute("name")!);
            var errors = new List<string>();
            foreach (var name in projects.Keys.Except(packages.Keys).Order(StringComparer.Ordinal))
            {
                // The tool reports no module without a sequence point (a skeleton of code-less types, spike S-2):
                // such an assembly has nothing to cover. One with code is missing: its tests never measured it.
                if (HasCoverableCode(projects[name]))
                {
                    errors.Add($"{name}: missing from the merged coverage report");
                }
                else
                {
                    Log.Information("{Assembly}: no executable code, nothing to cover", name);
                }
            }
            errors.AddRange(packages.Keys.Except(projects.Keys).Select(name => $"{name}: in the coverage report but not a src/ project of {Solution.FileName}"));

            foreach (var (name, package) in packages.Where(p => projects.ContainsKey(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var lineRate = double.Parse((string)package.Attribute("line-rate")!, CultureInfo.InvariantCulture);
                var branchRate = double.Parse((string)package.Attribute("branch-rate")!, CultureInfo.InvariantCulture);
                if (lineRate >= 1.0 && branchRate >= 1.0)
                {
                    continue;
                }
                errors.Add($"{name}: line-rate {lineRate:P2}, branch-rate {branchRate:P2} (100% required)");
                errors.AddRange(
                    from @class in package.Descendants("class")
                    from line in @class.Element("lines")!.Elements("line")
                    let conditions = (string?)line.Attribute("condition-coverage")
                    let partial = conditions is not null && !conditions.StartsWith("100%", StringComparison.Ordinal)
                    where (string)line.Attribute("hits")! == "0" || partial
                    select $"  {(string)@class.Attribute("filename")!}:{(string)line.Attribute("number")!}: " +
                        (partial ? $"branches {conditions}" : "not covered"));
            }

            errors.ForEach(e => Log.Error(e));
            Assert.True(errors.Count == 0, $"CoverageGate: {coverage / "index.html"}");
        });

    // The coverage tool applies the <Source> regex case-insensitively: `X.G.cs` is excluded as well.
    private static bool IsGeneratedSource(string path) => Regex.IsMatch(path, GeneratedSources, RegexOptions.IgnoreCase);

    // The src/ projects of Ducky.slnx that build an assembly. Only the NoTargets tombstone builds none (§3), known by its
    // Sdk attribute, and the exclusion is logged.
    private List<Project> SrcAssemblyProjects()
    {
        var projects = new List<Project>();
        foreach (var project in Solution.AllProjects.Where(p => (RootDirectory / "src").Contains(p.Path)))
        {
            if (XDocument.Load(project.Path).Root!.Attribute("Sdk")?.Value.StartsWith("Microsoft.Build.NoTargets/", StringComparison.Ordinal) == true)
            {
                Log.Information("{Project}: NoTargets project, builds no assembly to measure", project.Name);
                continue;
            }
            projects.Add(project);
        }
        return projects;
    }

    // [GeneratedCode] makes the coverage tool skip a type or method (spike S-2), so it may mark generated sources only.
    private IEnumerable<string> GeneratedCodeViolations()
    {
        foreach (var project in SrcAssemblyProjects())
        {
            if (ReleaseMethods(project) is not { } methods)
            {
                yield return $"{project.Name}: no Release build (dll and pdb) to check for [GeneratedCode]";
                continue;
            }
            foreach (var (method, document) in methods.Where(m => m.GeneratedCode)
                .SelectMany(m => m.Documents.Where(d => !IsGeneratedSource(d)).Select(d => (m.Name, d))))
            {
                yield return $"{project.Name}: [GeneratedCode] on {method} (or a containing type) excludes code of {document}, not a *.g.cs";
            }
        }
    }

    // A non-hidden sequence point outside generated sources in the project's Release build. No build counts as code, so a
    // project that was never built can't pass as code-less.
    private static bool HasCoverableCode(Project project) =>
        ReleaseMethods(project) is not { } methods || methods.Any(m => m.Documents.Any(d => !IsGeneratedSource(d)));

    private sealed record MethodCode(string Name, bool GeneratedCode, string[] Documents);

    // Every method of the project's Release build, whether it or a containing type carries [GeneratedCode] (matched by
    // name, as the tool does), and the documents of its non-hidden sequence points; null without a dll and pdb.
    private static List<MethodCode>? ReleaseMethods(Project project)
    {
        var dll = (project.Path.Parent / "bin" / "Release").GlobFiles($"*/{project.Name}.dll").FirstOrDefault();
        var pdb = dll is null ? null : Path.ChangeExtension(dll.ToString(), ".pdb");
        if (pdb is null || !File.Exists(pdb))
        {
            return null;
        }
        using var pe = new PEReader(File.OpenRead(dll!));
        using var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(pdb));
        var metadata = pe.GetMetadataReader();
        var debug = pdbProvider.GetMetadataReader();

        bool Marked(CustomAttributeHandleCollection attributes) => attributes
            .Select(handle => metadata.GetCustomAttribute(handle).Constructor)
            .Any(constructor => constructor.Kind switch
            {
                HandleKind.MemberReference when metadata.GetMemberReference((MemberReferenceHandle)constructor).Parent is { Kind: HandleKind.TypeReference } parent
                    => metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)parent).Name) == "GeneratedCodeAttribute",
                HandleKind.MethodDefinition
                    => metadata.GetString(metadata.GetTypeDefinition(metadata.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType()).Name) == "GeneratedCodeAttribute",
                _ => false,
            });
        bool TypeMarked(TypeDefinition type) =>
            Marked(type.GetCustomAttributes()) || (!type.GetDeclaringType().IsNil && TypeMarked(metadata.GetTypeDefinition(type.GetDeclaringType())));

        return
        [
            .. from typeHandle in metadata.TypeDefinitions
               let type = metadata.GetTypeDefinition(typeHandle)
               let typeMarked = TypeMarked(type)
               from methodHandle in type.GetMethods()
               let method = metadata.GetMethodDefinition(methodHandle)
               select new MethodCode(
                   $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}.{metadata.GetString(method.Name)}",
                   typeMarked || Marked(method.GetCustomAttributes()),
                   [
                       .. debug.GetMethodDebugInformation(methodHandle.ToDebugInformationHandle()).GetSequencePoints()
                           .Where(point => !point.IsHidden)
                           .Select(point => debug.GetString(debug.GetDocument(point.Document).Name))
                           .Distinct(),
                   ]),
        ];
    }

    // A C# fence (```csharp, ```cs, ~~~c#, any case) at any indent, also inside list items and blockquotes (CommonMark
    // containers). It would also flag an indented code block that shows a literal C# fence; no user doc needs one.
    private static readonly Regex CSharpFence = new(@"^[ \t>]*(?:(?:[-*+]|\d+[.)])[ \t]+)*(`{3,}|~{3,})[ \t]*(csharp|cs|c#)(?![\w#+-])",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // The copied design record, not user docs (§17.1).
    private static readonly string[] FenceExemptDocs = ["docs/spec/", "docs/adr/"];

    private AbsolutePath MigrationSolution => RootDirectory / "Ducky.Migration.slnx";

    // SPEC §17.1, §19. The checks run first (fast), then the migration samples build (Ducky.Migration.slnx: a plain, unlocked
    // restore, its Directory.Build.props sets RestoreLockedMode=false) and docfx renders the site to artifacts/docs, with
    // warnings as errors so an include of a missing file fails. M15-04 and M15-05 add the diagnostics-page and 1.x type checks.
    private Target Docs => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var docs = (RootDirectory / "docs").GlobFiles("**/*.md")
                .Select(f => (Path: RootDirectory.GetUnixRelativePathTo(f).ToString(), Text: f.ReadAllText())).ToList();
            FailOnViolations("Docs", [
                .. DocsFenceViolations(docs),
                .. DocsRegionViolations(docs, path => RootDirectory / path is var file && file.FileExists() ? file.ReadAllText() : null),
                .. DocsSelfCheck(),
            ]);
            DotNet($"build {MigrationSolution} -c Release -nodeReuse:false");
            (Artifacts / "docs").CreateOrCleanDirectory();
            DotNet($"docfx build {RootDirectory / "docs" / "docfx.json"} --warningsAsErrors");
        });

    // Every snippet is a #region of a compiled, tested file (§17.1).
    private static IEnumerable<string> DocsFenceViolations(IEnumerable<(string Path, string Text)> docs) =>
        docs.Where(d => !FenceExemptDocs.Any(e => d.Path.StartsWith(e, StringComparison.Ordinal)))
            .SelectMany(d => CSharpFence.Matches(d.Text).Select(m =>
                $"{d.Path}:{d.Text.AsSpan(0, m.Index).Count('\n') + 1}: fenced C# block; include a #region of a compiled, tested file instead"));

    // docfx includes code with [!code-<lang>[title](path#region)] (any case, the language optional) and renders an unknown
    // region as nothing, so each include is checked here: a #region (never a whole file, a line range or ?name=) of an
    // existing file under a compiled, tested root. C# regions (no language: C#) are looked up; other languages mark regions
    // differently and docfx resolves them itself. The :::code form is rejected outright: one include syntax, all checked.
    private static readonly Regex CodeInclude = new(@"\[!code(?:-([\w-]+))?\[[^\]]*\]\(\s*([^)#?\s]+)([#?][^)\s]*)?[^)]*\)\]", RegexOptions.IgnoreCase);

    private static readonly Regex TripleColonCode = new(@":::\s*code\b", RegexOptions.IgnoreCase);

    private static readonly string[] SnippetRoots = ["test/", "docs/migration/samples/"];

    private static IEnumerable<string> DocsRegionViolations(IEnumerable<(string Path, string Text)> docs, Func<string, string?> read) =>
        from d in docs
        where !FenceExemptDocs.Any(e => d.Path.StartsWith(e, StringComparison.Ordinal))
        from m in CodeInclude.Matches(d.Text).Concat(TripleColonCode.Matches(d.Text))
        let triple = m.Value[0] == ':'
        let at = $"{d.Path}:{d.Text.AsSpan(0, m.Index).Count('\n') + 1}"
        let link = m.Groups[2].Value
        let region = m.Groups[3].Value
        let target = ResolveDocLink(d.Path, link)
        let text = triple || target is null || !SnippetRoots.Any(r => target.StartsWith(r, StringComparison.Ordinal)) || !region.StartsWith('#') ? null : read(target)
        let violation =
            triple ? $"{at}: :::code include; use [!code-csharp[](path#region)], which this gate checks"
            : !region.StartsWith('#') || region.Length == 1 ? $"{at}: code include '{link}{region}' names no #region; include a #region of a compiled, tested file"
            : target is null ? $"{at}: code include '{link}' leaves the repository"
            : !SnippetRoots.Any(r => target.StartsWith(r, StringComparison.Ordinal)) ? $"{at}: code include '{target}' is not under {string.Join(" or ", SnippetRoots)} (a compiled, tested file)"
            : text is null ? $"{at}: code include '{target}' does not exist"
            : m.Groups[1].Value.ToLowerInvariant() is "" or "csharp" or "cs" && !Regex.IsMatch(text, $@"^[ \t]*#region[ \t]+{Regex.Escape(region[1..])}[ \t]*\r?$", RegexOptions.Multiline)
                ? $"{at}: code include '{target}' has no '#region {region[1..]}'"
            : null
        where violation is not null
        select violation;

    // The repository-relative path of a link in a doc, or null when it climbs above the root.
    private static string? ResolveDocLink(string doc, string link)
    {
        var parts = doc.Split('/')[..^1].ToList();
        foreach (var part in link.Split('/'))
        {
            if (part == "..")
            {
                if (parts.Count == 0)
                {
                    return null;
                }
                parts.RemoveAt(parts.Count - 1);
            }
            else if (part is not ("." or ""))
            {
                parts.Add(part);
            }
        }
        return string.Join('/', parts);
    }

    // The acceptance checks of M0-11 and M15-01, planted on miniature docs.
    private static IEnumerable<string> DocsSelfCheck()
    {
        const string fenced = "# Guide\n\n```csharp\nvar store = new Store();\n```\n";
        (string Path, string Text)[] unplanted =
        [
            ("docs/spec/SPEC.md", fenced),
            ("docs/adr/0001-record.md", fenced),
            ("docs/guides/selectors.md", "```json\n{}\n```\n\n[!code-csharp[](../../test/Ducky.Docs.Tests/Guide.cs#select)]\n"),
        ];
        (string Path, string Text, string Expected)[] planted =
        [
            ("docs/guides/getting-started.md", fenced, "docs/guides/getting-started.md:3: fenced C# block"),
            ("docs/guides/indented.md", "text\n\n   ```CSharp\n", "docs/guides/indented.md:3: fenced C# block"),
            ("docs/guides/short.md", "~~~cs\n~~~\n", "docs/guides/short.md:1: fenced C# block"),
            ("docs/guides/sharp.md", "```c# title=x\n```\n", "docs/guides/sharp.md:1: fenced C# block"),
            ("docs/specification/x.md", fenced, "docs/specification/x.md:3: fenced C# block"),
            ("docs/guides/list.md", "1. Install:\n\n    ```csharp\n    ```\n", "docs/guides/list.md:3: fenced C# block"),
            ("docs/guides/nested.md", "- a\n  - b\n\n      ```cs\n      ```\n", "docs/guides/nested.md:4: fenced C# block"),
            ("docs/guides/marker.md", "1. ```csharp\n   ```\n", "docs/guides/marker.md:1: fenced C# block"),
            ("docs/guides/quote.md", "> ```csharp\n> ```\n", "docs/guides/quote.md:1: fenced C# block"),
            ("docs/guides/tab.md", "- a\n\n\t```csharp\n\t```\n", "docs/guides/tab.md:3: fenced C# block"),
        ];
        foreach (var violation in DocsFenceViolations(unplanted))
        {
            yield return $"DocsSelfCheck: the unplanted docs must pass, got: {violation}";
        }
        foreach (var (path, text, expected) in planted)
        {
            var violations = DocsFenceViolations([(path, text)]).ToList();
            if (!violations.Any(v => v.Contains(expected, StringComparison.Ordinal)))
            {
                yield return $"DocsSelfCheck: a fenced block in {path} must report '{expected}', got [{string.Join("; ", violations)}]";
            }
        }
        Dictionary<string, string> files = new()
        {
            ["test/Ducky.Docs.Tests/Guide.cs"] = "class Guide\n{\n    #region select\n    int x;\n    #endregion\n}\n",
            ["src/Ducky/Store.cs"] = "#region store\n#endregion\n",
            ["test/Ducky.Docs.Tests/Crlf.cs"] = "#region crlf\r\n#endregion\r\n",
            ["docs/migration/samples/ducky-1x/Before/A.cs"] = "#region before\n#endregion\n",
        };
        string? Read(string path) => files.GetValueOrDefault(path);
        const string include = "[!code-csharp[](../../test/Ducky.Docs.Tests/Guide.cs#select)]";
        (string Path, string Text)[] includes =
        [
            ("docs/guides/selectors.md", $"# Selectors\n\n{include}\n"),
            ("docs/guides/razor.md", "[!code-razor[](../../test/Ducky.Docs.Tests/Guide.cs#other)]\n"),
            ("docs/spec/SPEC.md", "[!code-csharp[](../../src/Nope.cs)]\n:::code source=\"../../src/Nope.cs\":::\n"),
            ("docs/guides/crlf.md", "[!code-csharp[](../../test/Ducky.Docs.Tests/Crlf.cs#crlf)]\n"),
            ("docs/migration/ducky-1x.md", "[!code-csharp[](samples/ducky-1x/Before/A.cs#before)]\n"),
            ("docs/guides/upper-ok.md", "[!CODE-CSharp[](../../test/Ducky.Docs.Tests/Guide.cs#select)]\n[!code[](../../test/Ducky.Docs.Tests/Guide.cs#select)]\n"),
        ];
        (string Path, string Text, string Expected)[] plantedIncludes =
        [
            ("docs/guides/whole.md", "[!code-csharp[](../../test/Ducky.Docs.Tests/Guide.cs)]\n", "docs/guides/whole.md:1: code include '../../test/Ducky.Docs.Tests/Guide.cs' names no #region"),
            ("docs/guides/empty.md", "x\n[!code-csharp[](../../test/Ducky.Docs.Tests/Guide.cs#)]\n", "docs/guides/empty.md:2: code include '../../test/Ducky.Docs.Tests/Guide.cs#' names no #region"),
            ("docs/guides/query.md", "[!code-cs[](../../test/Ducky.Docs.Tests/Guide.cs?name=select)]\n", "names no #region"),
            ("docs/guides/region.md", "[!code-csharp[](../../test/Ducky.Docs.Tests/Guide.cs#selector)]\n", "docs/guides/region.md:1: code include 'test/Ducky.Docs.Tests/Guide.cs' has no '#region selector'"),
            ("docs/guides/cs.md", "[!code-cs[title](../../test/Ducky.Docs.Tests/Guide.cs#nope \"t\")]\n", "has no '#region nope'"),
            ("docs/guides/missing.md", "[!code-csharp[](../../test/Ducky.Docs.Tests/Gone.cs#select)]\n", "code include 'test/Ducky.Docs.Tests/Gone.cs' does not exist"),
            ("docs/guides/src.md", "[!code-csharp[](../../src/Ducky/Store.cs#store)]\n", "code include 'src/Ducky/Store.cs' is not under test/ or docs/migration/samples/"),
            ("docs/guides/up.md", "[!code-csharp[](../../../outside.cs#a)]\n", "code include '../../../outside.cs' leaves the repository"),
            ("docs/guides/prefix.md", "[!code-csharp[](../../test/Ducky.Docs.Tests/Guide.cs#sel)]\n", "has no '#region sel'"),
            ("docs/guides/nolang.md", "[!code[](../../test/Ducky.Docs.Tests/Guide.cs)]\n", "docs/guides/nolang.md:1: code include '../../test/Ducky.Docs.Tests/Guide.cs' names no #region"),
            ("docs/guides/upper.md", "[!CODE-csharp[](../../test/Ducky.Docs.Tests/Guide.cs)]\n", "docs/guides/upper.md:1: code include '../../test/Ducky.Docs.Tests/Guide.cs' names no #region"),
            ("docs/guides/nolang-region.md", "[!code[](../../test/Ducky.Docs.Tests/Guide.cs#nope)]\n", "has no '#region nope'"),
            ("docs/guides/upper-lang.md", "[!code-CSharp[](../../test/Ducky.Docs.Tests/Guide.cs#nope)]\n", "has no '#region nope'"),
            ("docs/guides/triple.md", "x\n:::code language=\"csharp\" source=\"../../src/Ducky/Store.cs\":::\n", "docs/guides/triple.md:2: :::code include"),
            ("docs/guides/triple-region.md", "::: CODE source=\"../../test/Ducky.Docs.Tests/Guide.cs\" id=\"select\":::\n", "docs/guides/triple-region.md:1: :::code include"),
            ("docs/guides/dot.md", "[!code-csharp[](./../../test/./Ducky.Docs.Tests/Guide.cs#selectx)]\n", "'test/Ducky.Docs.Tests/Guide.cs' has no '#region selectx'"),
        ];
        foreach (var violation in DocsRegionViolations(includes, Read))
        {
            yield return $"DocsSelfCheck: the unplanted includes must pass, got: {violation}";
        }
        foreach (var (path, text, expected) in plantedIncludes)
        {
            var violations = DocsRegionViolations([(path, text)], Read).ToList();
            if (violations.Count != 1 || !violations[0].Contains(expected, StringComparison.Ordinal))
            {
                yield return $"DocsSelfCheck: the include in {path} must report '{expected}' once, got [{string.Join("; ", violations)}]";
            }
        }
    }
}
