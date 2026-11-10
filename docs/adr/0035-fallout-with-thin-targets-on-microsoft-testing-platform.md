# ADR-0035: Fallout with thin targets on Microsoft.Testing.Platform

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §19; ENG-05; owner decision; open question 18; amended 2026-11-10 (CI-MUTATION-LOCAL, ADR-0048)

## Context

Fallout's `ITest` and `IReportCoverage` are VSTest/coverlet-shaped (sealed `TestSettingsBase` adds `--logger trx` and coverlet when `IsServerBuild`), and Fallout has no MTP support.

## Decision

The build derives from `FalloutBuild`, `IHasSolution`, `IConfigureGitHubActions` only. Every target is a thin wrapper over the dotnet CLI (MTP syntax: `dotnet test --solution … --coverage --coverage-output-format cobertura --report-trx`) and Fallout's tool wrappers (Stryker with `test-runner mtp`, ReportGenerator). Workflows are generated from `DuckyGitHubActionsAttribute`, a subclass that overrides `GetJobs` so each job is named after its workflow (Fallout names jobs after the image, which makes required checks unmatchable); `VerifyWorkflows` diffs them and asserts unique job names. `release.yml` is hand-written (three jobs, `release-gates`, `release-mutation` and `publish`, §20.1), and so are `mutation.yml` and `nightly-mutation.yml` since M0-15 (shard matrices with one aggregate job each, which Fallout's one-job-per-workflow attribute cannot emit, §17.8); `VerifyWorkflows` checks their jobs.

## Consequences

Works with xUnit v3 on MTP; replacing Fallout with plain workflow steps stays mechanical.

## Alternatives considered

Compose `ITest`/`IReportCoverage` (rejected: breaks on MTP); plain GitHub Actions (rejected by the owner).

## Amendment (2026-11-10): no mutation workflows (ADR-0048)

ADR-0048 supersedes the mutation-in-CI part of this decision. `mutation.yml` and `nightly-mutation.yml` are deleted and
`release.yml` has two jobs, `release-gates` and `publish`; `VerifyWorkflows` no longer checks shard matrices, and instead
fails on any committed workflow step that runs a `Mutation*` target or Stryker. Stryker stays a thin wrapper of the local
`Mutation` and `MutationPr` targets.
