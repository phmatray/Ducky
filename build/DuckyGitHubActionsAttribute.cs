using Fallout.Common;
using Fallout.Common.CI.GitHubActions;
using Fallout.Common.CI.GitHubActions.Configuration;
using Fallout.Common.Execution;
using Fallout.Common.IO;

// SPEC §20.1, spike S-8: Fallout 10.4.0 names every job after its image (`ubuntu-latest`), so the required checks of
// §20.2 would never report. Each workflow has one image and one job named after the workflow.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
internal sealed class DuckyGitHubActionsAttribute : GitHubActionsAttribute
{
    // VerifyWorkflows regenerates into this directory instead of .github/workflows.
    internal const string OutputDirectoryVariable = "DUCKY_WORKFLOWS_DIR";

    // AutoGenerate off: a local build must not rewrite .github/workflows behind VerifyWorkflows' back (it would undo the
    // hand edit the gate exists to catch, and prompt for a key on an interactive console).
    public DuckyGitHubActionsAttribute(string name, GitHubActionsImage image)
        : base(name, image) => AutoGenerate = false;

    public override AbsolutePath ConfigurationFile =>
        EnvironmentInfo.GetVariable(OutputDirectoryVariable) is { Length: > 0 } directory
            ? (AbsolutePath)directory / $"{IdPostfix}.yml"
            : base.ConfigurationFile;

    protected override GitHubActionsJob GetJobs(GitHubActionsImage image, IReadOnlyCollection<ExecutableTarget> relevantTargets)
    {
        var job = base.GetJobs(image, relevantTargets);
        job.Name = IdPostfix;
        return job;
    }
}
