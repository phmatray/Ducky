using Fallout.Common;
using Fallout.Common.CI.GitHubActions;
using Fallout.Common.CI.GitHubActions.Configuration;
using Fallout.Common.IO;
using Fallout.Components;
using Fallout.Solutions;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// SPEC §19 (ADR-0035): every target is a thin dotnet CLI wrapper; no ITest/IReportCoverage/IPack.
internal sealed partial class Build : FalloutBuild, IHasSolution, IConfigureGitHubActions
{
    public static int Main() => Execute<Build>(x => x.Ci);

    private Solution Solution => ((IHasSolution)this).Solution;

    [Parameter("Branch releases come from; flipped to main by the GA rename PR (§21)")]
    private readonly string ReleaseBranch = "v2";

    [Parameter("PR base for Stryker --since; default origin/$GITHUB_BASE_REF, else origin/{ReleaseBranch}")]
    private readonly string? BaseRef;

    [Parameter("Version (no v) of the release notes, the GitHub release and ShipPublicApi's analyzer release; default GITHUB_REF_NAME minus v, else MinVerVersion")]
    private readonly string? ReleaseVersion;

    private AbsolutePath Artifacts => RootDirectory / "artifacts";

    private Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            foreach (var directory in RootDirectory.GlobDirectories("src/**/bin", "src/**/obj", "test/**/bin", "test/**/obj"))
            {
                directory.DeleteDirectory();
            }
            Artifacts.DeleteDirectory();
        });

    // Always locked, locally too: a stale packages.lock.json fails here, not only in CI.
    private Target Restore => _ => _
        .Executes(() => DotNet($"restore {Solution} --locked-mode"));

    private Target Format => _ => _
        .DependsOn(Restore)
        .Executes(() => DotNet($"format {Solution} --verify-no-changes --no-restore"));

    // Warnings are errors via Directory.Build.props; Directory.Build.targets fails a packable net10.0 src project
    // without IsAotCompatible (§3) and loads PublicApiAnalyzers in every src project, whose RS0016/RS0017 .globalconfig
    // raises to errors. PublicApiSelfCheck proves it on a planted fixture and checks each src project takes it (M0-10).
    private Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNet($"build {Solution} -c Release --no-restore");
            FailOnViolations("Compile", PublicApiSelfCheck().ToList());
        });

    // §19; Test runs through CoverageGate, Pack through PackageSmoke.
    private Target Ci => _ => _
        .DependsOn(Format, ExclusionGate, CoverageGate, ConcurrencyTest, SpecTraceGate, PackageSmoke, VerifyWorkflows, Docs);
}
