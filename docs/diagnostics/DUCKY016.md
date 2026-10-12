# DUCKY016: Generated registration never called

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build warning on each `AddDucky` call (SPEC §16.1, §16.2,
ADR-0017, R-PKG-5).

## What happened

This project declares slices or effects that the generated method registers (non-abstract, non-generic, non-`file`
classes accessible within the assembly, deriving from `Slice<TState>`, `Effect<T>` or `EffectGroup`, an effect with a
public constructor; a type DUCKY023 excludes doesn't count) and calls `AddDucky`, but never calls
`AddDuckyGenerated_{Asm}`, the method the Ducky generator emits for this assembly (`{Asm}` is the assembly name with
every non-identifier character replaced by `_`). None of its slices or effects is registered: the store doesn't know
them, so their actions reduce nothing and their effects never run.

Each assembly gets its own generated method, and generator assets never flow through a `ProjectReference`: every
project that declares actions, slices or `[Draftable]` records needs its own direct `Ducky` (or `Ducky.Draft`)
`PackageReference`, or it gets no generated code at all.

## How to fix it

Call the generated method in the `AddDucky` callback: `services.AddDucky(b => b.AddDuckyGenerated_MyApp())`. For a
class library that declares slices, call the library's method from the app as well, as in
`b.AddDuckyGenerated_MyApp().AddDuckyGenerated_MyLib()`. Middleware is never generated: register it with `Use<T>()`.
