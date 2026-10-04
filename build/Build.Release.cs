using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Utilities;
using Serilog;
using YamlDotNet.RepresentationModel;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// SPEC §3 (R-PKG-1, R-PKG-5, R-PKG-6), §17.9 and §19: Pack and PackageSmoke. PackageSmoke is staged (§17.9): this file
// holds the step-1 checks; later consumers and assertions arrive with their features.
// SPEC §19, §20.1 and §21 (M16-03): the release targets Changelog, Publish, GitHubRelease, ReleaseGates, MutationForSha
// and Release, and the checks of the hand-written release.yml.
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

    private const string NuGetSource = "https://api.nuget.org/v3/index.json";

    // ADR-0045: the tombstone is pushed once, with exactly this tag; prerelease and patch tags never push it (§19, §21).
    private const string TombstoneTag = "v2.0.0";

    private static readonly Regex VersionTag = new(@"^v\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);

    // -rc.N and GA (or patch) tags release every stage: SpecTraceGate and Mutation run --strict-stages (§19).
    private static readonly Regex StrictTag = new(@"^v\d+\.\d+\.\d+(-rc\.\d+)?$", RegexOptions.CultureInvariant);

    // git-cliff, from Conventional Commits (cliff.toml); not a dotnet tool, so release.yml installs a pinned binary.
    private Target Changelog => _ => _
        .Before(Publish)
        .Executes(() =>
        {
            var cliff = OperatingSystem.IsWindows() ? "git-cliff.exe" : "git-cliff";
            Assert.True(EnvironmentInfo.Paths.Any(p => File.Exists(Path.Combine(p, cliff))),
                "Changelog: git-cliff is not on PATH; install it (https://git-cliff.org/docs/installation: `brew install git-cliff` or `cargo install git-cliff`)");
            var version = ReleaseVersionOrDefault();
            ProcessTasks.StartProcess("git-cliff", $"--tag v{version} -o CHANGELOG.md", RootDirectory).AssertZeroExitCode();
            Artifacts.CreateDirectory();
            ProcessTasks.StartProcess("git-cliff", $"--latest --strip header -o {Artifacts / "notes.md"}", RootDirectory).AssertZeroExitCode();
        });

    // No DependsOn: in release.yml it pushes the packages release-gates built and uploaded, and a dependency would
    // rebuild the tree while the short-lived key is live. Every check runs before the key is read.
    private Target Publish => _ => _
        .After(Ci, E2E, AotSmoke, MutationForSha, PackageSmoke)
        .Executes(() =>
        {
            FailOnViolations(nameof(Publish), ReleaseSelfCheck());
            var tag = ReleaseTag();
            FailOnViolations(nameof(Publish),
                [.. PublishViolations(tag, ReleaseBranch, t => IsReachable(RootDirectory, t, ReleaseBranch), FileNames(Packages), FileNames(Tombstones))]);
            var apiKey = EnvironmentInfo.GetVariable("NUGET_API_KEY");
            Assert.True(!string.IsNullOrEmpty(apiKey), "Publish: NUGET_API_KEY is not set (release.yml bridges it from NuGet/login)");
            // Each .nupkg push also pushes the .snupkg beside it; no committed nuget.config sets a push source.
            foreach (var directory in PushedDirectories(tag!))
            {
                DotNet($"nuget push {Artifacts / directory / "*.nupkg"} --source {NuGetSource} --api-key {apiKey:r} --skip-duplicate");
            }
        });

    // --verify-tag: never let gh create a missing tag on the default branch. It opens no PR (§19).
    private Target GitHubRelease => _ => _
        .DependsOn(Publish, Changelog)
        .Executes(() =>
        {
            var version = ReleaseVersionOrDefault();
            var assets = string.Join(' ', Packages.GlobFiles("*").Select(f => f.ToString().DoubleQuoteIfNeeded()));
            var prerelease = IsPrerelease(version) ? " --prerelease" : "";
            ProcessTasks.StartProcess("gh", $"release create v{version} {assets:nq} --verify-tag --notes-file {Artifacts / "notes.md"}{prerelease:nq}",
                RootDirectory).AssertZeroExitCode();
        });

    // release.yml uploads artifacts/packages and artifacts/tombstone after it; SpecTraceGate (in Ci) is strict for -rc.N
    // and GA tags when ReleaseGates or Release is invoked.
    private Target ReleaseGates => _ => _
        .DependsOn(Ci, E2E, AotSmoke);

    // DependsOn(Compile), as Mutation: the fallback runs Mutation's body in this process, since a child build could not open
    // the build.log this run holds. The cost is a Compile in the release-mutation job even when a nightly run passes it.
    private Target MutationForSha => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            FailOnViolations(nameof(MutationForSha), ReleaseSelfCheck());
            var sha = EnvironmentInfo.GetVariable("GITHUB_SHA") is { Length: > 0 } s ? s : Git(RootDirectory, $"rev-parse HEAD").Single();
            var strict = StrictStages || ReleaseTag() is { } tag && StrictTag.IsMatch(tag);
            var failure = NightlyMutationOrRun(sha,
                () =>
                {
                    var query = ProcessTasks.StartProcess("gh", $"run list --workflow nightly-mutation.yml --commit {sha} --status success --json databaseId --limit 1",
                        RootDirectory, logOutput: false).AssertWaitForExit();
                    // stdout alone is the JSON; a failure reports stderr too.
                    return (query.ExitCode, string.Join('\n', query.Output.Where(o => query.ExitCode != 0 || o.Type == OutputType.Std).Select(o => o.Text)));
                },
                () => RunMutation(strict));
            Assert.True(failure is null, failure);
        });

    // Local convenience only; release.yml splits it into three jobs (§20.1).
    private Target Release => _ => _
        .DependsOn(ReleaseGates, MutationForSha, Publish, GitHubRelease);

    // The tag being released: the pushed tag in release.yml, else the one v* tag on HEAD (the local Release chain).
    private string? ReleaseTag() =>
        EnvironmentInfo.GetVariable("GITHUB_REF") is { } gitRef && gitRef.StartsWith("refs/tags/", StringComparison.Ordinal)
            ? gitRef["refs/tags/".Length..]
            : Git(RootDirectory, $"tag --points-at HEAD --list v*") is [var tag] ? tag : null;

    private string ReleaseVersionOrDefault() =>
        ReleaseVersion ?? (ReleaseTag() is { } tag && VersionTag.IsMatch(tag) ? tag[1..] : MinVerVersion());

    // SpecTraceGate's --strict: the switch, or a -rc.N/GA tag under ReleaseGates (§17.1, §19).
    private bool StrictSpecTrace =>
        StrictStages || (InvokedTargets.Any(t => t.Name is nameof(ReleaseGates) or nameof(Release)) && ReleaseTag() is { } tag && StrictTag.IsMatch(tag));

    // gh's --prerelease for -alpha.N and -rc.N (any SemVer prerelease).
    private static bool IsPrerelease(string version) => version.Contains('-', StringComparison.Ordinal);

    private static string[] PushedDirectories(string tag) => tag == TombstoneTag ? ["packages", "tombstone"] : ["packages"];

    // §19 Publish, before any push: a v* tag, Pack's exact-set check at the tag's version (Publish runs no MinVer: nothing
    // is restored in the publish job), and the tag reachable from origin/{ReleaseBranch} (tag rulesets can't enforce it, §20.2).
    private static IEnumerable<string> PublishViolations(string? tag, string releaseBranch, Func<string, bool> reachable, List<string> packages, List<string> tombstones)
    {
        if (tag is null || !VersionTag.IsMatch(tag))
        {
            yield return $"Publish requires a v* version tag on the released commit, got '{tag}'";
            yield break;
        }
        foreach (var violation in PackageSetViolations(tag[1..], packages, tombstones))
        {
            yield return violation;
        }
        if (!reachable(tag))
        {
            yield return $"{tag} is not reachable from origin/{releaseBranch}: release tags are cut from {releaseBranch}";
        }
    }

    // git merge-base --is-ancestor: 0 reachable, 1 not; any other exit (an unknown ref, no origin/{branch}) is an error.
    private static bool IsReachable(AbsolutePath repository, string tag, string branch)
    {
        var exitCode = ProcessTasks.StartProcess("git", $"merge-base --is-ancestor {tag} origin/{branch}", repository, logOutput: false).AssertWaitForExit().ExitCode;
        Assert.True(exitCode is 0 or 1, $"git merge-base --is-ancestor {tag} origin/{branch} failed (exit code {exitCode}): is origin/{branch} fetched?");
        return exitCode == 0;
    }

    // §19 MutationForSha: a successful nightly-mutation run for the SHA passes; no run starts the full Mutation. A failed or
    // unreadable query fails: reading it as "no run" would silently start a 330-minute run.
    private static string? NightlyMutationOrRun(string sha, Func<(int ExitCode, string Output)> query, Action runMutation)
    {
        var (exitCode, output) = query();
        if (exitCode != 0)
        {
            return $"MutationForSha: the nightly-mutation query failed (exit code {exitCode}), so no full run is started: {output}";
        }
        JsonNode? runs;
        try
        {
            runs = JsonNode.Parse(output);
        }
        catch (JsonException)
        {
            runs = null;
        }
        if (runs is not JsonArray found)
        {
            return $"MutationForSha: the nightly-mutation query returned no JSON array, so no full run is started: {output}";
        }
        if (found.Count > 0)
        {
            Log.Information("MutationForSha: a successful nightly-mutation run exists for {Sha}", sha);
            return null;
        }
        Log.Information("MutationForSha: no successful nightly-mutation run for {Sha}; running Mutation", sha);
        runMutation();
        return null;
    }

    // §20.1: the hand-written release.yml keeps its gates in front of the push: v* tags only, the three jobs with full
    // history, publish behind both gate jobs (no if, no continue-on-error) and the nuget environment's approval, running
    // Publish GitHubRelease unskipped.
    private static IEnumerable<string> ReleaseWorkflowViolations(string workflow)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(workflow));
        var root = stream.Documents.FirstOrDefault()?.RootNode;
        static YamlNode? At(YamlNode? node, params string[] path)
        {
            foreach (var key in path)
            {
                node = node is YamlMappingNode map && map.Children.TryGetValue(new YamlScalarNode(key), out var child) ? child : null;
            }
            return node;
        }
        static List<string> Scalars(YamlNode? node) => node switch
        {
            YamlScalarNode scalar => [scalar.Value!],
            YamlSequenceNode list => [.. list.Children.OfType<YamlScalarNode>().Select(s => s.Value!)],
            YamlMappingNode map => [.. map.Children.Select(c => $"{c.Key}: {c.Value}")],
            _ => [],
        };

        if (At(root, "on") is not YamlMappingNode on || on.Children.Count != 1 || Scalars(At(on, "push", "tags")) is not ["v*"] || At(on, "push") is not YamlMappingNode { Children.Count: 1 })
        {
            yield return "release.yml must run on v* tags only (on: push: tags: ['v*'])";
        }

        (string Job, string Run, string[] Permissions)[] expected =
        [
            ("release-gates", "./build.sh ReleaseGates", []),
            ("release-mutation", "./build.sh MutationForSha", ["actions: read", "contents: read", "issues: write"]),
            ("publish", "./build.sh Publish GitHubRelease", ["contents: write", "id-token: write"]),
        ];
        var jobs = Scalars(At(root, "jobs")).Select(j => j[..j.IndexOf(':', StringComparison.Ordinal)]).Order(StringComparer.Ordinal).ToList();
        if (!jobs.SequenceEqual(expected.Select(e => e.Job).Order(StringComparer.Ordinal)))
        {
            yield return $"release.yml must have exactly the jobs [{string.Join(", ", expected.Select(e => e.Job))}], has [{string.Join(", ", jobs)}]";
        }
        foreach (var (job, run, permissions) in expected)
        {
            var steps = (At(root, "jobs", job, "steps") as YamlSequenceNode)?.Children ?? [];
            if (!steps.Any(s => Scalars(At(s, "uses")) is [var uses] && uses.StartsWith("actions/checkout@", StringComparison.Ordinal)
                    && Scalars(At(s, "with", "fetch-depth")) is ["0"]))
            {
                yield return $"{job}: checkout must set fetch-depth: 0 (MinVer, git-cliff and the reachability check need full history)";
            }
            var runs = steps.SelectMany(s => Scalars(At(s, "run"))).SelectMany(r => r.Split('\n')).Select(l => l.Trim()).ToList();
            if (!runs.Contains(run, StringComparer.Ordinal) || runs.Any(r => r.StartsWith("./build.sh", StringComparison.Ordinal) && r != run))
            {
                yield return $"{job}: must run exactly {run} (no --skip, no other build invocation)";
            }
            if (permissions.Length > 0 && !Scalars(At(root, "jobs", job, "permissions")).Order(StringComparer.Ordinal).SequenceEqual(permissions))
            {
                yield return $"{job}: permissions must be [{string.Join(", ", permissions)}]";
            }
            if (At(root, "jobs", job, "continue-on-error") is not null || steps.Any(s => At(s, "continue-on-error") is not null))
            {
                yield return $"{job}: must not set continue-on-error (a failing gate would count as a success)";
            }
            if (steps.Any(s => At(s, "if") is not null && Scalars(At(s, "run")).Any(r => r.Contains("./build.sh", StringComparison.Ordinal))))
            {
                yield return $"{job}: the ./build.sh step must have no if (a skipped gate passes)";
            }
        }

        if (Scalars(At(root, "jobs", "publish", "environment")) is not ["nuget"] && Scalars(At(root, "jobs", "publish", "environment", "name")) is not ["nuget"])
        {
            yield return "publish: environment must be nuget (its required reviewer approves every push)";
        }
        var needs = Scalars(At(root, "jobs", "publish", "needs"));
        if (!needs.Contains("release-gates") || !needs.Contains("release-mutation"))
        {
            yield return "publish: needs must hold release-gates and release-mutation (nothing is pushed before every gate passed)";
        }
        if (At(root, "jobs", "publish", "if") is not null)
        {
            yield return "publish: must have no if (always() or !cancelled() would run it after a failed gate)";
        }
    }

    // The acceptance checks of M16-03: the tombstone and prerelease decisions, the reachability refusal on a planted
    // repository, a failing gh query, and release.yml's gates on a miniature workflow.
    private static List<string> ReleaseSelfCheck()
    {
        var failures = new List<string>();
        void Expect(bool condition, string message)
        {
            if (!condition)
            {
                failures.Add($"ReleaseSelfCheck: {message}");
            }
        }

        foreach (var (tag, expected) in new[]
                 {
                     ("v2.0.0-alpha.1", "packages"), ("v2.0.0-rc.1", "packages"), ("v2.0.1", "packages"), ("v2.0.0", "packages, tombstone"),
                 })
        {
            Expect(string.Join(", ", PushedDirectories(tag)) == expected, $"{tag} must push [{expected}], pushes [{string.Join(", ", PushedDirectories(tag))}]");
        }
        foreach (var (version, prerelease) in new[] { ("2.0.0-alpha.1", true), ("2.0.0-rc.2", true), ("2.0.0", false), ("2.0.1", false) })
        {
            Expect(IsPrerelease(version) == prerelease, $"{version} must {(prerelease ? "" : "not ")}be a prerelease");
        }
        foreach (var (tag, strict) in new[] { ("v2.0.0-alpha.1", false), ("v2.0.0-rc.1", true), ("v2.0.0", true), ("v2.0.1", true) })
        {
            Expect(StrictTag.IsMatch(tag) == strict, $"{tag} must {(strict ? "" : "not ")}run --strict-stages");
        }

        // A repository whose origin/v2 holds v2.0.0-alpha.1, with v2.0.0-alpha.2 on a commit off that branch.
        var repository = (AbsolutePath)Directory.CreateTempSubdirectory("ducky-release-").FullName;
        try
        {
            const string commit = "-c user.name=Release -c user.email=release@localhost -c commit.gpgsign=false commit --quiet --allow-empty --no-verify -m";
            // Lightweight, unsigned tags whatever the developer's tag.gpgSign (a signed tag would wait for a message).
            const string tag = "-c tag.gpgSign=false tag";
            Git(repository, $"init --quiet --initial-branch=v2");
            Git(repository, $"{commit:nq} on-v2");
            Git(repository, $"{tag:nq} v2.0.0-alpha.1");
            Git(repository, $"update-ref refs/remotes/origin/v2 HEAD");
            Git(repository, $"checkout --quiet -b topic");
            Git(repository, $"{commit:nq} off-v2");
            Git(repository, $"{tag:nq} v2.0.0-alpha.2");
            static List<string> Libraries(string version) =>
                [.. LibraryPackages.SelectMany(id => new[] { $"{id}.{version}.nupkg", $"{id}.{version}.snupkg" })];
            List<string> Violations(string? tag, List<string> packages) =>
                [.. PublishViolations(tag, "v2", t => IsReachable(repository, t, "v2"), packages, [TombstonePackage])];

            var reachable = Violations("v2.0.0-alpha.1", Libraries("2.0.0-alpha.1"));
            Expect(reachable.Count == 0, $"v2.0.0-alpha.1 on origin/v2 must publish, got [{string.Join("; ", reachable)}]");
            (string Case, List<string> Violations, string Expected)[] planted =
            [
                ("a tag off origin/v2", Violations("v2.0.0-alpha.2", Libraries("2.0.0-alpha.2")), "v2.0.0-alpha.2 is not reachable from origin/v2"),
                ("no tag", Violations(null, Libraries("2.0.0-alpha.1")), "Publish requires a v* version tag"),
                ("a tag without v", Violations("2.0.0-alpha.1", Libraries("2.0.0-alpha.1")), "Publish requires a v* version tag"),
                ("packages of another version", Violations("v2.0.0-alpha.1", Libraries("2.0.0-alpha.0.7")), "artifacts/packages is missing Ducky.2.0.0-alpha.1.nupkg"),
            ];
            foreach (var (plantedCase, violations, expected) in planted)
            {
                Expect(violations.Any(v => v.Contains(expected, StringComparison.Ordinal)), $"{plantedCase} must report '{expected}', got [{string.Join("; ", violations)}]");
            }
        }
        finally
        {
            repository.DeleteDirectory();
        }

        // MutationForSha: only a successful query decides; a failure never starts the full run.
        (string Case, int ExitCode, string Output, bool Fails, bool Runs)[] queries =
        [
            ("a failing gh query (HTTP 403)", 1, "HTTP 403: Resource not accessible by integration", true, false),
            ("a query that is not JSON", 0, "<html>rate limited</html>", true, false),
            ("no successful nightly-mutation run", 0, "[]", false, true),
            ("a successful nightly-mutation run", 0, "[{\"databaseId\":42}]", false, false),
        ];
        foreach (var (queryCase, exitCode, output, fails, runs) in queries)
        {
            var ran = false;
            var failure = NightlyMutationOrRun("abc123", () => (exitCode, output), () => ran = true);
            Expect((failure is not null) == fails && ran == runs,
                $"{queryCase} must {(fails ? "fail" : "pass")} and {(runs ? "" : "not ")}run Mutation; got failure '{failure}', ran {ran}");
        }

        // release.yml: publish waits for both gate jobs and the nuget environment's approval.
        const string workflow = """
            on:
              push:
                tags: ['v*']
            jobs:
              release-gates:
                steps:
                  - uses: actions/checkout@v7
                    with:
                      fetch-depth: 0
                  - run: ./build.sh ReleaseGates
              release-mutation:
                permissions:
                  contents: read
                  actions: read
                  issues: write
                steps:
                  - uses: actions/checkout@v7
                    with:
                      fetch-depth: 0
                  - run: ./build.sh MutationForSha
              publish:
                needs: [release-gates, release-mutation]
                environment: nuget
                permissions:
                  id-token: write
                  contents: write
                steps:
                  - uses: actions/checkout@v7
                    with:
                      fetch-depth: 0
                  - run: ./build.sh Publish GitHubRelease
            """;
        var unplanted = ReleaseWorkflowViolations(workflow).ToList();
        Expect(unplanted.Count == 0, $"the unplanted release.yml must pass, got [{string.Join("; ", unplanted)}]");
        (string Case, string From, string To, string Expected)[] workflows =
        [
            ("publish without the nuget environment", "    environment: nuget\n", "", "publish: environment must be nuget"),
            ("publish not waiting for release-mutation", "[release-gates, release-mutation]", "[release-gates]", "publish: needs must hold release-gates and release-mutation"),
            ("a shallow checkout", "          fetch-depth: 0\n      - run: ./build.sh MutationForSha", "          fetch-depth: 1\n      - run: ./build.sh MutationForSha",
                "release-mutation: checkout must set fetch-depth: 0"),
            ("publish skipping targets", "./build.sh Publish GitHubRelease", "./build.sh Publish GitHubRelease --skip Restore", "publish: must run exactly ./build.sh Publish GitHubRelease"),
            ("release-mutation without issues: write", "      issues: write\n", "      issues: read\n", "release-mutation: permissions must be"),
            ("publish without id-token: write", "      id-token: write\n", "", "publish: permissions must be"),
            ("a branch trigger", "tags: ['v*']", "branches: [v2]", "release.yml must run on v* tags only"),
            ("a fourth job", "jobs:\n", "jobs:\n  extra:\n    steps: []\n", "release.yml must have exactly the jobs"),
            ("publish running after a failed gate", "    needs: [release-gates, release-mutation]\n", "    needs: [release-gates, release-mutation]\n    if: always()\n",
                "publish: must have no if"),
            ("a gate job continuing on error", "  release-mutation:\n", "  release-mutation:\n    continue-on-error: true\n", "release-mutation: must not set continue-on-error"),
            ("a gate step continuing on error", "      - run: ./build.sh ReleaseGates\n", "      - run: ./build.sh ReleaseGates\n        continue-on-error: true\n",
                "release-gates: must not set continue-on-error"),
            ("a skipped gate step", "      - run: ./build.sh MutationForSha\n", "      - run: ./build.sh MutationForSha\n        if: false\n",
                "release-mutation: the ./build.sh step must have no if"),
        ];
        foreach (var (workflowCase, from, to, expected) in workflows)
        {
            Expect(workflow.Contains(from, StringComparison.Ordinal), $"{workflowCase}: the fixture lacks '{from}'");
            var violations = ReleaseWorkflowViolations(workflow.Replace(from, to, StringComparison.Ordinal)).ToList();
            Expect(violations.Any(v => v.Contains(expected, StringComparison.Ordinal)), $"{workflowCase} must report '{expected}', got [{string.Join("; ", violations)}]");
        }
        return failures;
    }
}
