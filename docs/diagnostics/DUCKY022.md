# DUCKY022: Registration call not emitted

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build warning on the attributed type's name (SPEC §16.1,
§16.2, GEN-02).

## What happened

The registration generator turns `[Persist]` into a `PersistAttributeDefaults<T>` call and `[Prerender]` into a
`Prerender<T>` call on `Ducky.Blazor.DuckyBlazorBuilderExtensions`. Nothing is emitted for the attribute, for one of these
reasons:

- the type is not a slice (it doesn't derive from `Slice<TState>`): only slices are persisted or prerendered;
- the gated method doesn't resolve: the project has no Ducky.Blazor reference, or it references a skewed or older
  Ducky.Blazor that lacks the method. Each call is gated on that exact method (name, arity and parameter types), never
  on the extension type, so generated code can't fail with CS0117. This is reported for any slice carrying the
  attribute, including one that `AddDuckyGenerated_{Asm}` doesn't register (DUCKY023, abstract or generic).

A registered slice is still registered by `AddDuckyGenerated_{Asm}`.

## How to fix it

Put the attribute on a slice only. Otherwise reference Ducky.Blazor in a version that matches your Ducky package (the
same 2.x version), or remove the attribute. You can also register the behaviour yourself: `builder.Persist<T>(...)` or
`builder.Prerender<T>()`.
