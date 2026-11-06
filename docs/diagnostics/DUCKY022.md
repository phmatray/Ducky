# DUCKY022: Registration call not emitted

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build warning on the attributed type's or the reactive
effect's name (SPEC §16.1, §16.2, GEN-02).

## What happened

The registration generator turns `[Persist]` into a `PersistAttributeDefaults<T>` call and `[Prerender]` into a
`Prerender<T>` call on `Ducky.Blazor.DuckyBlazorBuilderExtensions`, and registers each `ReactiveEffect` with an
`AddReactiveEffect<T>` call on `Ducky.Reactive.DuckyReactiveExtensions`. Nothing is emitted for the attribute or the
reactive effect, for one of these reasons:

- the type is not a slice (it doesn't derive from `Slice<TState>`): only slices are persisted or prerendered;
- the gated method doesn't resolve: the project has no Ducky.Blazor reference, or it references a skewed or older
  Ducky.Blazor that lacks the method. Each call is gated on that exact method (name, arity and parameter types), never
  on the extension type, so generated code can't fail with CS0117. This is reported for any slice carrying the
  attribute, including one that `AddDuckyGenerated_{Asm}` doesn't register (DUCKY023, abstract or generic);
- for a reactive effect, `AddReactiveEffect<TEffect>(DuckyBuilder)` doesn't resolve: the referenced Ducky.Reactive is
  skewed or older and lacks that exact method. This is reported for every reactive effect that `AddDuckyGenerated_{Asm}`
  would register, and for a `file` one (DUCKY023); an abstract, generic, inaccessible or constructor-less reactive
  effect is never registered, so it gets neither.

A registered slice is still registered by `AddDuckyGenerated_{Asm}`.

## How to fix it

Put the attribute on a slice only. Otherwise reference Ducky.Blazor or Ducky.Reactive in a version that matches your
Ducky package (the same 2.x version), or remove the attribute. You can also register the behaviour yourself:
`builder.Persist<T>(...)`, `builder.Prerender<T>()` or `builder.AddReactiveEffect<T>()`.
