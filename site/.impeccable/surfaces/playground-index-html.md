---
version: 1
slug: "playground-index-html"
primary_target: "playground/index.html"
related_targets: []
---

# Surface brief: Ducky Playground (playground/index.html)

Scope: a standalone Blazor WebAssembly app (samples/Ducky.Playground) published beside the site at ./playground/, linked from the home page as the live demo. Visitor mode: **Operate** (the visitor uses a working app), with the X-ray layer as the explanation.

Audience and job: Blazor developers and tech leads evaluating Ducky. Task: use a real app, provoke the hard cases (double submit, typing fast, two tabs, a failing payment, a reload), and see which Ducky feature handled each one, with the code that does it. Proof/content: the app runs the real Ducky packages from src; every X-ray card shows the app's own compiled source and names the invariant or guide behind the behaviour; the inspector shows every action with its DispatchResult. Constraints: no server (an in-browser fake API with real latency on a TimeProvider), WCAG 2.2 AA, keyboard complete, works on a phone, trimmed WASM publish with zero warnings, pre-release honesty (no fake adopters or numbers).

Memorable moment: flipping X-ray on turns the shop into its own annotated blueprint: every region gets an ink outline and a yolk tag naming the feature (Switch, Exhaust, Persist, SyncAcrossTabs, EntityState, Produce, Reactive polling, Middleware veto), and pressing a tag opens the code that powers it while the inspector tape keeps scrolling.

## Direction contract

THESIS: The rubber duck supply shop that explains itself. A real little store (search, catalogue, cart, checkout, order tracking, profile) built only from Ducky, where an X-ray switch reveals the feature behind every behaviour. Refuses the demo-page default: a grid of isolated widgets with "click me" buttons and a counter.

OWN-WORLD: The site's three inks and one beak. The shop is a white answer sheet on the yolk desk; the inspector is the ink dispatcher tape from the home page (ruled rows, DispatchResult lamps: yolk Reduced, beak Dropped/Failed, ring Queued/Vetoed); X-ray tags are yolk pills with ink text on 3px ink outlines; products are authored duck silhouettes in their own colours on sheet. Anybody wide black headings, Atkinson body, JetBrains Mono for actions, state and code. Flat: no shadows, no gradients.

STORY: The visitor shops, sees it behave correctly under abuse, turns X-ray on, understands which Ducky piece did each thing, reads the code, and leaves knowing how they would write it.

FIRST VIEWPORT: Yolk desk. Top bar: Ducky Playground wordmark, the X-ray switch as the primary control (big pill, off by default), Inspector toggle, links back to site and docs. Main: the shop sheet with the live search field across the top, the catalogue grid of duck cards, and the cart column on the right. Bottom: the ink inspector tape, collapsed to one live row by default, expanding to the full log, state and render counters.

FORM: A real mini-app with an X-ray overlay, position 3 of 7 on the grounded list; seed key 09111508.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

## Unresolved

- Whether to add a Playwright smoke test for the playground in test/Ducky.E2E (desirable; depends on the E2E host).
