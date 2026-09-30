# ADR-0019: Ducky.Draft absorbs Mutty with a nested Draft type

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §13, §16.1; IMM-02; matrix conflict 16; open question 12; review rounds 6 and 7

## Context

Mutty (5.2k downloads) breaks reference-equality change detection, fails to compile many shapes, and its `{R}Extensions`/injected attribute collide across projects. Correctness-first emitted top-level `XDraft`/`XDraftExtensions` types.

## Decision

Ducky.Draft (independent of Ducky) generates a nested `R.Draft` and instance `R.Produce(recipe)` for `[Draftable] partial record R`, with lazy copy-on-write, identity preservation, runtime collection drafts, symbol-based type mapping, and revocation. Mutty is deprecated and archived; the deprecation message says Ducky.Draft needs net10.0 and the last Mutty keeps working, unmaintained, on older TFMs. Revocation and element-draft creation go through public runtime contracts (`IRevocable`, `Func<T,TDraft>` constructor arguments), so drafts compose across assemblies. Review round 7: user source compiled with nullable disabled is supported (an oblivious reference type counts as annotated wherever the draft declares or null-guards it, an oblivious collection element keeps element drafting, and element drafts never wrap a null element), and generated hint names are the sanitized fully qualified metadata name plus `.Draft.g.cs`, so same-named records in different namespaces, nested records and keyword-named records never crash the generator. Each generated `Draft` keeps an implicit conversion from its record, so `d.Child = value` compiles as in Mutty; a replaced child draft and element drafts flushed by a structural list operation are revoked, so a stale write throws; the no-op check compares each final member with the original at `Build` (reverts return the base); a member type maps to `C.Draft` only when that nested type exists (DUCKY107 otherwise) (recovered review round 2). Review round 6: nullable-annotated elements and dictionary values are never element-drafted (a `class`-constrained element draft over `Todo?` doesn't compile under nullable); the draft mirrors only public, internal and protected internal instance properties, with the minimum accessibility of member and type and `new` over hidden `object` members; and immutable types without a draft (`ImmutableQueue`, `ImmutableStack`, `ImmutableSortedDictionary`, `EntityState`) stay plain get/set, with DUCKY109 flagging a recipe that discards the new instance their methods return, which Mutty's mutable mappings let migrated code do in place.

## Consequences

No new top-level types, no collisions, derived records supported, collection drafts coverage-tested. Mutty users add `partial` (DUCKY101 tells them).

## Alternatives considered

Fix Mutty standalone (rejected: owner decision); extension-based top-level drafts (rejected: collision surface, DUCKY103 on nested records).
