# S-6: Stryker.NET on the MTP stack (week-1 prototype, SPEC §23)

Question: does Stryker.NET with `test-runner: mtp` score the three project shapes Ducky mutates, with `--since` and
full runs; does `DUCKY_REPEAT` set on the Stryker process reach the test hosts; does the boolean mutator mutate
`ConfigureAwait(false)`? Answers: `docs/spec/spikes.md` (S-6).

- `Plain`: plain-SDK net10.0 library (`Budget`): branches, and every await shape of `src/`: a `Task` awaited with
  `ConfigureAwait(ConfigureAwaitOptions.None)`, a `ValueTask` and an `await using` with `ConfigureAwait(false)` under
  `// Stryker disable once Boolean : …`. `.editorconfig` raises CA2007 as in `src/`.
- `Gen`: netstandard2.0 incremental generator, run by `Gen.Tests` through `CSharpGeneratorDriver`.
- `Web`: Razor-SDK library shaped like `src/Ducky.Blazor`: browser platform, `IsAotCompatible`, an STJ
  `JsonSerializerContext` and a `[LoggerMessage]` method, IVT to `Web.Tests` and `Web.Concurrency.Tests`. The
  concurrency test has the §17.3 shape (`DUCKY_REPEAT` repetitions, Barrier, 10 s `WaitAsync`) and appends one line
  per run to `$S6_RUN_LOG`.

Each library has its `stryker-config.json` in the shape `ExclusionGate` pins for `src/` (§17.1, §17.8). Full runs:

    cd spikes/S-6-mutation/Web
    S6_RUN_LOG=$PWD/../out/runlog.txt DUCKY_REPEAT=1 dotnet stryker --reporter Json --reporter Html --output ../out/web

(`Plain` and `Gen` the same way.) `--since` needs a plain clone: in a linked git worktree Stryker diffs the main
checkout. Commit, change a line, then `dotnet stryker --since:<sha>` (a SHA: `HEAD` resolved to `origin/HEAD`).
Outside every solution and gate (SPEC §18); `out/` and `TestResults/` are scratch.
