<!-- One PR = one story (PLAN.md §1). The title is the squash commit: a Conventional Commit, checked by pr-title. -->

Closes #<!-- story issue -->

**Story:** <!-- e.g. M1-05 --> · **Stage:** <!-- the §24 step of the story -->

- [ ] This PR completes its stage and bumps `activeStage` in `docs/spec/tests.yaml` (PLAN P1: only the PR that merges the last open story of a stage)
- [ ] This PR does not complete its stage

## Red, green, refactor

- [ ] **Red:** the story's named tests (spec names verbatim) were committed failing first
- [ ] **Green:** the least production code that makes them pass; no code without a failing test asking for it
- [ ] **Refactor:** cleaned up with the tests green; `PublicAPI.Unshipped.txt` and XML docs in step
- [ ] **Gate:** `./build.sh Ci` and `./build.sh MutationPr --base-ref HEAD` green locally

## Notes

<!-- EventId ranges reserved, spec ambiguities resolved, follow-ups. -->
