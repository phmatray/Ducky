# DUCKY103: Generic `[Draftable]` record

**Package:** Ducky.Draft (generator `Ducky.Draft.Generators`). **Surface:** build error on the record's name (SPEC §13.3).

## What happened

The record is generic, or nested in a generic type. The nested `Draft` would have to reproduce the type parameters and
their constraints, which Ducky.Draft 2.0 does not support, so nothing is emitted for the record.

## How to fix it

Make the record non-generic (a closed record per element type), or edit it with `with`.
