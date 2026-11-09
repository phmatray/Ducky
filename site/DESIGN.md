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
  side-title:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 880
    lineHeight: 1.05
    letterSpacing: "-0.02em"
    fontVariation: "'wdth' 122"
  action:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "1.15rem"
    fontWeight: 900
    lineHeight: 1
    letterSpacing: "-0.01em"
    fontVariation: "'wdth' 125"
  switch-label:
    fontFamily: "Anybody, Atkinson Hyperlegible Next, system-ui, sans-serif"
    fontSize: "1.02rem"
    fontWeight: 900
    lineHeight: 1
    letterSpacing: "0.06em"
    fontVariation: "'wdth' 150"
  annotation:
    fontFamily: "JetBrains Mono, ui-monospace, SF Mono, Menlo, Consolas, monospace"
    fontSize: "0.82rem"
    fontWeight: 700
    lineHeight: 1
    fontFeature: "'liga' 0, 'calt' 0, 'tnum' 1"
rounded:
  highlight: "3px"
  focus: "4px"
  chip-inline: "4px"
  kbd: "6px"
  row-current: "8px"
  chip-code: "8px"
  code-small: "12px"
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
  tape-bar: "3.6rem"
  tape-open: "min(27rem, 54vh)"
  drawer: "min(46rem, 46vw)"
  xray-offset: "7px"
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
  xray-switch:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.pill}"
    height: "3.1rem"
    padding: "0.3rem 0.5rem 0.3rem 1.15rem"
    typography: "{typography.switch-label}"
  xray-switch-hover:
    backgroundColor: "{colors.sheet}"
  xray-switch-on:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
  xray-tag:
    backgroundColor: "{colors.yolk}"
    textColor: "{colors.ink}"
    rounded: "{rounded.pill}"
    height: "2.1rem"
    padding: "0.3rem 0.75rem 0.3rem 0.6rem"
    typography: "{typography.annotation}"
  xray-tag-open:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
  xray-tag-pressed:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
  render-count:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
    rounded: "{rounded.pill}"
    height: "1.7rem"
    padding: "0 0.6rem"
  control-pill:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.pill}"
    height: "2.6rem"
    padding: "0.45rem 1.05rem"
  control-pill-pressed:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
  filter-chip:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.pill}"
    height: "2.2rem"
    padding: "0.25rem 0.75rem"
  filter-chip-hover:
    backgroundColor: "{colors.yolk}"
  filter-chip-selected:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
  stepper-button:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.pill}"
    size: "2rem"
  stepper-button-hover:
    backgroundColor: "{colors.yolk}"
  button-pay:
    backgroundColor: "{colors.beak}"
    textColor: "{colors.ink}"
    rounded: "{rounded.pill}"
    height: "3.5rem"
    padding: "0.5rem 1.2rem"
    typography: "{typography.action}"
  button-pay-hover:
    backgroundColor: "{colors.yolk}"
  button-pay-pressed:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.sheet}"
  button-pay-busy:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
  button-pay-disabled:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.on-sheet-2}"
  field-pill:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
    rounded: "{rounded.pill}"
    height: "2.6rem"
    padding: "0 1rem"
  shop-search-field:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
    rounded: "{rounded.pill}"
    height: "3.4rem"
    padding: "0 1.2rem"
  code-drawer:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.sheet}"
    rounded: "{rounded.code}"
    width: "{spacing.drawer}"
  file-tab:
    backgroundColor: "transparent"
    textColor: "{colors.yolk}"
    rounded: "{rounded.pill}"
    height: "2.2rem"
    padding: "0.3rem 0.85rem"
  file-tab-selected:
    backgroundColor: "{colors.yolk}"
    textColor: "{colors.ink}"
  inspector-tape:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.sheet}"
    rounded: "{rounded.code}"
    height: "{spacing.tape-bar}"
  tape-board-header:
    backgroundColor: "{colors.yolk}"
    textColor: "{colors.ink}"
    height: "2.4rem"
    padding: "0 0.8rem"
    typography: "{typography.label-small}"
  tape-board-row:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.sheet}"
    height: "2.5rem"
    padding: "0 0.8rem"
  tape-board-row-selected:
    backgroundColor: "{colors.yolk}"
    textColor: "{colors.ink}"
  tape-detail-code:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.sheet}"
    rounded: "{rounded.code-small}"
    padding: "0.8rem 0.9rem"
  cart-jump:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.yolk}"
    rounded: "{rounded.pill}"
    height: "2.8rem"
    padding: "0.4rem 1.1rem"
  cart-jump-pressed:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
---

# Design System: Ducky

## Overview

**Creative North Star: "The Rubber Duck on the Desk"**

The site is the conversation a developer has with the rubber duck beside their keyboard. The duck asks, on a field of drenched vinyl yellow; Ducky answers on a white answer sheet, in black ink and compiled code. Every surface is one of three grounds (yolk, ink, sheet), every stroke is ink, and a single beak orange marks the thing to press. Nothing is tinted, graded or lit: depth comes from swapping grounds and from heavy ruled lines, the way a moulded toy or a printed form gets its structure.

Density is generous at the section level (sections breathe at 4 to 7rem) and tight inside instruments: the dispatcher board is a ruled grid of fixed cells, the API list and package stack are ruled ledgers, and proof is a column of test names with check lamps. Display type is Anybody pushed wide and black, stamped like a toy's moulded mark; body is Atkinson Hyperlegible Next; JetBrains Mono appears wherever the reader is looking at something they would type or run.

Motion has one clock. A single beat (420ms, expo-out) drives the duck's squash-and-stretch quack, the speech bubble's wipe, the answer sheet's rise, the split-flap letters and lamps on the board, and the dispatcher's one-action-per-tick drain. Controls invert ink on press rather than glowing or lifting.

The documentation is the same world set as the duck's answer binder: yolk is the binder ground and its index tabs, the page being read is a white sheet in a 3px ink frame, ink is code, rules and the current tab. It runs denser than the home page (a reading ramp at a 35rem measure, small 3 to 8px radii on inline marks) but adds no new color, no shadow and no tint.

The playground is the same world as a working shop on the desk: the yolk ground is the desk, the shop is one white answer sheet in a 3px ink frame, and the inspector is the home page's ink dispatcher board docked to the bottom edge as a tape. An X-ray switch turns the shop into its own annotated blueprint: ink outlines around every region, yolk tag pills naming the Ducky feature, ink render-count chips, all arriving in reading order inside one beat. Code opens in an ink drawer docked beside the sheet, never over it. It adds product colours (only inside the duck silhouettes and their filter dots) and no new ground, shadow or tint.

**Key Characteristics:**
- Three grounds (yolk, ink, sheet) and one action accent (beak); secondary text hues are darker or lighter shades of the ground they sit on, never decorative tints.
- 3px ink rules for structure, 1.5px hairlines inside tables and ledgers.
- Wide, black Anybody display; Atkinson body; JetBrains Mono for code, API names, test names and the board.
- Pills for every control, 14 to 16px corners for panels and code, a 22px speech bubble with a drawn tail.
- One motion beat for the whole page; press = ink inversion.
- Flat. No shadows, no gradients, no glows.
- Docs: a yolk binder with index tabs on the white sheet's edge; ruled ledgers for every list of pages or headings.
- Playground: a yolk desk with one white shop sheet, an ink inspector tape docked at the bottom, an ink code drawer docked at the side, and an X-ray layer of ink outlines and yolk tags.

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

### Product colours (playground)
- **Duck product hues**: the playground's catalogue data carries each duck's own colour (yellow and black and white reuse yolk, ink and sheet; mint #7FD8BE, red #E5484D, blue #6CB6FF, lilac #B9A3F0, green #7E9A4A). They are data, not tokens: they fill the duck silhouette's body and the 0.85rem swatch dot in the matching filter chip, and nothing else. Outline and details stay ink; the beak is ink (yolk on the black duck), so beak orange stays reserved for the Pay button and alarms.

### Named Rules
**The Three Grounds Rule.** A section's ground is yolk, ink or sheet, full-bleed, and nothing else. Sections alternate grounds and separate with a 3px ink rule; no panel, card or band introduces a fourth ground.

**The Derived Text Rule.** A secondary text color belongs to its ground (on-yolk-2, on-sheet-2, on-ink-2) and exists for legibility. Never carry one ground's secondary hue onto another ground.

**The One Beak Rule.** Beak marks the thing to press, or an alarm (a dropped result, a warning or caution lamp). If two beak elements compete in one viewport, one of them is wrong.

**The Product Colour Rule.** A product hue lives inside the product's drawing (the duck's body, its swatch dot) and never becomes a fill, border, text color or ground in the interface. A duck is never drawn with a beak-orange beak.

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

### Playground ramp
The playground's sheet reuses the docs steps: the shop name is sheet title (max 14ch; clamp(1.9rem, 8.5vw, 2.4rem) at width 116% on phones), the shelf count ("36 ducks") is sheet heading. It adds:
- **Side title** (880, 1.5rem, 1.05, width 122%): the order column's headings (Basket, Your order, Profile), with a mono count beside them in on-sheet-2.
- **Duck name** (840, 1.15rem, 1.12, width 114%): the product name in a shelf cell, a step below sheet subhead.
- **Drawer title** (880, clamp(1.35rem, 2vw, 1.75rem), width 122%) in yolk on the ink drawer.
- **Action** (900, 1.15rem, width 125%, tabular numerals): the Pay button's label, the price inside it.
- **Switch label** (900, 1.02rem, 0.06em, uppercase, width 150%): the X-RAY word on the switch; the one wide label large enough to read as a control.
- **Annotation** (JetBrains Mono 700, 0.82rem, tabular): X-ray tag names, lamps and tape readouts; render counts at 600, 0.74rem.
- Body on the sheet runs at 1rem / 1.5 for the lede (54ch) and 0.92 to 0.95rem / 1.45 to 1.5 for cell blurbs and side notes; the drawer summary is 1rem / 1.62 at 62ch; body steps to 1.04rem on phones. Wide labels (label, label-small) name the tape (INSPECTOR), the board's columns, the drawer's GUIDE and the detail panel's Payload and State changed.

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

### Playground desk
The playground has no wrap: the shop sheet spans the yolk desk between the gutters (clamp(1.25rem, 4vw, 3rem); 0.75rem on phones) under a wrapping top bar (wordmark and PLAYGROUND badge, then the X-ray switch and Inspector toggle, then Docs and GitHub). The sheet pads clamp(1.2rem, 2.2vw, 2rem) on top and clamp(1.4rem, 3vw, 2.6rem) at the sides; its head is a 6fr / 5fr split (shop name, lede with the disclosure) over a 3px rule. Below, the shop grid runs a fluid main column (search, filters, shelf, warehouse) and an order column of 19 to 23rem (basket, checkout, order, profile) behind a 3px vertical rule; regions in the order column are separated by 3px rules with 2rem of air. The shelf is an auto-fill grid of cells at least 14rem wide.

The inspector tape is fixed to the bottom edge at 3.6rem collapsed and min(27rem, 54vh) open, and the desk pads its foot by the tape's height plus 2rem so nothing hides behind it. Open, the tape is a 7fr board / 5fr detail split. The code drawer docks to the right edge at min(46rem, 46vw), from the top down to the tape; the sheet and top bar give up that width (plus 1rem) and the text links hide, so the shop stays usable beside the code.

The sheet is a size container: below 860px of sheet width the order column drops under the shelf behind a 3px rule and the head stacks. At 1100px the drawer becomes a bottom sheet (full width, min(72vh, 42rem) tall, above the tape). At 960px a basket jump pill appears bottom-right above the tape once the basket has a duck, hidden while the inspector or the drawer is open; the open tape stacks board over detail. At 640px the shelf is one column of cells with the duck beside the text (4.6rem), tags hidden and blurbs clamped to two lines; the X-ray switch stretches across the bar; the tape bar keeps its title, the latest action and the toggle (icon only), and the board keeps only sequence, action and result.

In X-ray mode, regions take 3rem more air on top (3.4rem gaps in the main column) so tags never cover content.

## Elevation & Depth

Flat. There are no shadows anywhere. Depth is conveyed by changing ground (yolk to sheet to ink), by 3px ink rules and borders, and by inversion on press. The only lift is a 2px downward translate on the pressed install command, which reads as a key going down, not as elevation. In the docs, the index tabs slide 3px out on hover and 6px on press (upward along the top edge at 960px and below): a tab being pulled, not lifted. The current tab stacks above the sheet and shares its frame; the other tabs tuck behind it.

The playground's docked panels follow the same law: the inspector tape and the code drawer are ink grounds docked to the viewport's edges with no shadow, no scrim and no backdrop; the shop beside them stays lit and usable. The drawer enters by sliding 2.5rem (up from the bottom at 1100px and below) with a fade over one beat; the Pay button drops 2px on press like the install command. X-ray outlines are outlines, not raised layers: they sit 7px off each region and tighten in from 16px as they appear.

### Named Rules
**The Printed Object Rule.** No box-shadow, no gradient, no glow, no blur at rest. Separation is a rule, a border or a ground change.

**The Docked Panel Rule.** A panel that opens over the page (code drawer, inspector tape) docks to a viewport edge as an ink ground and never dims, blurs or blocks what it sits beside: no backdrop, no focus trap.

## Shapes

Two families. Controls are full pills (999px) with a 3px ink border: question chips, tabs, console controls, the GitHub link, migration links; smaller pill badges (version chip, INV links, test-kind tags) use 2px or 1.5px borders. Containers are softly rounded rectangles: 14px for the install command, the state panel and the board frame; 16px for code blocks and the matrix frame; 8px for inline API name chips. The speech bubble is the one soft outlier at 22px, with a two-layer triangular tail (ink outline, sheet fill).

The docs add a small-radius family for inline marks: 3px on the yolk highlight behind a hovered link, 4px on inline code chips (and the focus ring), 6px on the search field's key hint, 8px on the current row of the page ledger. The sheet is 14px with a square top-right corner where the tab strip meets it (fully rounded once the tabs move to the top edge); tables, alerts, pager cards and the search results sheet share the 14px frame; code slabs keep 16px.

The playground keeps both families. Every control is a pill with a 3px ink border (the X-ray switch, Inspector, filter chips, the sort select with a drawn chevron, form fields, Add, Pay, the basket jump), with 2px-bordered circles for stepper and remove buttons and 1.5px-bordered pill tags in shelf cells. Containers: the shop sheet and X-ray outlines at 14px, the tape board frame at 14px in yolk, the code drawer and inspector tape at 16px on their inner corners only (square where they meet the viewport edge), the drawer's code slab at 16px, and the detail panel's payload block at 12px (code-small, the same 12px the docs give phone code slabs). The checkout's check box is a 1.35rem square at the 6px key radius. Icons are one drawn set on a 24-unit grid, 2.4 stroke, round caps, currentColor.

Round marks are lamps: filled or outlined circles with a 2px border (board result lamps, matrix "yes" dots, the yolk check discs beside test names). The docs reuse them: on-this-page dots (1.5px ring, yolk when the section is in view), alert lamps (yolk, beak for warning and caution), the yolk lamp before "from a tested file", and the punch holes down the sheet's left margin (yolk discs in a 3px ink ring, drawn as an SVG pattern).

The playground adds: DispatchResult lamps in the tape (0.78rem, 2px ring), order steps (yolk lamp for done, ink lamp for current, empty ring for pending), the veto ring in a shelf cell, the yolk lamp before "the file compiled into this build", the yolk disc marking the line in the drawer's gutter, and the beak lamp before an alert title.

The duck is the one figurative shape: the site's duck redrawn as a product silhouette (16-unit ink outline on a 400 by 360 grid, ink wing lines, eye with a white glint), filled with the product's colour.

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

### X-ray (signature)
- **X-ray switch (primary control):** a 3.1rem pill with a 3px ink border holding the wide X-RAY label, a 3.3 by 1.9rem track with a round ink knob, and a mono On / Off. Off it is transparent on yolk and fills sheet on hover; on, the whole pill inverts to ink with yolk text and the track turns yolk as the knob slides 1.4rem over one beat. A real `role="switch"`.
- **Region outlines:** in X-ray mode every region gets a 3px ink outline at 7px offset with 14px corners. Inside the sheet the outlines nest within the white ground; the topbar controls are outlined too.
- **Tags:** yolk pills (2.1rem, 3px ink border, annotation mono) sitting on the top edge of the outline: a drawn code icon, the feature name (Switch, Exhaust, Persist + SyncAcrossTabs, EntityState + selectors, Middleware veto), then a live detail after a 1.5px divider ("1 superseded", "v2, synced"). Each is a button that opens the code drawer; hover and the open tag invert to ink with yolk text, press flips to sheet, and focus returns to the tag when the drawer closes.
- **Render counts:** ink pills (1.7rem, yolk mono 600 0.74rem, tabular) on the opposite corner of the outline (bottom-right inside the sheet, top-right inside each shelf cell) counting that component's renders.
- **Reveal on one beat:** outlines tighten from 16px to 7px offset and tags and counts wipe in left to right (a pill-shaped clip) over 0.6 beat, staggered by 0.066 beat in reading order, top bar first and tape last, so the whole layer lands inside one beat. Off, the layer is display: none.
- **X-ray-only content:** notes and controls that provoke the hard cases (the warehouse desk, "Make the next payment fail", the Exhaust note) render only in X-ray mode, in on-sheet-2 side notes.

### Code Drawer (signature)
A non-modal ink side panel: 16px inner corners, docked right between the top and the tape, sliding in over one beat. The head carries the feature title in yolk and an on-ink Close pill over an ink hairline; the body holds the summary in sheet text, a GUIDE label with yolk guide links, then file tabs: 2px yolk outline pills in mono (2.2rem) that fill yolk when selected or hovered and flip to sheet on press, a real tablist. Below them a code slab framed by an ink hairline at 16px, captioned with the file path in yolk mono and "the file compiled into this build" behind a yolk lamp; the code runs in mono 0.82rem / 1.65 with site token colours, on-ink-2 line numbers, and the line that matters marked by a yolk number with a yolk disc in the gutter, scrolled into view. Focus rings turn yolk, selection inverts to yolk, the slab's focus is the beak ring inset; scrollbars are yolk on ink. Esc closes it.

### Inspector Tape (signature)
The dispatcher board docked as a tape: an ink ground with 16px top corners fixed to the bottom edge. Collapsed, a 3.6rem bar holds the yolk INSPECTOR label, the latest action (sequence and origin in on-ink-2, action type in sheet mono, its lamp flipping in over 0.7 beat), counts of dropped, vetoed and failed lamps, and an on-ink Expand pill. Open, it adds the board (3px yolk frame, 14px; sticky yolk header 2.4rem with wide labels; 2.5rem ink rows ruled with ink hairlines; sequence, time and origin in on-ink-2) beside a detail panel. **DispatchResult lamps:** yolk filled for Reduced, beak filled for Dropped and Failed, an on-ink-2 ring for Vetoed (the playground has no queued state); a failed lamp on the white sheet keeps its beak fill with ink text and ring. Each action name is a button underlined in yolk on hover; the selected row fills yolk with ink text and ink lamps. **Detail panel:** the action type in yolk mono, label-small headings (Payload, State changed), the payload in a 12px hairline-framed mono block, and the state diff as a list ruled with ink hairlines: path in on-ink-2, "was" value, "now" value in yolk.

### Shop
- **Control pills:** 2.6rem, 3px ink, Atkinson 700 1rem; hover fills sheet on yolk and yolk on the sheet; press and pressed (Inspector open) invert to ink with yolk text. A small step (2.25rem, 0.92rem) serves Dismiss and form actions; the on-ink variant is the console control.
- **Duck cells:** the shelf is a ruled specimen sheet, not a deck of cards: cells divided by 1.5px ink hairlines under a 3px rule, each holding the duck silhouette (4.8rem), the duck name, a blurb in on-sheet-2, 1.5px pill tags (colour first), then price in mono 700 tabular, stock in on-sheet-2 (ink bold when fewer than five), and an Add pill (2.3rem, drawn plus) that fills yolk on hover and inverts on press. Sold out disables Add to a dashed on-sheet-2 border. Under it, "N in your basket" with a drawn check, or a veto ring with the reason.
- **Duck silhouette:** authored SVG of the site's duck in the product's colour with an ink outline, ink wing lines and eye; the beak is ink (yolk on the black duck), never beak orange: the One Beak Rule applied to the product. Decorative, the name is the text beside it.
- **Filter chips:** 2.2rem pills, 3px ink border, Atkinson 700 0.88rem; each colour chip leads with a 0.85rem swatch dot in the product hue inside a 2px ink ring (the ring turns yolk when selected); hover fills yolk, selected and pressed invert to ink with yolk text. "In stock only" is the same chip as a toggle; Sort is a pill select with a drawn chevron that fills yolk on hover.
- **Search field:** a 3.4rem sheet pill with a 3px ink border and a drawn magnifier, 1.1rem text, on-sheet-2 placeholder; focus is a 3px ink outline at 3px offset around the pill. A mono status line under it reports superseded runs.
- **Basket lines and steppers:** a list under a 3px rule, rows ruled at 1.5px: name in Atkinson 700, a stepper (2rem round buttons with a 2px ink ring and drawn minus and plus, quantity in mono tabular between), line total in mono, and a borderless round remove button. Stepper and remove fill yolk on hover and invert on press. The total sits under the list in mono 700 1.25rem.
- **Pay (the beak action):** the page's one beak element at rest. A full-width 3.5rem beak pill with a 3px ink border, label in the action setting with the total ("Pay €25.00"). Hover fills yolk; press inverts to ink with sheet text and drops 2px; while paying it holds ink with yolk text and reads "Paying…" (it stays enabled so Exhaust can drop repeated clicks); an empty basket disables it to sheet with a dashed on-sheet-2 border and on-sheet-2 label.
- **Declined alert:** a 3px ink frame at 14px with no fill, a stamped label-small title behind a beak lamp, the reason, and a small Dismiss pill.
- **Order tracker:** the order id and total in mono over a list ruled at 1.5px; each step leads with a lamp (yolk done, ink current flipping in over 0.7 beat, empty ring pending); pending steps in on-sheet-2, the current step bold.
- **Fields:** 2.6rem sheet pills with a 3px ink border, Atkinson 400 1rem, labels in Atkinson 700 0.86rem above. Check boxes are 1.35rem 3px-ink squares at 6px that fill ink with a drawn yolk check.
- **"Nothing is for sale" disclosure:** a bold ink line (700, 0.9rem) directly under the lede in the sheet's head, stating that the ducks, stock and orders are demonstration data and Pay charges nothing. It is part of the first viewport, never a banner, toast or footnote.
- **Basket jump (phones):** an ink pill (2.8rem, yolk text, drawn basket) fixed bottom-right above the tape, reading "Basket, N ducks"; press flips to sheet. It scrolls to and focuses the basket.
- **Top bar badge:** PLAYGROUND in label-small on an ink pill beside the wordmark (1.5rem, 900, width 140%).

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
- **Do** dock the playground's panels to a viewport edge as ink grounds (tape at the bottom, drawer at the side, bottom sheet at 1100px) and keep the shop usable beside them.
- **Do** annotate in X-ray with three marks only: a 3px ink outline at 7px offset, a yolk tag pill that opens the code, and an ink render-count chip; reveal them in reading order inside one beat.
- **Do** show the app's own compiled source in the code drawer, captioned with its path, with the line that matters marked in yolk.
- **Do** give every action in the inspector its DispatchResult lamp: yolk Reduced, beak Dropped and Failed, an on-ink-2 ring Vetoed.
- **Do** draw products as the site's duck silhouette in the product's colour with an ink outline and an ink beak (yolk on a black duck).
- **Do** state in the shop's first viewport that nothing is for sale and Pay charges nothing.
- **Do** disable a control with a dashed on-sheet-2 border and on-sheet-2 text, not by fading it.

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
- **Don't** put a scrim, backdrop, blur or focus trap behind the code drawer or the inspector tape.
- **Don't** let a product hue leave the duck's drawing and its swatch dot, and don't draw a duck with a beak-orange beak; beak orange belongs to Pay and alarms.
- **Don't** show X-ray outlines, tags or render counts when X-ray is off, or use the tag pill as a decorative label.
- **Don't** set the shelf as filled cards; it is a ruled specimen sheet of hairline cells.
