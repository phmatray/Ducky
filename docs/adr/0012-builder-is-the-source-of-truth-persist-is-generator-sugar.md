# ADR-0012: Builder is the source of truth; [Persist] is generator sugar

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §5.8, §9, §11.1, §16.1, §24; PER-01, GEN-02; matrix conflict 9; review rounds 1, 5, 6 and 7

## Context

Bustand users rely on a `[Persist]` attribute; a central builder keeps configuration in one place.

## Decision

`Persist<TSlice>(o => ...)` and `Prerender<TSlice>()` are the only runtime configuration. `[Persist]`/`[Prerender]` live in the runtime assembly and are turned into those calls by GEN-02 only when Ducky.Blazor is referenced (else DUCKY022). The runtime never reflects over `[DuckyAction]`, `[Persist]` or `[Prerender]`; `[ActionType]` is the one attribute read at run time, once per action type per store, with `type.GetCustomAttribute<ActionTypeAttribute>(inherit: false)` (trim-safe: no IL2xxx, and attributes on kept types survive trimming; the AOT smoke asserts it, `ActionType_AttributeName_SurvivesAot`, review round 7). `Persist<T>` composes: every configure delegate for a slice is applied, in call order, to one `PersistOptions`, so the attribute's `Version`/`Storage` and a builder `Migrate`/`MaxAge`/`Debounce` combine whatever the call order (`PersistAttributePlusBuilderMigrate_Composes`). The attribute is emitted as the hidden `PersistAttributeDefaults<TSlice>` call carrying only the named arguments actually written in `[Persist(...)]`, and those values are applied before every `Persist<TSlice>` delegate whatever the call order, so when both set one property the builder wins, as this ADR's title says (recovered review round 2: emitting defaults such as `Storage = Local` made an earlier builder value lose depending on call order). The generated calls are fully qualified static invocations. Review round 5: `PersistAttributeDefaults<TSlice>` registers the slice as persisted exactly like `Persist<TSlice>()` (it calls `AddBlazor()`, `RequireJsonTypeInfo(typeof(TState))` and the same validations) and records the attribute values as the base layer; GEN-02 emits it for every `[Persist]`, with an empty delegate for a bare `[Persist]` (`PersistAttribute_NoArguments_SlicePersisted`), so the attribute alone always persists the slice. Review round 6: the symbol gate of those calls is tested against the real Ducky.Blazor and Ducky.Reactive assemblies (split in review round 7 into `Registration_RealBlazor_EmitsPersistAndPrerender_NoDucky022`, stage 12, and `Registration_RealReactive_EmitsAddReactiveEffect_NoDucky022`, stage 16: each emit branch ships with the story that ships its method, since the real assemblies are skeletons before then and stubs are banned), a dedicated `Ducky.Blazor.Generated.Tests` project runs the attribute tests on generator-emitted code (review round 7 moved them out of `Ducky.Blazor.Tests`, whose hundreds of fixtures the generated registration would otherwise also register), and a PackageSmoke consumer declares `[Persist]` and `[Prerender]` slices, so a signature change can't silently turn every `[Persist]` into DUCKY022 and nothing emitted.

## Consequences

AOT-safe; one validation path; the attribute stays optional.

## Alternatives considered

Attribute-only (rejected: reflection or generator-only config); no attribute (rejected: migration cost for Bustand users); last call wins (rejected: the result depended on call order and silently dropped the attribute's version or the migrations); a configuration error when the attribute and a builder call set the same property (rejected in round 2: it would forbid a legitimate builder override of an attribute value).
