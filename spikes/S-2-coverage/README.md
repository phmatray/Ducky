# S-2: coverage without exclusions (week-1 prototype, SPEC §23)

Question: does the coverage tool reach 100% line and branch **with no exclusion** and no crash on the pinned
xunit.v3/MTP versions? Answer and choice: `docs/spec/spikes.md` (S-2).

- `Probe`: async methods, `await using`, records (sealed, non-sealed, derived), a lowered `lock` on a `Lock` and on
  an object, switch expressions with guard, type and default arms, a member-less error-level `[Obsolete]` interface.
  `SyncOnlyAsyncShapes` repeats the async shapes and is called with completed tasks only
  (`Async_CompletedOnly_FullyCovered`): it proves that an await's suspension path needs no test for coverage.
  `Uncovered.g.cs` is a hand-named generated-source file that no test calls: it proves the `*.g.cs` exclusion of
  `build/coverage.settings.xml`.
- `Probe.Generator`: an incremental generator, run by the tests through `CSharpGeneratorDriver`.
- `Probe.Empty`: only a code-less attribute (the shape of the `src/Ducky.Draft` and `src/Ducky.Testing` skeletons).

Check, with the tool defaults and then with the committed settings file:

    cd spikes/S-2-coverage
    dotnet test --project Probe.Tests -c Release --coverage --coverage-output-format cobertura \
      --coverage-output probe.cobertura.xml --results-directory out
    dotnet test --project Probe.Tests -c Release --no-build --coverage --coverage-output-format cobertura \
      --coverage-settings ../../build/coverage.settings.xml --coverage-output probe.settings.cobertura.xml \
      --results-directory out

Without the settings file, `out/probe.cobertura.xml` lists `Probe.Generator` at 100% and `Probe` below it only
through `Probe.Uncovered` (`Uncovered.g.cs:8`, 0 hits, 0/2 branches). With it, `out/probe.settings.cobertura.xml` lists
`Probe` and `Probe.Generator` at `line-rate="1" branch-rate="1"`, with every other line identical. Neither lists
`Probe.Empty`. Outside every solution and gate (SPEC §18).
