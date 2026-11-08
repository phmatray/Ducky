# Product

<!-- impeccable:product-schema 1 -->

> Inferred record. The owner delegated every choice ("Choisis les meilleurs choix toi-même", 2026-11-08); each fact below comes from the repository (docs/spec/SPEC.md, docs/guides, test/Ducky.Docs.Tests) or the owner's brief, and is marked *(inferred)* where it is a judgement rather than a repository fact.

## Platform

web

## Stack

delegated: plain static HTML/CSS with a few small vanilla JS modules, no build step and no runtime dependency, in `site/`. Chosen because the GitHub Pages workflow (`.github/workflows/docs.yml`) already assembles a static artifact, the docs beside it are rendered by the build into the same world (`site/docs-theme`, owner: "really ugly docfx site, I'd prefer a custom solution", 2026-11-08), and a dependency-free page cannot rot between releases.

## Users

.NET developers building Blazor apps (WebAssembly, Interactive Server, Interactive Auto, static SSR with prerender) who need application state that survives render-mode switches, prerender, reloads and multiple tabs, and tech leads choosing the state library a team will live with for years. They evaluate on NuGet, GitHub and the docs: they read code first, then look for proof (AOT and trimming safety, tests, render-mode support, maintenance).

## Product Purpose

Ducky 2.0 is a small, strict Redux store for .NET and Blazor: typed slices of immutable state, pure reducers, one dispatcher that reduces one action at a time, effects with concurrency policies, and Blazor integration (persistence, prerender handoff, server-side cache, cross-tab sync, Redux DevTools). Success: a Blazor developer reaches a working store in minutes and trusts it in production. Owner ambition: become the default state library for Blazor *(owner brief)*.

## Positioning

Every concurrency and lifecycle claim is an invariant (INV-nn) with a named test against the real store, and the same load rule works in every Blazor render mode. Source generators replace reflection and boilerplate (`AddDuckyGenerated_{Assembly}()`, `[DuckyAction]` dispatch helpers, analyzers with DUCKY ids). AOT and trimming safety is enforced by CI (NativeAOT smoke, trimmed AOT-WASM smoke). Optional packages: SliceStore (Zustand-style), Ducky.Draft (Immer-style `Produce`), Ducky.Reactive (R3), Ducky.Testing.

## Operating Context

Read in a browser next to an IDE; code is copied into `Program.cs`, slices and components. Packages: `Ducky`, `Ducky.Blazor`, `Ducky.Draft`, `Ducky.Reactive`, `Ducky.Testing` on NuGet, net10.0 only. Docs: generated guides under `docs/` (the build's own Markdown renderer, `docs/nav.yml`), every snippet a region of a compiled, tested file. A WASM sample is published beside the site under `sample/`.

## Capabilities and Constraints

- Slices (`Slice<T>`, `On<TAction>`), `SliceStore<T>`, effects (`Effect<T>`, `EffectGroup`, `Concurrency.Switch/Exhaust/...`), selectors (`Select`, memoized), `EntityState`.
- `DispatchAsync` returns `Task<DispatchResult>`, never faults; atomic commit across slices.
- Blazor: `DuckyComponent`, `Persist<T>()` (localStorage/sessionStorage/Server `IDistributedCache`), versioned migrations, `Prerender<T>()` handoff, `WasRestored<T>()` load rule, `SyncAcrossTabs`, Redux DevTools with time travel.
- Ducky.Testing: `SliceTest`, `EffectTest`, `TestStore`.
- Release status: 2.0 is pre-release (rc.1 pending owner approval). The site must not claim a GA release, download counts, adopters or benchmarks that do not exist.

## Brand Commitments

Name "Ducky". The 1.x repository is phmatray/Ducky; the duck is the name's only motif. Voice in the docs: plain, exact, short sentences, no hype *(repository evidence)*.

## Evidence on Hand

- Compiled, tested snippets: `test/Ducky.Docs.Tests/**` (quick start, getting started, SliceStore, effects, persistence, render modes, Draft, testing).
- Quality bar (SPEC §1.1 goal 7, §17): 100% line and branch coverage of library C#, Stryker mutation target ≥ 90% (break < 85%), property tests, named concurrency tests, Playwright for the JS, AOT gate, PublicAPI tracking, warnings as errors.
- ~64 documented diagnostics (`docs/diagnostics`); the Ducky 1.x migration guide. The guides for the owner's earlier trials (Mutty, DuckyNext, blazor-state, Bustand, TheBlazorState, BlazorMVU) stay unlisted: "apart from Ducky 1.x nobody uses my other libraries" (owner, 2026-11-08). Market leaders to compare with and migrate from: Fluxor first, then TimeWarp.State and the plain state-container pattern; their guides and the sourced comparison page (`docs/compare.md`) sit on the Migrate tab and the home page beside Ducky 1.x.
- Absent, never to fabricate: testimonials, adopters, download counts, benchmark numbers, star counts. The one exception is the comparison page (`docs/compare.md`), which may show dated nuget.org download and GitHub star figures, each linked to its source, for every library it compares, Ducky 1.x included; Ducky 2.0 has no adopters or benchmark numbers to claim there either.

## Product Principles

1. Prove, don't claim: every statement on the site maps to an invariant, a test or a compiled snippet.
2. One rule in every render mode: the site shows Blazor's real lifecycle, not a toy counter.
3. Code is the hero: the reader judges by the API they would write.
4. Honest about scope and status.

## Accessibility & Inclusion

WCAG 2.2 AA; full keyboard use; `prefers-reduced-motion` honoured; readable code at mobile widths *(inferred)*.
