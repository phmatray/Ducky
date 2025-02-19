using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.ChangeLog;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Discord;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.OctoVersion;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Octokit;
using Octokit.Internal;
using Serilog;
using Serilog.Core;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.Discord.DiscordTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Project = Nuke.Common.ProjectModel.Project;

// ReSharper disable AllUnderscoreLocalParameterName

[GitHubActions(
    "continuous",
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = false,
    FetchDepth = 0,
    OnPushBranches = [MainBranch, DevelopBranch, ReleasesBranch],
    OnPullRequestBranches = [ReleasesBranch],
    InvokedTargets = [nameof(Pack)],
    EnableGitHubToken = true,
    ImportSecrets = [nameof(NuGetApiKey)]
)]
partial class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Nuke.Common.Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Nuke.Common.Parameter("Nuget Feed Url for Public Access of Pre Releases")]
    readonly string NugetFeed;
    
    [Nuke.Common.Parameter("Nuget Api Key"), Secret]
    readonly string NuGetApiKey;

    [Nuke.Common.Parameter("Copyright Details")]
    readonly string Copyright;

    [Nuke.Common.Parameter("Artifacts Type")]
    readonly string ArtifactsType;

    [Nuke.Common.Parameter("Excluded Artifacts Type")]
    readonly string ExcludedArtifactsType = ".snupkg";
    
    // The Required Attribute will automatically throw an exception if the 
    // OctoVersionInfo parameter is not set due to an error or misconfiguration in Nuke.
    [Required]
    [OctoVersion(
        AutoDetectBranch = true,
        UpdateBuildNumber = true,
        Framework = "net9.0",
        Major = 1)]
    readonly OctoVersionInfo OctoVersionInfo;

    [GitRepository]
    readonly GitRepository GitRepository;
    
    [Solution(SuppressBuildProjectCheck = true, GenerateProjects = true)]
    readonly Solution Solution;
    
    const string MainBranch = "main";
    const string DevelopBranch = "develop";
    const string ReleasesBranch = "releases/**";

    const string PackageContentType = "application/octet-stream";

    GitHubActions GitHubActions => GitHubActions.Instance;
    
    AbsolutePath SourceDirectory => RootDirectory / "src";
    
    AbsolutePath ArtifactsDirectory => RootDirectory / ".artifacts";

    static string ChangeLogFile => RootDirectory / "CHANGELOG.md";
    
    string GithubNugetFeed => GitHubActions != null
        ? $"https://nuget.pkg.github.com/{GitHubActions.RepositoryOwner}/index.json"
        : null;
    
    Target Version => _ => _
        .Description("Logs the GitVersion")
        .Executes(() =>
        {
            Log.Information("GitVersion = {Value}", OctoVersionInfo.NuGetVersion);
        });
    
    Target Clean => _ => _
        .Description("Clean solution and projects directories")
        .Before(Restore)
        .Executes(() =>
        {
            // Clean projects
            DotNetClean(c => c.SetProject(Solution.library.Ducky));
            DotNetClean(c => c.SetProject(Solution.library.Ducky_Blazor));

            DotNetClean(c => c.SetProject(Solution.tests.AppStore_Tests));
            DotNetClean(c => c.SetProject(Solution.tests.Ducky_Tests));
            
            DotNetClean(c => c.SetProject(Solution.demo.Demo_BlazorWasm));
            
            // Clean artifacts directory
            ArtifactsDirectory.CreateOrCleanDirectory();
        });
    
    Target InstallWorkloads => _ => _
        .Description("Install .NET workloads")
        .Executes(() =>
        {
            // this requires sudo on macOS and Linux
            DotNetWorkloadInstall(s => s.SetWorkloadId("wasm-tools"));
        });

    Target Restore => _ => _
        .Description("Restore project dependencies")
        .DependsOn(InstallWorkloads)
        .Executes(() =>
        {
            Log.Information("Restoring NuGet packages");
            DotNetRestore(s => s.SetProjectFile(Solution));
            Log.Information("NuGet packages restored successfully");
        });

    Target Compile => _ => _
        .Description("Compile the solution")
        .DependsOn(Restore)
        .Executes(() =>
        {
            Log.Information("Building solution {Solution} with configuration {Configuration}", Solution, Configuration);
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(OctoVersionInfo.NuGetVersion)
                .SetAssemblyVersion(OctoVersionInfo.MajorMinorPatch)
                .SetInformationalVersion(OctoVersionInfo.InformationalVersion)
                .SetFileVersion(OctoVersionInfo.MajorMinorPatch)
                .SetDeterministic(true)
                .EnableNoRestore());
            Log.Information("Solution built successfully");
        });
    
    Target UnitTests => _ => _
        .Description("Run unit tests")
        .DependsOn(Compile)
        .Executes(() =>
        {
            Log.Information("Running unit tests");
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore());
            Log.Information("Unit tests passed successfully");
        });
    
    Target UpdateChangeLog => _ => _
        .Description("Update the CHANGELOG.md file")
        .Before(Pack)
        .Executes(() =>
        {
        });

    Target Pack => _ => _
        .Description("Pack libraries into NuGet packages with the version")
        .Requires(() => Configuration.Equals(Configuration.Release))
        .Produces(ArtifactsDirectory / ArtifactsType)
        .DependsOn(Compile, UnitTests)
        .Triggers(PublishToGithub, PublishToNuGet)
        .Executes(() =>
        {
            Log.Information("Packing NuGet packages for projects starting with 'Ducky'");
            Project[] projectsToPack =
            [
                Solution.library.Ducky,
                Solution.library.Ducky_Blazor
            ];

            foreach (var project in projectsToPack)
            {
                Log.Information("Packing project {ProjectName}", project.Name);
                DotNetPack(s => s
                    .SetProject(project)
                    .SetConfiguration(Configuration)
                    .SetOutputDirectory(ArtifactsDirectory)
                    .EnableNoBuild()
                    .EnableNoRestore()
                    .SetIncludeSymbols(false)
                    .SetIncludeSource(false)
                    .SetSymbolPackageFormat(DotNetSymbolPackageFormat.snupkg)
                    .SetCopyright(Copyright)
                    .SetVersion(OctoVersionInfo.NuGetVersion)
                    .SetAssemblyVersion(OctoVersionInfo.MajorMinorPatch)
                    .SetInformationalVersion(OctoVersionInfo.InformationalVersion)
                    .SetFileVersion(OctoVersionInfo.MajorMinorPatch));
                Log.Information("Project {ProjectName} packed successfully", project.Name);
            }
        });

    Target PublishToGithub => _ => _
        .Description($"Publishing to Github for Development only.")
        .Triggers(CreateRelease)
        .Requires(() => Configuration.Equals(Configuration.Release))
        .OnlyWhenStatic(() => GitRepository.IsOnDevelopBranch() || GitHubActions?.IsPullRequest == true)
        .Executes(() =>
        {
            ArtifactsDirectory.GlobFiles(ArtifactsType)
                .Where(x => !x.Name.EndsWith(ExcludedArtifactsType))
                .ForEach(x =>
                {
                    DotNetNuGetPush(s => s
                        .SetTargetPath(x)
                        .SetSource(GithubNugetFeed)
                        .SetApiKey(GitHubActions.Token)
                        .EnableSkipDuplicate()
                    );
                });
        });
    
    Target PublishToNuGet => _ => _
        .Description($"Publishing to NuGet with the version.")
        .Triggers(CreateRelease)
        .Requires(() => Configuration.Equals(Configuration.Release))
        .OnlyWhenStatic(() => GitRepository.IsOnMainOrMasterBranch())
        .Executes(() =>
        {
            ArtifactsDirectory.GlobFiles(ArtifactsType)
                .Where(x => !x.Name.EndsWith(ExcludedArtifactsType))
                .ForEach(x =>
                {
                    DotNetNuGetPush(s => s
                        .SetTargetPath(x)
                        .SetSource(NugetFeed)
                        .SetApiKey(NuGetApiKey)
                        .EnableSkipDuplicate()
                    );
                });
        });
    
    Target CreateRelease => _ => _
        .Description($"Creating release for the publishable version.")
        .Requires(() => Configuration.Equals(Configuration.Release))
        .OnlyWhenStatic(() => GitRepository.IsOnMainOrMasterBranch() || GitRepository.IsOnReleaseBranch())
        .Executes(async () =>
        {
            var credentials = new Credentials(GitHubActions.Token);
            GitHubTasks.GitHubClient = new GitHubClient(
                new ProductHeaderValue(nameof(NukeBuild)),
                new InMemoryCredentialStore(credentials));

            string owner = GitRepository.GetGitHubOwner();
            string name = GitRepository.GetGitHubName();

            var releaseTag = OctoVersionInfo?.NuGetVersion;
            var changeLogSectionEntries = ChangelogTasks.ExtractChangelogSectionNotes(ChangeLogFile);
            var latestChangeLog = changeLogSectionEntries
                .Aggregate((c, n) => c + Environment.NewLine + n);

            var newRelease = new NewRelease(releaseTag)
            {
                TargetCommitish = GitRepository.Branch,
                Draft = true,
                Name = $"v{releaseTag}",
                Prerelease = !string.IsNullOrEmpty(OctoVersionInfo.PreReleaseTag),
                Body = latestChangeLog
            };

            var createdRelease = await GitHubTasks
                .GitHubClient
                .Repository
                .Release.Create(owner, name, newRelease);

            ArtifactsDirectory.GlobFiles(ArtifactsType)
                .Where(x => !x.Name.EndsWith(ExcludedArtifactsType))
                .ForEach(async x => await UploadReleaseAssetToGithub(createdRelease, x));

            await GitHubTasks
                .GitHubClient
                .Repository
                .Release
                .Edit(owner, name, createdRelease.Id, new ReleaseUpdate { Draft = false });
        });

    private static async Task UploadReleaseAssetToGithub(Release release, string asset)
    {
        await using var artifactStream = File.OpenRead(asset);
        var fileName = Path.GetFileName(asset);
        var assetUpload = new ReleaseAssetUpload
        {
            FileName = fileName,
            ContentType = PackageContentType,
            RawData = artifactStream,
        };
        await GitHubTasks.GitHubClient.Repository.Release.UploadAsset(release, assetUpload);
    }
    
    // TODO: Validate this target
    Target PublishToGitHubPages => _ => _
        .Description("Publish demo to GitHub Pages")
        .Requires(() => Configuration.Equals(Configuration.Release))
        .OnlyWhenStatic(() => GitRepository.IsOnMainOrMasterBranch())
        .Executes(() =>
        {
            // Publish the site
            DotNetPublish(s => s
                .SetProject(Solution.demo.Demo_BlazorWasm)
                .SetConfiguration(Configuration.Release)
                .SetOutput(ArtifactsDirectory / "public")
                .SetProperty("GHPages", true)
            );
            
            // TODO: Deploy the site
        });
}
