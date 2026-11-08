---
name: Ducky
description: Blazor state you can explain to a duck. A rubber-duck review in three inks.
colors:
  yolk: "#FFD425"
  ink: "#17140F"
  sheet: "#FFFFFF"
  beak: "#FF6B1A"
  on-yolk-2: "#4A3C00"
  on-sheet-2: "#4F4838"
  on-ink-2: "#CBC3AA"
  ink-hairline: "#3A342A"
  code-type: "#FFB27A"
  code-str: "#FF8A4C"
  code-com: "#A39C88"
typography:
  display:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "clamp(2.8rem, 5.6vw, 5.2rem)"
    fontWeight: 900
    lineHeight: 0.94
    letterSpacing: "-0.035em"
    fontVariation: "'wdth' 132"
  headline:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "clamp(2.1rem, 4.6vw, 3.9rem)"
    fontWeight: 880
    lineHeight: 0.98
    letterSpacing: "-0.025em"
    fontVariation: "'wdth' 125"
  title:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "clamp(1.9rem, 3vw, 2.6rem)"
    fontWeight: 880
    lineHeight: 0.98
    letterSpacing: "-0.025em"
    fontVariation: "'wdth' 125"
  speech:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "clamp(1.15rem, 1.7vw, 1.45rem)"
    fontWeight: 820
    lineHeight: 1.18
    letterSpacing: "-0.01em"
    fontVariation: "'wdth' 112"
  body:
    fontFamily: "Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "1.125rem"
    fontWeight: 400
    lineHeight: 1.6
  label:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "0.78rem"
    fontWeight: 900
    lineHeight: 1
    letterSpacing: "0.08em"
    fontVariation: "'wdth' 150"
  code:
    fontFamily: "JetBrains Mono, ui-monospace, SF Mono, Menlo, Consolas, monospace"
    fontSize: "0.9rem"
    fontWeight: 400
    lineHeight: 1.65
    fontFeature: "'liga' 0, 'calt' 0"
  reading:
    fontFamily: "Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "1.0625rem"
    fontWeight: 400
    lineHeight: 1.66
  sheet-title:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "clamp(2.2rem, 4.2vw, 3.5rem)"
    fontWeight: 900
    lineHeight: 0.98
    letterSpacing: "-0.03em"
    fontVariation: "'wdth' 128"
  sheet-heading:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "clamp(1.55rem, 2.4vw, 2rem)"
    fontWeight: 880
    lineHeight: 1.05
    letterSpacing: "-0.02em"
    fontVariation: "'wdth' 122"
  sheet-subhead:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "1.25rem"
    fontWeight: 840
    lineHeight: 1.15
    letterSpacing: "-0.02em"
    fontVariation: "'wdth' 114"
  label-small:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "0.72rem"
    fontWeight: 900
    lineHeight: 1
    letterSpacing: "0.08em"
    fontVariation: "'wdth' 150"
  tab-label:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "0.74rem"
    fontWeight: 900
    lineHeight: 1
    letterSpacing: "0.07em"
    fontVariation: "'wdth' 112"
  code-sheet:
    fontFamily: "JetBrains Mono, ui-monospace, SF Mono, Menlo, Consolas, monospace"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.65
    fontFeature: "'liga' 0, 'calt' 0"
rounded:
  highlight: "3px"
  focus: "4px"
  chip-inline: "4px"
  kbd: "6px"
  row-current: "8px"
  chip-code: "8px"
  panel: "14px"
  code: "16px"
  bubble: "22px"
  pill: "999px"
spacing:
  gutter: "clamp(1.25rem, 4vw, 3rem)"
  wrap: "1240px"
  section: "clamp(4rem, 9vw, 7rem)"
  block: "2.4rem"
  rule: "3px"
  hairline: "1.5px"
  binder: "1480px"
  ledger: "16.5rem"
  tab-strip: "3.4rem"
  rail: "13.5rem"
  measure: "35rem"
  measure-wide: "60rem"
components:
  button-install:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.sheet}"
    rounded: "{rounded.panel}"
    padding: "0.95rem 1.2rem"
    typography: "{typography.code}"
  button-install-action:
    backgroundColor: "{colors.beak}"
    textColor: "{colors.ink}"
    padding: "0 1.1rem"
  button-install-action-hover:
    backgroundColor: "{colors.yolk}"
  button-install-pressed:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
  chip-question:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.pill}"
    padding: "0.62rem 1rem"
  chip-question-hover:
    backgroundColor: "{colors.sheet}"
  chip-question-selected:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
  chip-control-on-ink:
    backgroundColor: "transparent"
    textColor: "{colors.yolk}"
    rounded: "{rounded.pill}"
    padding: "0.62rem 1rem"
    typography: "{typography.code}"
  chip-control-on-ink-hover:
    backgroundColor: "{colors.yolk}"
    textColor: "{colors.ink}"
  chip-control-on-ink-pressed:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
  code-block:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.sheet}"
    rounded: "{rounded.code}"
    padding: "1.3rem 1.4rem"
    typography: "{typography.code}"
  speech-bubble:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
    rounded: "{rounded.bubble}"
    padding: "1rem 1.25rem 1.1rem"
    typography: "{typography.speech}"
  board-header:
    backgroundColor: "{colors.yolk}"
    textColor: "{colors.ink}"
    height: "2.6rem"
    typography: "{typography.label}"
  board-row:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.sheet}"
    height: "2.75rem"
    padding: "0 0.9rem"
  api-chip:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
    rounded: "{rounded.chip-code}"
    padding: "0.3rem 0.55rem"
  list-row-link-hover:
    backgroundColor: "{colors.yolk}"
  list-row-link-pressed:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
  api-chip-docs:
    backgroundColor: "{colors.yolk}"
    textColor: "{colors.ink}"
    rounded: "{rounded.chip-inline}"
    padding: "0.06em 0.32em"
  index-tab:
    backgroundColor: "{colors.yolk}"
    textColor: "{colors.ink}"
    rounded: "{rounded.panel}"
    padding: "0.8rem 0.5rem 0.8rem 0.45rem"
    typography: "{typography.tab-label}"
  index-tab-hover:
    backgroundColor: "{colors.sheet}"
  index-tab-current:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
  ledger-row-current:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
    rounded: "{rounded.row-current}"
  code-slab:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.sheet}"
    rounded: "{rounded.code}"
    padding: "1.15rem 1.3rem 1.25rem"
    typography: "{typography.code-sheet}"
    width: "{spacing.measure-wide}"
  copy-pill:
    backgroundColor: "transparent"
    textColor: "{colors.yolk}"
    rounded: "{rounded.pill}"
    padding: "0.3rem 0.75rem"
  copy-pill-hover:
    backgroundColor: "{colors.yolk}"
    textColor: "{colors.ink}"
  copy-pill-pressed:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
  table-header:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
    padding: "0.7rem 0.9rem"
    typography: "{typography.label-small}"
  alert-frame:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.panel}"
    padding: "0.95rem 1.2rem 1.05rem"
    width: "{spacing.measure}"
  pager-link:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.panel}"
    padding: "0.9rem 1.1rem"
  pager-link-hover:
    backgroundColor: "{colors.yolk}"
  pager-link-pressed:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
  search-field:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
    rounded: "{rounded.pill}"
    height: "2.6rem"
    padding: "0 2.6rem 0 2.5rem"
  search-results:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
    rounded: "{rounded.panel}"
    width: "40rem"
  search-result-selected:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.sheet}"
---

# Design System: Ducky

## Overview

**Creative North Star: "The Rubber Duck on the Desk"**

The site is the conversation a developer has with the rubber duck beside their keyboard. The duck asks, on a field of drenched vinyl yellow; Ducky answers on a white answer sheet, in black ink and compiled code. Every surface is one of three grounds (yolk, ink, sheet), every stroke is ink, and a single beak orange marks the thing to press. Nothing is tinted, graded or lit: depth comes from swapping grounds and from heavy ruled lines, the way a moulded toy or a printed form gets its structure.

Density is generous at the section level (sections breathe at 4 to 7rem) and tight inside instruments: the dispatcher board is a ruled grid of fixed cells, the API list and package stack are ruled ledgers, and proof is a column of test names with check lamps. Display type is Anybody pushed wide and black, stamped like a toy's moulded mark; body is Atkinson Hyperlegible Next; JetBrains Mono appears wherever the reader is looking at something they would type or run.

Motion has one clock. A single beat (420ms, expo-out) drives the duck's squash-and-stretch quack, the speech bubble's wipe, the answer sheet's rise, the split-flap letters and lamps on the board, and the dispatcher's one-action-per-tick drain. Controls invert ink on press rather than glowing or lifting.

The documentation is the same world set as the duck's answer binder: yolk is the binder ground and its index tabs, the page being read is a white sheet in a 3px ink frame, ink is code, rules and the current tab. It runs denser than the home page (a reading ramp at a 35rem measure, small 3 to 8px radii on inline marks) but adds no new color, no shadow and no tint.

**Key Characteristics:**
- Three grounds (yolk, ink, sheet) and one action accent (beak); secondary text hues are darker or lighter shades of the ground they sit on, never decorative tints.
- 3px ink rules for structure, 1.5px hairlines inside tables and ledgers.
- Wide, black Anybody display; Atkinson body; JetBrains Mono for code, API names, test names and the board.
- Pills for every control, 14 to 16px corners for panels and code, a 22px speech bubble with a drawn tail.
- One motion beat for the whole page; press = ink inversion.
- Flat. No shadows, no gradients, no glows.
- Docs: a yolk binder with index tabs on the white sheet's edge; ruled ledgers for every list of pages or headings.

## Colors

A strict three-ink set on saturated toy-vinyl yellow, with beak orange as the only accent.

### Primary
- **Duck-Vinyl Yolk** (yolk): the page ground. Owns the first viewport, the tour, the platform section and the close; it is also the "lit" color on dark grounds (board headers, lamps for reduced actions, headings on ink sections, focus rings on ink) and the keyword color in code. In the docs it is the binder ground, the index tabs, the highlighter behind inline code, the lamp for the section in view and for tested code, the hover fill of links and pager cards, and the underline under search hits.

### Secondary
- **Beak Orange** (beak): the single action accent. The copy segment of the install command, the text caret, the focus ring on code blocks. In the dispatcher board it is also the alarm state: the dropped-action lamp and the "stale run" note; in the docs it is the lamp on warning and caution alerts and the focus ring inside code slabs. Never a section ground, never body text on yolk.

### Neutral
- **Moulded Ink** (ink): all text and strokes on yolk and sheet; the ground of code blocks, the dispatcher board section, the receipts section and the footer.
- **Answer-Sheet White** (sheet): the ground where Ducky answers (answer sheet, render-mode matrix, packages, migration), the speech bubble, and the hover fill for outline chips on yolk.
- **Yolk Shadow-Ink** (on-yolk-2): secondary text on yolk (the offer line, ledger descriptions, notes). A darkened yolk hue chosen for contrast, not a tint.
- **Sheet Graphite** (on-sheet-2): secondary text on white (supporting paragraphs, matrix "no" cells, package descriptions).
- **Ink Parchment** (on-ink-2): secondary text on ink (board sequence and origin columns, queued lamps, footer links, the quiet Reset control).
- **Ink Hairline** (ink-hairline): the 1.5px dividers between cells and rows on ink grounds (board rows, state panel, receipts ledger). Custom property `--ink-hairline`.

### Code
- **Code Type Peach** (code-type), **Code String Orange** (code-str), **Code Comment Stone** (code-com): syntax hues used only inside ink code blocks; keywords and attributes reuse yolk. They are beak's family seen on ink, kept inside code.

### Named Rules
**The Three Grounds Rule.** A section's ground is yolk, ink or sheet, full-bleed, and nothing else. Sections alternate grounds and separate with a 3px ink rule; no panel, card or band introduces a fourth ground.

**The Derived Text Rule.** A secondary text color belongs to its ground (on-yolk-2, on-sheet-2, on-ink-2) and exists for legibility. Never carry one ground's secondary hue onto another ground.

**The One Beak Rule.** Beak marks the thing to press, or an alarm (a dropped result, a warning or caution lamp). If two beak elements compete in one viewport, one of them is wrong.

## Typography

**Display Font:** Anybody (variable width 50 to 150%, weights 100 to 900), with Atkinson Hyperlegible Next and system-ui fallback
**Body Font:** Atkinson Hyperlegible Next, with system-ui fallback
**Label/Mono Font:** JetBrains Mono, with ui-monospace, SF Mono, Menlo, Consolas

**Character:** A wide, ultra-black display face that reads like lettering moulded into a plastic toy, set against a body face built for legibility. Mono is the voice of the code and the tests, never decoration.

All three faces are self-hosted latin subsets (`fonts/`), loaded with `font-display: swap`; Anybody and Atkinson are preloaded.

### Hierarchy
- **Display** (900, clamp(2.8rem, 5.6vw, 5.2rem), 0.94, width 132%): the hero headline only, max 13ch. On phones it narrows to width 118%.
- **Headline** (880, clamp(2.1rem, 4.6vw, 3.9rem), 0.98, width 125%): section headings, balanced wrap. The closing heading goes larger (clamp(2.8rem, 7vw, 6rem), 900, width 140%).
- **Title** (880, clamp(1.9rem, 3vw, 2.6rem), 0.98): the answer headings on the answer sheet.
- **Speech** (820, clamp(1.15rem, 1.7vw, 1.45rem), 1.18, width 112%): the duck's question in the bubble.
- **Body** (400, 1.125rem, 1.6): running text, 1.06rem on phones; ledes at 1.2rem capped at 62ch, answer paragraphs at 52ch.
- **Label** (900, 0.78rem, 0.08em tracking, uppercase, width 150%): column headers of the board and matrix, and the docs mark pill in the docs bar. The docs use a smaller step (label-small, 0.7 to 0.74rem, same weight, tracking and width) for ON THIS PAGE, ledger group titles, PREVIOUS / NEXT, search result section tags, alert titles and table headers.
- **Code** (400 to 700, 0.86 to 1.5rem, 1.65 in blocks, ligatures off): code blocks, install command, API and package names, test names, board cells, numeric readouts (tabular numerals).

### Docs reading ramp
The docs set a denser ramp on the sheet. Body text is the reading token (1.0625rem / 1.66, 1.02rem on phones) held to a 35rem measure (about 65 to 75ch) for paragraphs, lists, headings and alerts; code slabs and tables take up to 60rem.
- **Sheet title** (900, clamp(2.2rem, 4.2vw, 3.5rem), 0.98, width 128%, -0.03em, max 22ch): the page's H1; on phones clamp(1.9rem, 8.5vw, 2.4rem) at width 116%.
- **Sheet heading** (880, clamp(1.55rem, 2.4vw, 2rem), 1.05, width 122%): H2, always with a 3px ink rule above and 1rem of air between; width 112% on phones.
- **Sheet subhead** (840, 1.25rem, 1.15, width 114%): H3.
- **H4** is a run-in in the body face at 800, 1.05rem: the one heading level below the display ramp.
- **Labels** 0.7 to 0.8rem, uppercase wide (label and label-small); the index tabs use their own tab-label setting (900, 0.74rem, 0.07em, width 112%) because they are set vertically in a narrow strip.
- **Code on the sheet** (code-sheet, 0.875rem / 1.65, 0.8rem on phones); captions and Copy in mono 0.78 to 0.8rem.

### Named Rules
**The Stamped Mark Rule.** Headings are Anybody at 820 to 900 weight and at least 112% width, with negative tracking. A narrow or light display setting is off-system. (The docs' H4 run-in in the body face is the only heading set outside the display face.)

**The Wide Label Rule.** Uppercase wide labels (Anybody 900, 150% width, 0.08em tracking) name an instrument's parts: column headers, table headers, ON THIS PAGE, ledger group titles, PREVIOUS / NEXT, search result tags, alert titles. They never sit above a section heading as a kicker.

**The Typed Things Rule.** If the reader could type it, run it or find it in a test report, it is set in JetBrains Mono; prose is never set in mono.

## Layout

Content sits in a 1240px wrap with a fluid gutter (clamp(1.25rem, 4vw, 3rem)); section grounds bleed full width behind it. Sections pad vertically at clamp(4rem, 9vw, 7rem) (the close goes to 8rem, the migration band to 6rem), and blocks inside a section start 2.4rem below their heading.

Grids are asymmetric two-column splits on a 12-part feel: the hero runs 7fr / 5fr (copy, then duck) with the question chips spanning the bottom edge above a 3px rule; the answer sheet runs 5fr text / 7fr code with proof stacked under the code; platform and receipts use 5fr / 7fr; the dispatcher console runs 4fr controls and state / 8fr board. Ledgers (API list, package stack, receipts) are two-column definition grids with ruled rows.

### Docs binder
The docs use a wider binder (1480px max, gutter clamp(1rem, 3vw, 2rem)) on a three-column grid: the section's page ledger (16.5rem, sticky), the sheet (fluid), and the tab strip (3.4rem, sticky) on the sheet's right edge. The sheet pads clamp(1.8rem, 4vw, 3.4rem) at the top, with a wider left margin (up to 4.4rem) that holds the punch row. The on-this-page rail (13.5rem) floats right at the sheet's head so prose wraps beside it, then code and tables take the full width below it. Prose blocks step 1.05em apart; H2 opens 2.2em above.

At 1280px the rail joins the sheet's head as a two-column ruled list (H3 entries hidden). At 960px the binder stacks: the ledger folds into a disclosure on the yolk ground, the tabs run along the sheet's top edge as a horizontally scrolling strip with the current tab scrolled into view, the punch row hides, and search drops to its own full-width row. At 640px the rail goes to one column, code slabs bleed 0.4rem past the sheet's padding at 12px corners, and the pager stacks.

Breakpoints: at 960px every split collapses to one column and the duck drops below the copy, right-aligned; at 640px the bar keeps only brand and GitHub, the install command goes full width, the board hides its origin column and lets rows grow, and the render-mode matrix becomes one ruled block per host with inline labels.

## Elevation & Depth

Flat. There are no shadows anywhere. Depth is conveyed by changing ground (yolk to sheet to ink), by 3px ink rules and borders, and by inversion on press. The only lift is a 2px downward translate on the pressed install command, which reads as a key going down, not as elevation. In the docs, the index tabs slide 3px out on hover and 6px on press (upward along the top edge at 960px and below): a tab being pulled, not lifted. The current tab stacks above the sheet and shares its frame; the other tabs tuck behind it.

### Named Rules
**The Printed Object Rule.** No box-shadow, no gradient, no glow, no blur at rest. Separation is a rule, a border or a ground change.

## Shapes

Two families. Controls are full pills (999px) with a 3px ink border: question chips, tabs, console controls, the GitHub link, migration links; smaller pill badges (version chip, INV links, test-kind tags) use 2px or 1.5px borders. Containers are softly rounded rectangles: 14px for the install command, the state panel and the board frame; 16px for code blocks and the matrix frame; 8px for inline API name chips. The speech bubble is the one soft outlier at 22px, with a two-layer triangular tail (ink outline, sheet fill).

The docs add a small-radius family for inline marks: 3px on the yolk highlight behind a hovered link, 4px on inline code chips (and the focus ring), 6px on the search field's key hint, 8px on the current row of the page ledger. The sheet is 14px with a square top-right corner where the tab strip meets it (fully rounded once the tabs move to the top edge); tables, alerts, pager cards and the search results sheet share the 14px frame; code slabs keep 16px.

Round marks are lamps: filled or outlined circles with a 2px border (board result lamps, matrix "yes" dots, the yolk check discs beside test names). The docs reuse them: on-this-page dots (1.5px ring, yolk when the section is in view), alert lamps (yolk, beak for warning and caution), the yolk lamp before "from a tested file", and the punch holes down the sheet's left margin (yolk discs in a 3px ink ring, drawn as an SVG pattern).

## Components

### Buttons
Tactile and literal: a control looks like the thing it does.
- **Install command (primary):** an ink slab (14px corners, 3px ink border) holding the command in mono with a yolk `$` prompt, joined to a beak copy segment with an icon and label. Hover turns the copy segment yolk; press drops the slab 2px and inverts the command to ink on white; the copied state holds that inversion for five beats with the prompt turned beak.
- **Text actions:** "Read the guide" is an 800-weight link with a 3px underline and an arrow that slides 4px on hover; secondary links are 700 weight with the default 2px underline, thickening to 3px on hover.

### Chips
- **Question chips (on yolk):** transparent, 3px ink pill, Atkinson 700. Hover fills sheet; selected and pressed invert to ink with yolk text. They are a real tablist driving the answer sheet.
- **Tabs (on yolk):** same pill, set in mono 600; selected inverts to ink.
- **Console controls (on ink):** yolk outline pill in mono; hover fills yolk, press flips to sheet. The quiet variant (Reset) uses on-ink-2 until hovered.
- **Badges:** the version chip pairs a filled ink pill with an outline pill; INV links are yolk mono pills with a 2px ink border that invert on hover.

### Cards / Containers
There are no cards. Containers are frames and ledgers:
- **Code block:** ink ground, 16px corners, mono 0.9rem / 1.65, ligatures off, horizontal scroll, yolk keywords; selection inverts to yolk.
- **Ledgers:** a 3px rule on top, 1.5px rules between rows, no fill; package rows fill yolk on hover and invert to ink on press.
- **Render-mode matrix:** a 3px-framed table, ink header row with yolk uppercase labels, yolk dot for supported cells.

### Navigation
A plain top bar on yolk: wide black wordmark with the duck, the version badges, then 700-weight text links that underline on hover and a GitHub pill that inverts on hover. Footer is an ink band with on-ink-2 links that turn yolk on hover. Focus everywhere is a 3px solid outline (ink on light grounds, yolk on ink grounds, beak on code) offset 3px.

### Speech Bubble (signature)
The duck's voice: a sheet bubble with a 3px ink border, 22px corners and a drawn tail pointing at the duck. Each new question wipes in left to right over one and a half beats while the duck squashes and stretches.

### Dispatcher Board (signature)
A ruled board of fixed cells: yolk sticky header with wide uppercase labels, 2.75rem ink rows separated by ink hairlines, mono tabular text. New rows arrive letter by letter on a split-flap flip (0.7 beat); each row ends in a result lamp: yolk filled for Reduced, beak filled for Dropped, an outlined parchment ring for Queued. Beside it a state panel of labelled mono readouts in yolk, framed in yolk at 14px.

### Docs Binder (signature)
- **Index tabs:** yolk tabs on the sheet's right edge, 3px ink frame with the inner edge open and 14px outer corners, uppercase wide black labels set vertically (tab-label). They tuck behind the sheet's frame; hover fills sheet and slides out 3px, press 6px. The current section's tab inverts to ink with yolk text and sits over the sheet, sharing its frame. At 960px and below they stand along the sheet's top edge as a scrolling strip, the current tab kept in view (centred by script, ordered first without it).
- **Punch row:** authored SVG holes, yolk in a 3px ink ring every 160px, down the sheet's left margin; desktop only.
- **Page ledger:** the section name in the title setting (880, width 125%) over a 3px rule, optional uppercase group titles in on-yolk-2, then a list ruled at 1.5px. Rows nudge 0.4rem right and underline on hover; the current page inverts to ink with yolk text in an 8px row. Folds into a disclosure at 960px.
- **On-this-page rail:** an uppercase ON THIS PAGE label over a 3px rule, then entries ruled at 1.5px with lamp dots; the entry for the section in view goes bold with a yolk lamp.

### Docs Content
- **Inline code:** in docs prose, a yolk highlighter chip with ink text (4px, 0.86em, ligatures off): the docs variant of the api-chip. An ink chip every few words reads as black blotches in dense diagnostics prose. Inside a hovered link it inverts to ink with yolk text; in table headers it drops its fill.
- **Links:** 2px underline at 0.2em offset; hover lays a yolk highlight (3px corners) behind the word.
- **Code slabs:** ink slabs (16px, up to 60rem) with a provenance caption on top: the source file as a yolk mono link, "from a tested file" in on-ink-2 behind a yolk lamp, separated from the code by an ink hairline; a Copy pill (2px yolk outline, mono) in the top-right corner fills yolk on hover and flips to sheet on press or once copied. Selection inverts to yolk; focus is a beak ring inset in the slab.
- **Tables:** framed ledgers (3px ink frame, 14px, up to 60rem, scrolling sideways when needed) with an ink header row of yolk wide labels, ink-hairline dividers between header cells and 1.5px ink rules between body rows and columns.
- **Alerts:** a 3px ruled frame at 14px with no fill or tint, a stamped uppercase title preceded by a lamp (yolk; beak for warning and caution). The generator's glyph icons are hidden.
- **Pager:** two 3px-framed 14px cards under a 3px rule at the sheet's foot, an uppercase PREVIOUS / NEXT label over the page title; hover fills yolk, press inverts to ink. Stacks on phones.

### Search
- **Field:** a sheet pill (2.6rem tall, 3px ink border) in the bar with a drawn magnifier and a `/` key hint in a 6px mono key that hides on focus.
- **Results sheet:** a sheet panel (3px frame, 14px, up to 40rem wide) of rows ruled at 1.5px: page title bold with its section tag in label-small, then the heading and a snippet in on-sheet-2. The selected or hovered row inverts to ink. Hits are bold with a 3px yolk underline and no fill, so they never look like code.

### Proof List (signature)
Under every answer: "Proved by" with INV pills, then test names in mono, each preceded by a yolk check disc with an ink border; a small outline tag names the harness when it is not the default.

## Do's and Don'ts

### Do:
- **Do** build every surface from yolk, ink and sheet, and reserve beak for the primary action and the dropped state.
- **Do** separate sections and rows with ink rules: 3px for structure, 1.5px inside tables and ledgers.
- **Do** set headings in Anybody at 820 to 900 weight and 112 to 150% width with negative tracking.
- **Do** set code, API names, package names, test names and live readouts in JetBrains Mono with tabular numerals where numbers change.
- **Do** make controls pills with a 3px border that invert ink on press and selection.
- **Do** time motion in multiples of the single 420ms beat on the expo-out curve, and honour reduced motion.
- **Do** pair every claim with its proof: code block plus named tests.
- **Do** hold docs prose to the 35rem measure and let code slabs and tables take up to 60rem.
- **Do** caption every docs code slab with its source file and its tested status.
- **Do** keep the reader's place visible on every docs page: current tab in ink, current ledger row in ink, yolk lamp on the section in view.

### Don't:
- **Don't** add shadows, gradients, glows or translucent overlays; depth is ground, rule and inversion.
- **Don't** introduce tints of yolk or ink as fills; secondary hues exist only as text on their own ground.
- **Don't** put beak on more than one element per viewport outside the dispatcher board.
- **Don't** use cards with fills and corners as a layout device; use ruled ledgers and frames.
- **Don't** set prose or headings in mono, or code in the display face.
- **Don't** give a section heading a small label above it; headings stand alone.
- **Don't** set inline code in docs prose as ink chips; use the yolk highlighter chip.
- **Don't** style search hits with a fill or in mono; they are bold with a yolk underline so they never read as code.
- **Don't** tint alerts by kind; the kind lives in the stamped title and its lamp.
