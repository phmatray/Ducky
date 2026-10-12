# DUCKY021: Dispatch helper name collides with a store member

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build warning on the action's name (SPEC §16.1, GEN-01).

## What happened

The generated helpers `X` and `XAsync` are extension methods on `IDispatcher`. C# binds an accessible instance member
before any extension method, so when `X` or `XAsync` names a public member of `IDispatcher`, `IStore` (including
`IDisposable` and `IAsyncDisposable`), `EffectContext`, `TestStore` or `object`, the call would silently reach that
member instead: with a `record Dispose`, `store.DisposeAsync()` would dispose the store rather than dispatch the action,
and with a `record Settled`, `testStore.Settled()` would wait instead of dispatching. The generator skips both helpers of
this action. Static members don't count, since C# ignores them for an instance receiver.

`TestStore` is checked only when the project that declares the action references Ducky.Testing. Helpers generated in an
app project that doesn't reference it are not checked against `TestStore`'s members, so a `record Settled` declared
there still gets its helpers, and `testStore.Settled()` in the test project waits; call `testStore.Dispatch(new
Settled())` or set `MethodName`.

## How to fix it

Set a non-colliding name on the attribute, for example `[DuckyAction(MethodName = "DisposeItem")]`, or rename the
record. You can also dispatch the action directly: `store.Dispatch(new Dispose())`.
