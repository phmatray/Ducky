# Spike results (SPEC §23)

Week-1 prototype answers. Each prototype lives under `spikes/`, outside every solution and gate.

## S-2: coverage without exclusions (M0-04)

**Question.** Can the coverage tool reach 100% line and branch without exclusions, with the pinned xunit.v3/MTP
versions?

**Setup.** `spikes/S-2-coverage`, run 2026-10-01 on macOS arm64 with SDK 10.0.401 (global.json 10.0.100,
`latestFeature`), `xunit.v3.mtp-v2` 4.0.1, `Microsoft.Testing.Extensions.CodeCoverage` 18.11.2,
`Microsoft.CodeAnalysis.CSharp` 5.0.0 (the pins of `Directory.Packages.props`). Two runs: the tool's defaults with no
settings file, then `--coverage-settings ../../build/coverage.settings.xml` (commands in the spike's README).

**Answer: yes.** 8 tests pass, no crash. Without any settings file every probe shape is at 100% line and branch:
the only uncovered code is `Probe/Uncovered.g.cs`, a hand-named `*.g.cs` that no test calls on purpose (line 8, 0/2
branches). With the committed settings file that class is gone and both probe assemblies show `line-rate="1"
branch-rate="1"`; every other line has the same hits and branch counts in both runs.

**Choice: `Microsoft.Testing.Extensions.CodeCoverage`** (18.11.2, already pinned). The coverlet.MTP fallback is not
triggered and was not evaluated.

**Tool defaults the gate relies on** (measured in both runs, so passing `build/coverage.settings.xml` keeps them):

- Attribute exclusions merged by default: `DebuggerHiddenAttribute`, `DebuggerNonUserCodeAttribute`,
  `GeneratedCodeAttribute`, `ExcludeFromCodeCoverageAttribute` (the last is a banned API in `src/`, §17.1).
- Compiler-generated code is folded into the user method, not reported separately: async state machines report no
  branch for an await's `IsCompleted` check or for the `await using` null check and dispose. `SyncOnlyAsyncShapes.AddAsync`
  and `UseAsync` repeat those shapes and are called with completed tasks only (`Async_CompletedOnly_FullyCovered`):
  every line is covered and no branch is partial, so await suspension points never demand a pending-task test for
  coverage alone.
- Records count their declaration line (primary constructor and property initialisation); the synthesized `Equals`,
  `GetHashCode`, `PrintMembers`, `ToString`, `Deconstruct` and clone members add no line or branch.
- A lowered `lock` (on `System.Threading.Lock` and on an object with Monitor's `lockTaken` flag) adds no branch.
- Switch expressions report their arms as branches on the discriminating line; the default arm counts. An enum
  switch without a discard arm fails the build (CS8524 under warnings as errors), so its throwing default arm needs a
  test.
- `SkipAutoProperties` is `True` and the MTP extension leaves test assemblies out.
- A module without any sequence point (only a member-less interface such as the tombstone shape, or
  `public sealed class X : Attribute;`) is absent from the report rather than listed at 100%.

**Consequences for `CoverageGate` and `ExclusionGate`.**

- `build/coverage.settings.xml` is the one coverage settings file; its only content is the source exclusion
  `.*\.g\.cs$` (the tool takes ECMAScript regexes, so this is the `**/*.g.cs` of §17.1). `Uncovered.g.cs` proves it
  matches: present and uncovered without the file, absent with it. The `Test` target passes it
  with `--coverage-settings`, and `ExclusionGate` fails on any other element or attribute, or on a second settings
  file.
- A `src/` assembly of `Ducky.slnx` that is absent from the merged report counts as missing unless its Release PDB
  has no non-hidden sequence point outside `*.g.cs`, like the stage-1 skeletons of `Ducky.Draft`
  (`DraftableAttribute`) and `Ducky.Testing` (`DuckyAssertionException`), whose final §13.2/§15 declarations are
  code-less too. With nothing to cover they pass, and the gate logs them. A project without a PDB counts as having
  code. SPEC §17.1, §19 and §24 carry this rule.
- The path exclusion and the default `[GeneratedCode]` exclusion may only ever remove generated code, so
  `ExclusionGate` fails on a committed `*.g.cs` anywhere in the repository (a file outside `src/` can be linked into a
  `src/` project), and on a `[GeneratedCode]` type or method (read from the Release metadata, containing types
  included) with a sequence point outside `*.g.cs`. The tool applies the `<Source>` regex case-insensitively
  (`Uncovered.G.cs` is dropped from the report too), so the gate matches `*.g.cs` case-insensitively as well.
- `#line hidden` hides sequence points (the tool never reports them) and `#line 1 "Fake.g.cs"` remaps them onto the
  path exclusion, so `ExclusionGate` rejects any `#line` directive in `src/`.
- MTP writes each cobertura file twice, in `artifacts/test/` and in `artifacts/test/<machine>_<date>/In/<machine>/`.
  The `**/*.cobertura.xml` glob merges both copies: hit counts double, rates are unchanged.
- ReportGenerator writes `branch="true"` in lowercase; the gate reads `condition-coverage` instead.

Not measured here: Linux and Windows. CoverageGate runs there once the CI workflows arrive (M0-11).
