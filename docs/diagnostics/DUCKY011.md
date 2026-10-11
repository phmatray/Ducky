# DUCKY011: Reducer copies the state unchanged

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build info on the `with` expression (SPEC §16.2).

## What happened

A reducer lambda passed to `On<T>` returns `s with { }`, a copy of its state parameter with nothing changed. A slice
changed if and only if its reducer returned a different reference (ADR-0007), so this copy notifies every subscriber
of the slice on every such action, although the state is equal.

Only the lambda written directly as the `On<T>` reducer is analysed, nested lambdas inside it included; a method group
or a delegate stored in a variable is not.

## How to fix it

Return the state parameter itself when nothing changes, as in `On<Refresh>((s, a) => s)`, and use `with` only to change
something, as in `s with { Count = a.Count }`. With Ducky.Draft, `s.Produce(d => …)` returns the original reference when
the recipe changed nothing.
