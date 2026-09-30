# ADR-0036: Release with MinVer, git-cliff and trusted publishing

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §19, §21; ENG-05; review rounds 4, 5 and 6

## Context

1.x had three versioning systems, an async-void release step and a stale changelog.

## Decision

MinVer (MSBuild, `v` prefix, `MinVerMinimumMajorMinor=2.0`), git-cliff notes from Conventional Commits, NuGet trusted publishing in a hand-written `release.yml` with three jobs: `release-gates` (Ci, E2E and AotSmoke only), `release-mutation` (`MutationForSha`, in parallel with the gates) and `publish` (`needs` both, runs `NuGet/login@v1` as its first credentialed step and pushes immediately, well inside the key's one-hour validity), environment approval, all packages at one version, except the Ducky.Generator tombstone: fixed at 2.0.0 (`MinVerSkip`), packed into its own `artifacts/tombstone` and pushed only by the exact `v2.0.0` tag (review round 4). `Pack` uses `PackageOutputPath` rather than `-o` and fails unless the exact package set is produced. `release-mutation` reuses the `nightly-mutation` result for the tagged SHA when one exists. `Publish` has no `DependsOn` (review round 5): Fallout's `--skip` leaves a skipped target's dependencies scheduled, so `--skip Pack PackageSmoke` still ran a locked restore and a full compile with the key live; `Publish` instead applies `Pack`'s exact-set check to the downloaded packages, and the local `Release` chain gets `Pack` and `PackageSmoke` through `ReleaseGates`. `ReleaseGates` runs `SpecTraceGate --strict` for `-rc` and GA tags. Every `release.yml` job checks out with full history, and the publish job installs a pinned git-cliff binary. The tag reachability check and the Stryker `--since` base follow parameters, `ReleaseBranch` (`v2`, flipped to `main` after the GA rename) and `BaseRef` (`origin/$GITHUB_BASE_REF`), never a hard-coded branch (recovered review round 2). Both pushes name their source and key in full (review round 6): `dotnet nuget push artifacts/packages/*.nupkg --source https://api.nuget.org/v3/index.json --api-key $NUGET_API_KEY --skip-duplicate`, and, for the exact `v2.0.0` tag only, the same command over `artifacts/tombstone/*.nupkg`; no committed `nuget.config` defines a `defaultPushSource`. `Changelog` and `GitHubRelease` take a `ReleaseVersion` parameter (the tag without its `v` in `release.yml`; the GA release PR passes `--release-version 2.0.0`, because MinVer yields a prerelease height version in a PR), falling back to `dotnet msbuild -getProperty:MinVerVersion` rather than Fallout's `[MinVer]` (which needs `minver-cli`). Token scopes are explicit: the nightly workflows get `issues: write` and `contents`/`actions: read`, `release-mutation` gets `contents: read, actions: read, issues: write` with `GH_TOKEN`, `publish` passes `GH_TOKEN` to `gh release create`, and `MutationForSha` fails, never falls back, when its run query errors. The package validation baseline is committed only after the GA push, by the maintainer running `./build.sh SetBaseline` in an ordinary PR (review round 3: a PR opened with `GITHUB_TOKEN` triggers no workflows, so its required checks would never report).

## Consequences

No long-lived NuGet key; reproducible versions from tags.

## Alternatives considered

Nerdbank/GitVersion (rejected: extra system); API key secret (rejected: long-lived secret).
