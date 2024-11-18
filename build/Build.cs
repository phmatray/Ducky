using System;
using System.Linq;
using System.Text;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Discord;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.Discord.DiscordTasks;

partial class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    [Solution(SuppressBuildProjectCheck = true)]
    readonly Solution Solution;

    [GitVersion]
    readonly GitVersion GitVersion;
    
    const string MainBranch = "main";
    const string DevelopBranch = "develop";
    const string ReleaseBranchPrefix = "release";
    const string HotfixBranchPrefix = "hotfix";
    
    AbsolutePath SourceDirectory => RootDirectory / "src";
    
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    
    Target Version => _ => _
        .Executes(() =>
        {
            Log.Information("GitVersion = {Value}", GitVersion.MajorMinorPatch);
        });
    
    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            // Clean projects directories
            var directories = SourceDirectory
                .GlobDirectories("**/bin", "**/obj");
            
            foreach (var directory in directories)
            {
                Log.Information("Cleaning {Value}", directory);
                directory.DeleteDirectory();
            }
            
            // Clean Artifacts directory
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            Log.Information("Restoring NuGet packages");
            DotNetTasks.DotNetRestore(s => s.SetProjectFile(Solution));
            Log.Information("NuGet packages restored successfully");
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            Log.Information("Building solution {Solution} with configuration {Configuration}", Solution, Configuration);
            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetDeterministic(true));
            Log.Information("Solution built successfully");
        });
    
    Target UnitTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            Log.Information("Running unit tests");
            DotNetTasks.DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore());
            Log.Information("Unit tests passed successfully");
        });
    
    Target IntegrationTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
        });
    
    Target Tests => _ => _
        .DependsOn(UnitTests, IntegrationTests, Compile)
        .Executes(() =>
        {
        });
    
    Target UpdateChangeLog => _ => _
        .Before(Pack)
        .Executes(() =>
        {
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            Log.Information("Packing NuGet packages for projects starting with 'Ducky'");
            var projectsToPack = Solution.AllProjects
                .Where(p => p.Name.StartsWith("Ducky"));

            foreach (var project in projectsToPack)
            {
                Log.Information("Packing project {ProjectName}", project.Name);
                DotNetTasks.DotNetPack(s => s
                    .SetProject(project)
                    .SetConfiguration(Configuration)
                    .SetOutputDirectory(ArtifactsDirectory)
                    .EnableNoBuild()
                    .EnableNoRestore()
                    .SetIncludeSymbols(true)
                    .SetIncludeSource(true)
                    .SetSymbolPackageFormat(DotNetSymbolPackageFormat.snupkg));
                Log.Information("Project {ProjectName} packed successfully", project.Name);
            }
        });
    
    Target Publish => _ => _
        .DependsOn(Pack, UpdateChangeLog, Tests, Clean)
        .Executes(() =>
        {
        });
    
    [Parameter] [Secret] readonly string DiscordWebhook;
    
    // string AnnouncementTitle => $"NUKE {MajorMinorPatchVersion} RELEASED!";
    int AnnouncementColor => 0x00ACC1;

    Target AnnounceDiscord => _ => _
        .DependsOn(Publish)
        .Executes(() =>
        {
            // await SendDiscordMessageAsync(_ => _
            //         .SetContent("@everyone")
            //         .AddEmbed(_ => _
            //             .SetTitle(AnnouncementTitle)
            //             .SetColor(AnnouncementColor)
            //             .SetThumbnail(new DiscordEmbedThumbnail()
            //                 .SetUrl(AnnouncementThumbnailUrl))
            //             .SetDescription(new StringBuilder()
            //                 .Append($"This new release includes *[{AnnouncementGitInfo.CommitsText}]({AnnouncementComparisonUrl})*")
            //                 .AppendLine(AnnouncementGitInfo.NotableCommmitters.Count > 0
            //                     ? $" with notable contributions from {AnnouncementGitInfo.NotableCommmitters.JoinCommaAnd()}. A round of applause for them! 👏"
            //                     : ". No contributions this time. 😅")
            //                 .AppendLine()
            //                 .AppendLine(AnnouncementReleaseNotes).ToString()
            //                 .Replace("*", "**"))),
            //     DiscordWebhook);
        });
}
