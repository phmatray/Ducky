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
    // §17.1: `// Stryker disable [once] <mutators> : <reason>`, `#pragma warning disable <ids> // justification: <why>`.
    private static readonly (Regex Pattern, string Rule)[] ExclusionRules =
    [
        // Stryker's own parser takes zero spaces and any case; '*' stops the reason search at a block comment's end.
        (new Regex(@"Stryker\s*disable(?![^:*\r\n]*:\s*[^\s*])", RegexOptions.IgnoreCase), "Stryker disable without ': <reason>'"),
        (new Regex(@"#\s*pragma\s+warning\s+disable(?!.*//\s*justification:\s*\S)"), "#pragma warning disable without '// justification: <why>'"),
        // `#line hidden` hides sequence points and `#line N "x.g.cs"` moves them onto the path exclusion.
        (new Regex(@"^\s*#\s*line\b"), "#line directive (hides or remaps sequence points: a coverage exclusion)"),
    ];

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
    // carry: it and the *.g.cs path exclusion are checked here. The stryker-config.json checks arrive with those files
    // (M0-05).
    private Target ExclusionGate => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var csFiles = GatedFiles("**/*.cs");
            var violations =
                from f in csFiles.Where(f => f.StartsWith("src/", StringComparison.Ordinal))
                from line in (RootDirectory / f).ReadAllLines().Select((text, index) => (text, number: index + 1))
                from rule in ExclusionRules
                where rule.Pattern.IsMatch(line.text)
                select $"{f}:{line.number}: {rule.Rule}: {line.text.Trim()}";
            // Repository-wide: a *.g.cs anywhere can be linked into a src/ project. Generator output stays in memory or obj/.
            var committedGenerated = csFiles
                .Where(IsGeneratedSource)
                .Select(f => $"{f}: a committed *.g.cs is excluded from coverage by path; generated sources come from generators only");

            var list = violations.Concat(committedGenerated).Concat(GeneratedCodeViolations()).Concat(CoverageSettingsViolations()).ToList();
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
}
