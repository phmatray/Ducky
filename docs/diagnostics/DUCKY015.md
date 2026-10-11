# DUCKY015: Two slices share a state type

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build error on the name of each slice involved (SPEC
§16.2), reported once the whole compilation is analysed.

## What happened

Two or more non-abstract slices declared in this compilation hold the same state type. The store reads state by type
(`State.Get<TState>()`, ADR-0009), so each state type maps to exactly one slice; registering both fails at run time with
DUCKY305. A slice deriving from another non-abstract slice shares its state type too.

## How to fix it

Give each slice its own state type, for example `record CartState(...)` and `record WishlistState(...)` instead of one
shared record, or make the common base slice `abstract`.
