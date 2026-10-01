using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// SPEC §3 (R-PKG-1, R-PKG-5, R-PKG-6), §17.9 and §19: Pack and PackageSmoke. PackageSmoke is staged (§17.9): this file
// holds the step-1 checks; later consumers and assertions arrive with their features.
internal sealed partial class Build
{
    // R-PKG-6: the five library IDs Pack sends to artifacts/packages (each with its .snupkg), and the tombstone.
    private static readonly string[] LibraryPackages = ["Ducky", "Ducky.Blazor", "Ducky.Draft", "Ducky.Reactive", "Ducky.Testing"];
    private const string TombstonePackage = "Ducky.Generator.2.0.0.nupkg";

    // R-PKG-5 and §3: the packages that reference Ducky with PrivateAssets="none", and the only runtime dependencies §3
    // allows each of them (later stories add the non-Ducky ones with their features).
    private static readonly Dictionary<string, string[]> DuckyDependents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ducky.Blazor"] = ["Ducky", "Microsoft.AspNetCore.Components.Web", "Microsoft.AspNetCore.Components.Authorization", "Microsoft.Extensions.Caching.Abstractions"],
        ["Ducky.Reactive"] = ["Ducky", "R3"],
        ["Ducky.Testing"] = ["Ducky", "Microsoft.Extensions.TimeProvider.Testing", "Microsoft.Extensions.DependencyInjection"],
    };

    // §3: the entries each library package must hold besides lib/net10.0/{Id}.dll and .xml. A package holds nothing else
    // but its nuspec and the OPC parts (PackagingEntry) plus, in Ducky.Blazor only, the Razor SDK's other generated build
    // files and the static web assets (BlazorEntry): no content/ or contentFiles/ entry ever ships.
    private static readonly Dictionary<string, string[]> LibraryLayouts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ducky"] = ["analyzers/dotnet/cs/Ducky.Generators.dll", "buildTransitive/Ducky.targets"],
        ["Ducky.Blazor"] = ["staticwebassets/ducky.js", "build/Ducky.Blazor.props", "buildTransitive/Ducky.Blazor.props"],
        ["Ducky.Draft"] = ["analyzers/dotnet/cs/Ducky.Draft.Generators.dll", "buildTransitive/Ducky.Draft.targets"],
        ["Ducky.Reactive"] = [],
        ["Ducky.Testing"] = [],
    };
    private static readonly Regex PackagingEntry = new(@"^(_rels/\.rels|\[Content_Types\]\.xml|package/services/metadata/core-properties/[^/]+\.psmdcp|[^/]+\.nuspec)$");
    private static readonly Regex BlazorEntry = new(@"^(buildMultiTargeting/Ducky\.Blazor\.props|build/Microsoft\.AspNetCore\.StaticWebAsset(s|Endpoints)\.props|staticwebassets/.+)$");

    // `dotnet list package --format json` sections of one framework.
    private static readonly string[] ListedPackageKinds = ["topLevelPackages", "transitivePackages"];

    // §3: Ducky's only runtime dependencies.
    private static readonly string[] DuckyDependencies = ["Microsoft.Extensions.DependencyInjection.Abstractions", "Microsoft.Extensions.Logging.Abstractions"];

    // R-PKG-1: package IDs no package may bring into a consumer's restore graph (NuGet IDs are case-insensitive). The
    // Microsoft.AspNetCore.App framework reference is checked in the nuspec: `dotnet list package` doesn't list frameworks.
    private static readonly Regex ForbiddenPackage = new(
        @"^(Microsoft\.CodeAnalysis\..+|xunit.*|bunit|Microsoft\.Reactive\.Testing|System\.Reactive|FakeItEasy)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private AbsolutePath Packages => Artifacts / "packages";
    private AbsolutePath Tombstones => Artifacts / "tombstone";
    private AbsolutePath SmokeSolution => RootDirectory / "Ducky.PackageSmoke.slnx";
    private AbsolutePath SmokeDirectory => RootDirectory / "test" / "Ducky.PackageSmoke";

    // Package validation (EnablePackageValidation, Directory.Build.props) runs inside `dotnet pack`, not for the tombstone.
    private Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            Packages.DeleteDirectory();
            Tombstones.DeleteDirectory();
            DotNet($"pack {Solution} -c Release --no-build");
            var violations = PackageSetViolations(MinVerVersion(), FileNames(Packages), FileNames(Tombstones))
                .Concat(PackSelfCheck())
                .ToList();
            violations.ForEach(v => Log.Error(v));
            Assert.True(violations.Count == 0, $"Pack: {violations.Count} violation(s)");
        });

    private Target PackageSmoke => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            var version = MinVerVersion();
            (Artifacts / "smoke-packages").DeleteDirectory();

            // Step 2: the nuspec and layout of every package.
            var violations = NuspecViolations(
                    Packages.GlobFiles("*.nupkg").Select(PackageContent.Read).ToList(),
                    PackageContent.Read(Tombstones / TombstonePackage),
                    FileNames(Packages))
                .Concat(NuspecSelfCheck())
                .ToList();

            // Step 3: the consumers' whole restore graph.
            DotNet($"restore {SmokeSolution} -p:DuckyVersion={version}");
            var graph = JsonDocument.Parse(string.Join('\n',
                DotNet($"list {SmokeSolution} package --include-transitive --format json --no-restore", logOutput: false).Select(o => o.Text)));
            violations.AddRange(ForbiddenPackages(graph, XElement.Load(SmokeSolution).Descendants("Project")
                .Select(p => Path.GetFileNameWithoutExtension((string)p.Attribute("Path")!))).Concat(GraphSelfCheck()));

            // Step 1: the consumers compile with TreatWarningsAsErrors.
            var exitCode = 0;
            DotNet($"build {SmokeSolution} -c Release --no-restore -p:DuckyVersion={version}", exitHandler: p => exitCode = p.ExitCode);
            if (exitCode != 0)
            {
                violations.Add($"{SmokeSolution.Name} failed to build (exit code {exitCode})");
            }

            // Step 4 and the step-1 variants: each must fail with its diagnostic.
            (string Case, AbsolutePath Project, string Properties, string Code)[] variants =
            [
                ("the Ducky.Blazor-only consumer plus the fake Ducky.Generator 1.0.292", SmokeDirectory / "BlazorOnly", "-p:SmokeLegacyGenerator=1.0.292", "DUCKY900"),
                ("a consumer of the Ducky.Generator 2.0.0 tombstone", SmokeDirectory / "Tombstone", "", "DUCKY900"),
                ("the Server consumer on the fake Ducky.Blazor 1.0.292", SmokeDirectory / "Server", "-p:SmokeBlazorVersion=1.0.292", "DUCKY902"),
            ];
            foreach (var (variant, project, properties, code) in variants)
            {
                var variantExit = 0;
                var output = DotNet($"build {project} -c Release -p:DuckyVersion={version} {properties:nq}",
                        logOutput: false, exitHandler: p => variantExit = p.ExitCode)
                    .Select(o => o.Text)
                    .ToList();
                if (variantExit == 0 || !output.Any(line => line.Contains($"error {code}", StringComparison.Ordinal)))
                {
                    // The error lines tell a failed restore (e.g. NU1101) from a missing diagnostic.
                    var errors = output.Where(line => line.Contains(": error ", StringComparison.Ordinal)).Distinct().ToList();
                    (errors.Count > 0 ? errors : output.TakeLast(20)).ToList().ForEach(line => Log.Warning("{Line}", line));
                    violations.Add($"{variant} must fail with {code} (exit code {variantExit})");
                }
            }

            violations.ForEach(v => Log.Error(v));
            Assert.True(violations.Count == 0, $"PackageSmoke: {violations.Count} violation(s)");
            Log.Information("PackageSmoke: Ducky {Version} packages passed the step-1 checks", version);
        });

    // §19: the build reads the version only through MinVer's property; it is set by MinVer's target, not at evaluation.
    private string MinVerVersion()
    {
        var version = DotNet($"msbuild {RootDirectory / "src" / "Ducky" / "Ducky.csproj"} -nologo -t:MinVer -getProperty:MinVerVersion -p:Configuration=Release",
                logOutput: false)
            .Select(o => o.Text.Trim())
            .LastOrDefault(t => t.Length > 0);
        Assert.True(!string.IsNullOrEmpty(version), "MinVerVersion is empty: is the MinVer GlobalPackageReference restored?");
        return version!;
    }

    private static List<string> FileNames(AbsolutePath directory) =>
        Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(directory, f).Replace('\\', '/'))]
            : [];

    // R-PKG-6: exactly the five libraries plus their symbols at the MinVer version, and the tombstone on its own.
    private static IEnumerable<string> PackageSetViolations(string version, List<string> packages, List<string> tombstones)
    {
        var expected = LibraryPackages.SelectMany(id => new[] { $"{id}.{version}.nupkg", $"{id}.{version}.snupkg" }).ToHashSet();
        foreach (var missing in expected.Except(packages).Order(StringComparer.Ordinal))
        {
            yield return $"artifacts/packages is missing {missing}";
        }
        foreach (var unexpected in packages.Except(expected).Order(StringComparer.Ordinal))
        {
            yield return $"artifacts/packages holds unexpected {unexpected}";
        }
        if (tombstones is not [TombstonePackage])
        {
            yield return $"artifacts/tombstone must hold exactly {TombstonePackage}, holds [{string.Join(", ", tombstones)}]";
        }
    }

    private static IEnumerable<string> PackSelfCheck()
    {
        const string version = "2.0.0-alpha.0.7";
        static List<string> Libraries(string version) =>
            [.. LibraryPackages.SelectMany(id => new[] { $"{id}.{version}.nupkg", $"{id}.{version}.snupkg" })];
        (string Case, List<string> Packages, List<string> Tombstones, string Expected)[] planted =
        [
            ("an extra nupkg in artifacts/packages", [.. Libraries(version), "Ducky.Tests.2.0.0-alpha.0.7.nupkg"], [TombstonePackage],
                "artifacts/packages holds unexpected Ducky.Tests.2.0.0-alpha.0.7.nupkg"),
            ("a missing .snupkg", [.. Libraries(version).Where(f => f != $"Ducky.Draft.{version}.snupkg")], [TombstonePackage],
                $"artifacts/packages is missing Ducky.Draft.{version}.snupkg"),
            ("packages not at the MinVer version", Libraries("1.0.0"), [TombstonePackage],
                $"artifacts/packages is missing Ducky.{version}.nupkg"),
            ("the tombstone in artifacts/packages", [.. Libraries(version), TombstonePackage], [],
                $"artifacts/packages holds unexpected {TombstonePackage}"),
            ("a second tombstone", Libraries(version), [TombstonePackage, "Ducky.Generator.2.0.1.nupkg"],
                $"artifacts/tombstone must hold exactly {TombstonePackage}"),
        ];
        foreach (var violation in PackageSetViolations(version, Libraries(version), [TombstonePackage]))
        {
            yield return $"PackSelfCheck: the unplanted fixture must pass, got: {violation}";
        }
        foreach (var (plantedCase, packages, tombstones, expected) in planted)
        {
            var violations = PackageSetViolations(version, packages, tombstones).ToList();
            if (!violations.Any(v => v.Contains(expected, StringComparison.Ordinal)))
            {
                yield return $"PackSelfCheck: {plantedCase} must report '{expected}', got [{string.Join("; ", violations)}]";
            }
        }
    }

    private sealed record PackageContent(string Id, XElement Metadata, HashSet<string> Entries)
    {
        public static PackageContent Read(AbsolutePath nupkg)
        {
            using var zip = ZipFile.OpenRead(nupkg);
            using var nuspec = zip.Entries.Single(e => e.FullName == e.Name && e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).Open();
            var metadata = XDocument.Load(nuspec).Root!.Elements().Single(e => e.Name.LocalName == "metadata");
            return new(metadata.Elements().Single(e => e.Name.LocalName == "id").Value, metadata, [.. zip.Entries.Select(e => e.FullName)]);
        }

        private IEnumerable<XElement> Named(string localName) => Metadata.Descendants().Where(e => e.Name.LocalName == localName);

        public List<XElement> Dependencies => [.. Named("dependency")];

        public bool HasDependencyGroups => Named("dependencies").Any();

        public IEnumerable<string> FrameworkReferences => Named("frameworkReference").Select(e => (string?)e.Attribute("name") ?? "");
    }

    // §17.9 step 2 at stage 1 (the nuspec, layout and R-PKG checks the skeleton packages can pass).
    private static IEnumerable<string> NuspecViolations(List<PackageContent> libraries, PackageContent tombstone, List<string> packageFiles)
    {
        var byId = libraries.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var id in LibraryPackages.Where(id => !byId.ContainsKey(id)))
        {
            yield return $"{id}: package not found";
        }
        static string Id(XElement dependency) => (string?)dependency.Attribute("id") ?? "";
        static IEnumerable<string> Layout(PackageContent package, string[] required, Regex? extra = null) =>
            required.Where(e => !package.Entries.Contains(e)).Select(e => $"{package.Id}: the package has no {e}")
                .Concat(package.Entries
                    .Where(e => !required.Contains(e) && !PackagingEntry.IsMatch(e) && extra?.IsMatch(e) != true)
                    .Order(StringComparer.Ordinal)
                    .Select(e => $"{package.Id}: unexpected entry {e} (§3 layout)"));

        foreach (var (id, package) in byId.Where(p => LibraryLayouts.ContainsKey(p.Key)))
        {
            foreach (var violation in Layout(package, [$"lib/net10.0/{id}.dll", $"lib/net10.0/{id}.xml", .. LibraryLayouts[id]],
                id.Equals("Ducky.Blazor", StringComparison.OrdinalIgnoreCase) ? BlazorEntry : null))
            {
                yield return violation;
            }
        }
        foreach (var violation in Layout(tombstone, ["buildTransitive/Ducky.Generator.targets"]))
        {
            yield return violation;
        }

        if (byId.TryGetValue("Ducky", out var ducky))
        {
            var dependencies = ducky.Dependencies.Select(Id).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
            if (!dependencies.SequenceEqual(DuckyDependencies, StringComparer.OrdinalIgnoreCase))
            {
                yield return $"Ducky: dependencies must be exactly [{string.Join(", ", DuckyDependencies)}], are [{string.Join(", ", dependencies)}]";
            }
        }

        // R-PKG-5: an exclude, or an include narrower than All, on the Ducky dependency drops Ducky.Generators.dll and
        // buildTransitive/Ducky.targets. §3: no dependency outside the package's row of the table.
        foreach (var (id, allowed) in DuckyDependents.Where(d => byId.ContainsKey(d.Key)))
        {
            var onDucky = byId[id].Dependencies.Where(d => Id(d).Equals("Ducky", StringComparison.OrdinalIgnoreCase)).ToList();
            if (onDucky.Count == 0)
            {
                yield return $"{id}: no dependency on Ducky";
            }
            foreach (var exclude in onDucky.Select(d => (string?)d.Attribute("exclude")).Where(e => e is not null))
            {
                yield return $"{id}: its Ducky dependency carries exclude=\"{exclude}\" (R-PKG-5)";
            }
            foreach (var include in onDucky.Select(d => (string?)d.Attribute("include")).Where(i => i is not null && !i.Equals("All", StringComparison.OrdinalIgnoreCase)))
            {
                yield return $"{id}: its Ducky dependency carries include=\"{include}\" (R-PKG-5)";
            }
            foreach (var extra in byId[id].Dependencies.Select(Id).Where(d => !allowed.Contains(d, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return $"{id}: dependency {extra} is not in its §3 row [{string.Join(", ", allowed)}]";
            }
        }

        if (byId.TryGetValue("Ducky.Draft", out var draft) && draft.Dependencies.Count > 0)
        {
            yield return $"Ducky.Draft: must have zero dependencies, has [{string.Join(", ", draft.Dependencies.Select(Id))}]";
        }

        if (tombstone.HasDependencyGroups)
        {
            yield return "Ducky.Generator 2.0.0: the tombstone's nuspec must have no dependency groups (SuppressDependenciesWhenPacking)";
        }
        foreach (var file in packageFiles.Where(f => f.StartsWith("Ducky.Generator.", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)))
        {
            yield return $"artifacts/packages holds {file}: the tombstone belongs only in artifacts/tombstone";
        }

        foreach (var package in libraries.Append(tombstone))
        {
            foreach (var dependency in package.Dependencies)
            {
                var range = (string?)dependency.Attribute("version") ?? "";
                if (range.StartsWith('[') && range.EndsWith(']') && !range.Contains(','))
                {
                    yield return $"{package.Id}: dependency {Id(dependency)} uses the exact range {range}; pack a minimum version";
                }
                if (ForbiddenPackage.IsMatch(Id(dependency)))
                {
                    yield return $"{package.Id}: dependency {Id(dependency)} is forbidden (R-PKG-1)";
                }
            }
            foreach (var framework in package.FrameworkReferences.Where(f => f.Equals("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase)))
            {
                yield return $"{package.Id}: framework reference {framework} is forbidden (R-PKG-1)";
            }
        }
    }

    private static IEnumerable<string> NuspecSelfCheck()
    {
        static PackageContent Package(string id, string? dependencies, params string[] entries) => new(id,
            XElement.Parse($"<metadata><id>{id}</id>{(dependencies is null ? "" : $"<dependencies><group targetFramework=\"net10.0\">{dependencies}</group></dependencies>")}</metadata>"),
            [$"{id}.nuspec", "_rels/.rels", "[Content_Types].xml", .. id == "Ducky.Generator" ? [] : new[] { $"lib/net10.0/{id}.dll", $"lib/net10.0/{id}.xml" }, .. entries]);
        const string onDucky = "<dependency id=\"Ducky\" version=\"2.0.0\" />";
        static List<PackageContent> Libraries(Func<PackageContent, PackageContent>? plant = null) =>
        [
            .. new[]
            {
                Package("Ducky", "<dependency id=\"Microsoft.Extensions.DependencyInjection.Abstractions\" version=\"10.0.12\" /><dependency id=\"Microsoft.Extensions.Logging.Abstractions\" version=\"10.0.12\" />",
                    "analyzers/dotnet/cs/Ducky.Generators.dll", "buildTransitive/Ducky.targets"),
                Package("Ducky.Blazor", onDucky, "staticwebassets/ducky.js", "build/Ducky.Blazor.props", "buildTransitive/Ducky.Blazor.props"),
                Package("Ducky.Draft", null, "analyzers/dotnet/cs/Ducky.Draft.Generators.dll", "buildTransitive/Ducky.Draft.targets"),
                Package("Ducky.Reactive", onDucky + "<dependency id=\"R3\" version=\"1.3.1\" />"),
                Package("Ducky.Testing", onDucky),
            }.Select(p => plant?.Invoke(p) ?? p),
        ];
        static PackageContent Replace(PackageContent package, string id, string? dependencies, params string[] entries) =>
            package.Id == id ? Package(id, dependencies, entries.Length > 0 ? entries : [.. package.Entries]) : package;
        var tombstone = Package("Ducky.Generator", null, "buildTransitive/Ducky.Generator.targets");
        List<string> files = ["Ducky.2.0.0.nupkg"];
        (string Case, List<string> Violations, string Expected)[] planted =
        [
            ("exclude=\"Build,Analyzers\" on Ducky.Blazor's Ducky dependency",
                [.. NuspecViolations(Libraries(p => Replace(p, "Ducky.Blazor", "<dependency id=\"Ducky\" version=\"2.0.0\" exclude=\"Build,Analyzers\" />")), tombstone, files)],
                "Ducky.Blazor: its Ducky dependency carries exclude=\"Build,Analyzers\""),
            ("an xunit dependency of Ducky",
                [.. NuspecViolations(Libraries(p => Replace(p, "Ducky", "<dependency id=\"Microsoft.Extensions.DependencyInjection.Abstractions\" version=\"10.0.12\" /><dependency id=\"Microsoft.Extensions.Logging.Abstractions\" version=\"10.0.12\" /><dependency id=\"xunit.v3.mtp-v2\" version=\"4.0.1\" />")), tombstone, files)],
                "Ducky: dependency xunit.v3.mtp-v2 is forbidden (R-PKG-1)"),
            ("Ducky without its Extensions abstractions",
                [.. NuspecViolations(Libraries(p => Replace(p, "Ducky", null)), tombstone, files)],
                "Ducky: dependencies must be exactly"),
            ("Ducky without its generator",
                [.. NuspecViolations(Libraries(p => Replace(p, "Ducky", "<dependency id=\"Microsoft.Extensions.DependencyInjection.Abstractions\" version=\"10.0.12\" /><dependency id=\"Microsoft.Extensions.Logging.Abstractions\" version=\"10.0.12\" />", "buildTransitive/Ducky.targets")), tombstone, files)],
                "Ducky: the package has no analyzers/dotnet/cs/Ducky.Generators.dll"),
            ("a Ducky.Draft dependency", [.. NuspecViolations(Libraries(p => Replace(p, "Ducky.Draft", onDucky)), tombstone, files)],
                "Ducky.Draft: must have zero dependencies, has [Ducky]"),
            ("an exact R3 range", [.. NuspecViolations(Libraries(p => Replace(p, "Ducky.Reactive", onDucky + "<dependency id=\"R3\" version=\"[1.3.1]\" />")), tombstone, files)],
                "Ducky.Reactive: dependency R3 uses the exact range [1.3.1]"),
            ("Ducky.Testing without a Ducky dependency", [.. NuspecViolations(Libraries(p => Replace(p, "Ducky.Testing", "")), tombstone, files)],
                "Ducky.Testing: no dependency on Ducky"),
            ("a tombstone dependency group", [.. NuspecViolations(Libraries(), Package("Ducky.Generator", ""), files)],
                "the tombstone's nuspec must have no dependency groups"),
            ("the tombstone in artifacts/packages", [.. NuspecViolations(Libraries(), tombstone, [.. files, TombstonePackage])],
                $"artifacts/packages holds {TombstonePackage}"),
            ("a Microsoft.AspNetCore.App framework reference",
                [.. NuspecViolations(Libraries(p => p.Id == "Ducky.Blazor" ? p with { Metadata = XElement.Parse("<metadata><id>Ducky.Blazor</id><dependencies><group targetFramework=\"net10.0\"><dependency id=\"Ducky\" version=\"2.0.0\" /></group></dependencies><frameworkReferences><group targetFramework=\"net10.0\"><frameworkReference name=\"Microsoft.AspNetCore.App\" /></group></frameworkReferences></metadata>") } : p), tombstone, files)],
                "Ducky.Blazor: framework reference Microsoft.AspNetCore.App is forbidden (R-PKG-1)"),
            ("a stryker-config.json Content item packed into Ducky.Blazor",
                [.. NuspecViolations(Libraries(p => p.Id == "Ducky.Blazor" ? p with { Entries = [.. p.Entries, "content/stryker-config.json", "contentFiles/any/net10.0/stryker-config.json"] } : p), tombstone, files)],
                "Ducky.Blazor: unexpected entry contentFiles/any/net10.0/stryker-config.json"),
            ("a generator DLL in Ducky's lib/",
                [.. NuspecViolations(Libraries(p => p.Id == "Ducky" ? p with { Entries = [.. p.Entries, "lib/net10.0/Ducky.Generators.dll"] } : p), tombstone, files)],
                "Ducky: unexpected entry lib/net10.0/Ducky.Generators.dll"),
            ("a readme in the tombstone",
                [.. NuspecViolations(Libraries(), tombstone with { Entries = [.. tombstone.Entries, "README.md"] }, files)],
                "Ducky.Generator: unexpected entry README.md"),
            ("include=\"Compile,Runtime\" on Ducky.Testing's Ducky dependency",
                [.. NuspecViolations(Libraries(p => Replace(p, "Ducky.Testing", "<dependency id=\"Ducky\" version=\"2.0.0\" include=\"Compile,Runtime\" />")), tombstone, files)],
                "Ducky.Testing: its Ducky dependency carries include=\"Compile,Runtime\""),
            ("a pinned transitive dependency promoted into Ducky.Reactive",
                [.. NuspecViolations(Libraries(p => Replace(p, "Ducky.Reactive", onDucky + "<dependency id=\"R3\" version=\"1.3.1\" /><dependency id=\"Microsoft.Extensions.Logging.Abstractions\" version=\"10.0.12\" exclude=\"Analyzers\" />")), tombstone, files)],
                "Ducky.Reactive: dependency Microsoft.Extensions.Logging.Abstractions is not in its §3 row"),
            ("a missing library", [.. NuspecViolations([.. Libraries().Where(p => p.Id != "Ducky.Testing")], tombstone, files)],
                "Ducky.Testing: package not found"),
        ];
        foreach (var violation in NuspecViolations(Libraries(), tombstone, files))
        {
            yield return $"NuspecSelfCheck: the unplanted fixture must pass, got: {violation}";
        }
        foreach (var (plantedCase, violations, expected) in planted.Where(p => !p.Violations.Any(v => v.Contains(p.Expected, StringComparison.Ordinal))))
        {
            yield return $"NuspecSelfCheck: {plantedCase} must report '{expected}', got [{string.Join("; ", violations)}]";
        }
        (string Id, bool Forbidden)[] ids =
        [
            ("Microsoft.CodeAnalysis.CSharp", true), ("xunit", true), ("xunit.v3.assert", true), ("XUnit.Core", true), ("bunit", true),
            ("System.Reactive", true), ("Microsoft.Reactive.Testing", true), ("FakeItEasy", true),
            ("Microsoft.CodeAnalysisX", false), ("bunit.web", false), ("System.Reactive.Linq", false), ("R3", false),
            ("Microsoft.AspNetCore.Components.Analyzers", false),
        ];
        foreach (var (id, forbidden) in ids.Where(i => ForbiddenPackage.IsMatch(i.Id) != i.Forbidden))
        {
            yield return $"NuspecSelfCheck: {id} must {(forbidden ? "" : "not ")}match R-PKG-1";
        }
    }

    // §17.9 step 3: R-PKG-1 over the consumers' whole restore graph, top-level and transitive. An empty graph proves
    // nothing, so every expected project must be listed with a framework whose top-level packages include a Ducky package.
    private static IEnumerable<string> ForbiddenPackages(JsonDocument graph, IEnumerable<string> expectedProjects)
    {
        var frameworks = graph.RootElement.GetProperty("projects").EnumerateArray()
            .SelectMany(project => project.TryGetProperty("frameworks", out var list) ? list.EnumerateArray().Select(f => (Project: Path.GetFileNameWithoutExtension(project.GetProperty("path").GetString())!, Framework: f)) : [])
            .ToList();
        static IEnumerable<string> Ids(JsonElement framework, string kind) =>
            framework.TryGetProperty(kind, out var list) ? list.EnumerateArray().Select(p => p.GetProperty("id").GetString()!) : [];
        foreach (var project in expectedProjects.Where(p => !frameworks.Any(f => f.Project == p && Ids(f.Framework, "topLevelPackages").Any(id => id.StartsWith("Ducky", StringComparison.OrdinalIgnoreCase)))))
        {
            yield return $"{project}: missing from the restore graph, or without a top-level Ducky package";
        }
        foreach (var (project, id) in frameworks.SelectMany(f => ListedPackageKinds.SelectMany(kind => Ids(f.Framework, kind)).Select(id => (f.Project, id)))
            .Where(p => ForbiddenPackage.IsMatch(p.id)).Distinct())
        {
            yield return $"{project}: {id} is in the restore graph (R-PKG-1)";
        }
    }

    private static IEnumerable<string> GraphSelfCheck()
    {
        static string Project(string name, string frameworks) => $$"""{"path":"/smoke/{{name}}/{{name}}.csproj"{{frameworks}}}""";
        static string Framework(string topLevel, string transitive = "") =>
            $$""","frameworks":[{"framework":"net10.0","topLevelPackages":[{{topLevel}}],"transitivePackages":[{{transitive}}]}]""";
        const string ducky = """{"id":"Ducky","requestedVersion":"2.0.0","resolvedVersion":"2.0.0"}""";
        static List<string> Run(params string[] projects)
        {
            using var graph = JsonDocument.Parse($$"""{"version":1,"projects":[{{string.Join(',', projects)}}]}""");
            return [.. ForbiddenPackages(graph, ["Graph"])];
        }
        (string Case, List<string> Violations, string Expected)[] planted =
        [
            ("a transitive xunit.v3.core", Run(Project("Graph", Framework(ducky, """{"id":"xunit.v3.core","resolvedVersion":"3.0.0"}"""))),
                "Graph: xunit.v3.core is in the restore graph (R-PKG-1)"),
            ("a project missing from the graph", Run(), "Graph: missing from the restore graph"),
            ("a project without frameworks", Run(Project("Graph", "")), "Graph: missing from the restore graph"),
            ("a framework without a Ducky package", Run(Project("Graph", Framework(""))), "Graph: missing from the restore graph"),
        ];
        foreach (var violation in Run(Project("Graph", Framework(ducky))))
        {
            yield return $"GraphSelfCheck: the unplanted graph must pass, got: {violation}";
        }
        foreach (var (plantedCase, violations, expected) in planted.Where(p => !p.Violations.Any(v => v.StartsWith(p.Expected, StringComparison.Ordinal))))
        {
            yield return $"GraphSelfCheck: {plantedCase} must report '{expected}', got [{string.Join("; ", violations)}]";
        }
    }
}
