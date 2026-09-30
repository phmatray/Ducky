using System.Text.Json;
using Fallout.Common;
using Fallout.Common.IO;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

internal partial class Build
{
    // The coverage exclusion list (§19): the one place it is stated.
    private static readonly string[] CoverageExclusions = ["Ducky.Concurrency.Tests", "Ducky.E2E"];

    private AbsolutePath TestFilter => RootDirectory / "Ducky.Tests.slnf";

    private Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            // Visual Studio saves .slnf paths with backslashes.
            static string Normalize(JsonElement path) => path.GetString()!.Replace('\\', '/');
            var testDirectory = RootDirectory / "test";
            var expected = Solution.AllProjects
                .Where(p => testDirectory.Contains(p.Path))
                .Select(p => p.Name)
                .Except(CoverageExclusions)
                .ToHashSet();
            var solution = JsonDocument.Parse(TestFilter.ReadAllText()).RootElement.GetProperty("solution");
            Assert.True(Normalize(solution.GetProperty("path")) == Solution.FileName,
                $"{TestFilter.Name} must filter {Solution.FileName}");
            var filtered = solution.GetProperty("projects").EnumerateArray()
                .Select(p => Path.GetFileNameWithoutExtension(Normalize(p)))
                .ToHashSet();
            Assert.True(expected.SetEquals(filtered),
                $"{TestFilter.Name} must list the test projects of {Solution.FileName} minus [{string.Join(", ", CoverageExclusions)}]. " +
                $"Missing: [{string.Join(", ", expected.Except(filtered))}]; unexpected: [{string.Join(", ", filtered.Except(expected))}]");

            // MTP names each cobertura file with a new GUID: a stale one would feed CoverageGate old hits.
            (Artifacts / "test").CreateOrCleanDirectory();

            // Fixed property seeds and one CsCheck thread (§17.1); DUCKY_REPEAT passes through from the environment.
            var environment = new Dictionary<string, string>(EnvironmentInfo.Variables)
            {
                ["DUCKY_PROPERTY_SEEDS"] = "1",
                ["CsCheck_Threads"] = "1",
            };
            DotNet($"test --solution {TestFilter} -c Release --no-build --coverage --coverage-output-format cobertura --coverage-settings {CoverageSettings} --report-trx --results-directory {Artifacts / "test"}",
                environmentVariables: environment);
        });

    // Real interleavings, never measured (§17.3): no --coverage, and DUCKY_REPEAT passes through.
    private Target ConcurrencyTest => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            (Artifacts / "concurrency").CreateOrCleanDirectory();
            DotNet($"test --project {RootDirectory / "test" / "Ducky.Concurrency.Tests"} -c Release --no-build --report-trx --results-directory {Artifacts / "concurrency"}");
        });
}
