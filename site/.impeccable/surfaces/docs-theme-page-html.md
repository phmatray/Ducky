---
version: 1
slug: "docs-theme-page-html"
primary_target: "docs-theme/page.html"
related_targets: []
---

# Surface brief: Ducky documentation (site/docs-theme/page.html)

Scope: every generated documentation page under ./docs/ (guides, threading contract, migration guides, comparison, diagnostics catalogue), rendered by the build from docs/**/*.md through one template. Visitor mode: **Read**.

Audience and job: Blazor developers mid-task, IDE open beside the browser, and tech leads comparing Ducky with Fluxor and the other options. Task: find the page, read the rule, copy compiled code, follow a migration checklist. Proof/content: every C# snippet is a #region of a compiled, tested file; invariants INV-nn and named tests; DUCKY diagnostics. Constraints: static output, no runtime dependency, works without JavaScript (search and copy enhance), WCAG 2.2 AA, prose measure 65-75ch, code readable on phones.

Memorable moment: the docs are the duck's answer binder. Section tabs stand up on the right edge of the white sheet like yolk index tabs; pulling a tab opens that section, and the current page is the sheet lying on top.

## Direction contract

THESIS: The answer binder. The documentation is the binder the rubber duck keeps on the desk: white answer sheets clipped in a yolk binder, sections as index tabs standing on the sheet's edge, compiled code in ink. Refuses the docs-generator default: grey left tree, blue links, a centred column floating on white, a search box in a nav bar that looks like every other docs site.

OWN-WORLD: The home page's three inks and one beak, unchanged. Yolk is the binder (page ground and tabs), sheet is the page being read, ink is code, rules and the current tab. Index tabs are yolk pills turned on edge with wide black Anybody labels; the current section's tab inverts to ink. Ruled ledgers for page lists and the on-this-page rail; 3px ink rules for structure, 1.5px hairlines inside. Flat, no tints, no shadows.

STORY: The reader lands on the sheet, sees where it sits (tab, section ledger, heading), reads the rule in plain prose, copies compiled code, and turns to the next sheet or another tab without losing their place.

FIRST VIEWPORT: Yolk binder ground. A white sheet with 3px ink frame takes the reading column; its right edge carries the stacked section tabs (current one ink). Left of the sheet, the current section's ruled page ledger; top bar with wordmark, search field and GitHub. The page title in the stamped headline face opens the sheet.

FORM: Ring binder with index tabs, position 6 of 7 on the grounded list; seed key b4368085.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

## Unresolved

- API reference pages: not generated (docfx's API pages were not published either); XML docs ship in the packages for IDEs.
