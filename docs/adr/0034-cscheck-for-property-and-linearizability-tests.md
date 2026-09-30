# ADR-0034: CsCheck for property and linearizability tests

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §17.2; ENG-02; D13

## Context

The matrix named FsCheck. Concurrency claims need linearizability checking, not just random sampling.

## Decision

Use CsCheck (`Sample`, `SampleParallel` against sequential models) for dispatch linearizability, init-buffer ordering, subscription wake-ups, effect policies, persistence writers, selectors, entities and Draft. Nightly long runs raise iterations.

## Consequences

C#-native generators and shrinking; linearizability against the real dispatcher.

## Alternatives considered

FsCheck (rejected: no parallel model checking, F#-centric API).
