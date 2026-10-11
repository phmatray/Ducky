# DUCKY014: Duplicate handler for an action type

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build error on each repeated `On<T>` call (SPEC §16.2).

## What happened

One slice or `EffectGroup` constructor calls `On<T>` twice or more for the same `T` (the two `On<T>` overloads of a
slice count as the same registration, and so do tuple types that differ only in element names, since the names are
erased at run time). Each action type has exactly one handler per slice or effect group (ADR-0008),
so the second call throws when the constructor runs: DUCKY308 at run time.

## How to fix it

Keep one `On<T>` per action type and merge the two handlers into it, calling shared private methods when the logic is
long.
