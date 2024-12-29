using Nuke.Common.CI.GitHubActions;

[GitHubActions(
    "windows-latest",
    GitHubActionsImage.WindowsLatest,
    FetchDepth = 0,
    OnPushBranchesIgnore = [MainBranch, $"{ReleaseBranchPrefix}/*"],
    OnPullRequestBranches = [DevelopBranch],
    InvokedTargets = [nameof(Tests), nameof(Pack)],
    PublishArtifacts = false,
    AutoGenerate = false)]
[GitHubActions(
    "macos-latest",
    GitHubActionsImage.MacOsLatest,
    FetchDepth = 0,
    OnPushBranchesIgnore = [MainBranch, $"{ReleaseBranchPrefix}/*"],
    OnPullRequestBranches = [DevelopBranch],
    InvokedTargets = [nameof(Tests), nameof(Pack)],
    PublishArtifacts = false,
    AutoGenerate = false)]
[GitHubActions(
    "ubuntu-latest",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    OnPushBranchesIgnore = [MainBranch, $"{ReleaseBranchPrefix}/*"],
    OnPullRequestBranches = [DevelopBranch],
    InvokedTargets = [nameof(Tests), nameof(Pack)],
    PublishArtifacts = false,
    AutoGenerate = false)]
partial class Build;
