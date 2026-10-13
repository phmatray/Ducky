# DUCKY107: member type is `[Draftable]` in metadata but has no compatible `Draft`

**Package:** Ducky.Draft (generator `Ducky.Draft.Generators`). **Surface:** build info on the member (SPEC §13.3).

## What happened

A member's type `C` (or the element or value type of an `ImmutableList`, `ImmutableArray` or `ImmutableDictionary`
member) is marked `[Draftable]` in another assembly, but `C` has no nested public `Draft` that implements `IDraft<C>`
and has a public constructor taking exactly one `C`. The generated draft of the record would call `new C.Draft(x)`, so
the member is drafted as a plain get/set instead (a collection keeps a plain collection draft whose elements are
replaced, not drafted). This happens when the other assembly was built without the Ducky.Draft generator (analyzers
excluded, `PrivateAssets` hiding them, a skewed package version) or declares a hand-written or outdated `Draft` (a
struct or an abstract class, a constructor taking its `C` by `ref` or `out`, or a `Draft` or `Draft(C)` constructor that
is `[Obsolete(error: true)]` or carries an `[Obsolete]` `DiagnosticId` or an `[Experimental]` id, doesn't count either).
A member is reported once: an inherited one by the `[Draftable]` base of this compilation that declares it, and otherwise
(a base that gets no draft, or a base in another assembly) on the deriving record's name.

## How to fix it

Build the other assembly with the same Ducky.Draft package, analyzers included, so that its `[Draftable] partial record C`
gets its generated `Draft`. Until then, edit the member by assigning a new instance (`d.Child = d.Child with { ... }`).
