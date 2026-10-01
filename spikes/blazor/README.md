# S-3, S-5, S-7: Blazor platform questions (week-1 prototypes, SPEC §23)

Answers: `docs/spec/spikes.md` (S-3, S-5, S-7). Outside every solution and gate (SPEC §18); `out/` is scratch.

- `Client`: the WASM side of one Blazor Web App and the pages both sides render (global interactivity on `Routes`).
  `SpikeStore` stands in for a store with its prerender handoff and persistence middleware: a singleton owning a store
  scope in the browser, scoped on the server; the first component touches it with its renderer-scope services. It
  records service identity and lifetimes, the `ducky.js`-style import probe, the seed it takes (`ducky:seed`, a UTF-8
  envelope through `PersistAsJson<byte[]>`/`TryTakeFromJson<byte[]>` under the audited suppression), the seed it
  persists through `RegisterOnPersisting(…, RenderMode.InteractiveAuto)` (`src` `prerender` or `pause`), and every
  `AuthenticationStateChanged` with whether `DuckyScopes.NameIdentifier`'s shape completes synchronously.
  `/s7` prints all of it; `/s5` runs the `IJSStreamReference` read path; `?nullmode=1` adds a null-mode registration.
- `Host`: the server side. `SPIKE_MODES=server|wasm|auto` configures one render mode or both (the Auto template);
  `/login?user=x` signs in with a cookie, `/spike-log` lists persists, auth events and circuit lifecycle.
- `Driver`: starts `Host` once per mode and drives it with Playwright (Chromium), including pause/resume
  (`Blazor.pauseCircuit()`/`resumeCircuit()`) and a reconnect (`Blazor._internal.forceCloseConnection()`).

Run (Debug build, Development environment, so server-side scope validation is on):

    dotnet build spikes/blazor/Host/Host.csproj && dotnet build spikes/blazor/Driver/Driver.csproj
    cd spikes/blazor && dotnet Driver/bin/Debug/net10.0/Driver.dll Host/bin/Debug/net10.0/Host.dll server wasm auto

S-3, trimmed AOT publish (needs the wasm-tools workload of the running SDK band; the WASM SDK suppresses trim warnings
unless `SuppressTrimAnalysisWarnings=false`, which `Client.csproj` sets with `TrimMode=full`):

    dotnet publish Host/Host.csproj -c Release -o out/aot -p:RunAOTCompilation=true -p:ILLinkTreatWarningsAsErrors=false
    SPIKE_ENVIRONMENT=Production dotnet Driver/bin/Debug/net10.0/Driver.dll out/aot/Host.dll wasm

Add `-p:SeedPath=Json -p:TreatWarningsAsErrors=false` to the publish to drop the suppression and see the warnings.
