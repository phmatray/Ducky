# Design record

`SPEC.md` is authoritative, with the ADRs in `../adr/`. `PLAN.md` and `plan.json` split it into stories; where they
disagree with the spec, the spec wins. `spikes.md` records the week-1 spike answers. `tests.yaml` is the staged test
manifest that `SpecTraceGate` reads (SPEC §4, §17.1, §19, §24).

## The test manifest (`tests.yaml`)

```yaml
activeStage: 1
projects:   { Ducky: 2, …, Ducky.Draft.Generators: 17 }   # §17.8, read by Mutation and MutationPr
invariants: { INV-01: 2, …, INV-32: 8 }
tests:
  - { name: Reducers_NeverRunConcurrently, projects: [Ducky.Concurrency.Tests], invariants: [INV-01], stage: 2 }
```

- **Entries.** One per normative test name: every backticked name of the §4 "Named tests" column, the names of §14 and
  §17, and the backticked names in the acceptance tests of `PLAN.md`. `projects` lists every test project the name runs
  in (`Ducky.AotSmoke` for the names the AotSmoke binary prints). `invariants` lists the §4 rows that name the test; a
  name §4 does not list takes the INV column of its §17.3 row (`Api_{Member}_NeverFaults`: INV-10, §8.1); a
  `…_Deterministic` twin carries its race test's invariants.
- **An entry's stage** is the stage (the §24 step) of the story that implements it. A name listed in several projects
  takes the stage of the last story that adds it (PLAN P1), so its existence is checked once every project has it.
- **An invariant's stage** is the lowest stage of a story that adds a covered-project test for it (covered: measured by
  `CoverageGate`, so neither `Ducky.Concurrency.Tests`, `Ducky.E2E` nor `Ducky.AotSmoke`). INV-24 has no covered test
  (its gate is the `aot` job); it carries the stage of the `AotSmoke` entry.
- **`projects:`** is pinned by §17.8; `Mutation` and `MutationPr` fail when it differs.
- **`activeStage`** moves to N in the change that completes the last open story of the stages <= N. It may jump several
  stages when later ones are already complete, it never passes a stage with an open story, and it never moves down
  (`MutationPr` fails on a lowered one). A story of a later stage may land early: its tests run under `Test`,
  `CoverageGate` and the name checks at once, while the existence checks, `MutationPr` and the zero-mutant check of an
  inactive project wait for the bump.
- The story that first adds real tests to a test project deletes its `Skeleton_{Project}_Smoke` test and drops the
  project from that entry (PLAN P2).

## What `SpecTraceGate` checks

One `dotnet test --solution Ducky.slnx --list-tests` run lists every test project, `Ducky.E2E` included.

| Check | When |
|---|---|
| the §4 table parses to exactly INV-01..INV-32, each row naming a test, and `invariants:` has exactly those keys | always |
| `activeStage`, every `stage` and every `invariants:` value is >= 1; every entry lists one or more distinct projects | always |
| no name is blank, and no `{Placeholder}` name matches another manifest or §4 name (a bare `{X}` would stand in for all) | always |
| each `invariants:` value equals the lowest `stage` of the covered entries mapped to it (INV-24: of its `Ducky.AotSmoke` entries), when it has any | always |
| every manifest name appears in `SPEC.md` | always |
| every §4 "Named tests" name is in the manifest | always |
| the manifest entry of each §4 "Named tests" name lists that row's ID in its `invariants` | always |
| an entry exists in each of its projects (`Ducky.AotSmoke` excepted: the AotSmoke target checks it) | `stage <= activeStage` |
| every invariant but INV-24 has an active entry in a covered project | invariant's `stage <= activeStage` |
| no entry is inactive; all 32 invariants and every entry are checked | `--strict-stages` (`-rc.N` and GA tags) |

One matching rule: a `{Placeholder}` matches `\w+` at any position (`Caching_{Step}_Cached` matches
`Caching_Pipeline_Cached`), and a listed test matches on its method name, the last `.`-separated segment before any
`(` (a theory listed as `…Name(repeat: 1)` matches `Name`). Each run also replays the gate's planted checks on a
miniature manifest, so a regression in the gate itself fails it.
