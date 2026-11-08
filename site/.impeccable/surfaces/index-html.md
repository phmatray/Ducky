---
version: 1
slug: "index-html"
primary_target: "index.html"
related_targets: []
---

# Surface brief: Ducky landing (site/index.html)

Scope: the GitHub Pages home of Ducky 2.0, served at the site root; docfx guides at ./docs/, WASM sample at ./sample/. Visitor mode: **Persuade**.

Audience and job: Blazor developers and tech leads choosing a state library. Action: install (`dotnet add package Ducky --prerelease`) and open the guide. Proof: compiled, tested snippets from test/Ducky.Docs.Tests; invariants INV-nn with their named tests (SPEC §4); CI gates (100% line+branch coverage, mutation ≥90 target / break 85, NativeAOT + trimmed AOT-WASM smoke, zero IL warnings). Constraint: 2.0 is pre-release; no adopters, downloads, stars or benchmark numbers.

Memorable moment: the duck interrogates the visitor's state (the questions Blazor devs get burned by) and every answer is real code plus the named test that proves it.

## Direction contract

THESIS: Rubber-duck review. The page is the conversation a developer has with the rubber duck on their desk: the duck asks the hard questions about state in Blazor, and Ducky answers each with compiled code and a named invariant test. Refuses the category default: dark hero, gradient glow, one code block, six icon cards.

OWN-WORLD: Drenched duck-vinyl yellow owns the first viewport and every "duck asks" field; ink black and answer-sheet white are the only other inks, beak orange the single action accent. No tints, no gradients, no glows. Expanded black display letters stamped like a toy's moulded mark; JetBrains Mono only for code and test names. Controls invert ink on press. The action log is a ruled board of fixed cells where DispatchResult lamps light per row.

STORY: The visitor recognises their own fears (races, prerender, tabs, dropped circuits, AOT, testing), sees each answered with code and a test name, watches one dispatcher reduce one action at a time, sees the render-mode matrix and the gates, then installs.

FIRST VIEWPORT: Full-bleed yellow. Left two thirds: headline at display scale, one-sentence offer, install command with copy as the primary action, then guide and GitHub. Right third: the duck mark large, speech bubble asking the current question. Along the bottom edge: the question chips that drive the interrogation.

FORM: Rubber-duck debugging desk, position 6 of 7 on the grounded list (literal reading, assigned by the roll); seed key 683d22d7. Raises: from the split-flap concourse (competitive), the action log as a ruled board of fixed cells with per-row result lamps; from the rubber-hose cel (declined), one motion beat for the whole page, as the dispatcher reduces one action per tick; from the Movida cover (declined), a strict three-ink set with no tints; from the one-bit desktop (declined), controls that invert on press with explicit state vocabulary.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

## Unresolved

- Real adopters/testimonials: none exist; the page has no such section.
- Mutation scores: the campaign (M16-02) is still running; the page states the gate, not a measured score.
