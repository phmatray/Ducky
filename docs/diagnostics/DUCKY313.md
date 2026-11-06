# DUCKY313: retired

**Package:** Ducky.Blazor. **Surface:** none since Ducky 2.0 (SPEC §8.2, §11.6).

## What happened

Nothing: Ducky 2.0 never reports DUCKY313. A pre-release design rejected `PersistStorage.Server` in the browser
(Blazor WebAssembly) with this code. Ducky 2.0 makes Server storage inert there instead: the WebAssembly store neither
reads nor writes those slices, and the circuit side persists them. A registration shared between the server and the
`.Client` project of an Interactive Auto app therefore starts on both sides.

## How to fix it

There is nothing to fix. If you added a separate WebAssembly registration only to avoid this error, you can share the
server's registration again. The code stays retired and is never reused for another rule.
