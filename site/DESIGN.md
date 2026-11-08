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
rounded:
  focus: "4px"
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
---

# Design System: Ducky

## Overview

**Creative North Star: "The Rubber Duck on the Desk"**

The site is the conversation a developer has with the rubber duck beside their keyboard. The duck asks, on a field of drenched vinyl yellow; Ducky answers on a white answer sheet, in black ink and compiled code. Every surface is one of three grounds (yolk, ink, sheet), every stroke is ink, and a single beak orange marks the thing to press. Nothing is tinted, graded or lit: depth comes from swapping grounds and from heavy ruled lines, the way a moulded toy or a printed form gets its structure.

Density is generous at the section level (sections breathe at 4 to 7rem) and tight inside instruments: the dispatcher board is a ruled grid of fixed cells, the API list and package stack are ruled ledgers, and proof is a column of test names with check lamps. Display type is Anybody pushed wide and black, stamped like a toy's moulded mark; body is Atkinson Hyperlegible Next; JetBrains Mono appears wherever the reader is looking at something they would type or run.

Motion has one clock. A single beat (420ms, expo-out) drives the duck's squash-and-stretch quack, the speech bubble's wipe, the answer sheet's rise, the split-flap letters and lamps on the board, and the dispatcher's one-action-per-tick drain. Controls invert ink on press rather than glowing or lifting.

**Key Characteristics:**
- Three grounds (yolk, ink, sheet) and one action accent (beak); secondary text hues are darker or lighter shades of the ground they sit on, never decorative tints.
- 3px ink rules for structure, 1.5px hairlines inside tables and ledgers.
- Wide, black Anybody display; Atkinson body; JetBrains Mono for code, API names, test names and the board.
- Pills for every control, 14 to 16px corners for panels and code, a 22px speech bubble with a drawn tail.
- One motion beat for the whole page; press = ink inversion.
- Flat. No shadows, no gradients, no glows.

## Colors

A strict three-ink set on saturated toy-vinyl yellow, with beak orange as the only accent.

### Primary
- **Duck-Vinyl Yolk** (yolk): the page ground. Owns the first viewport, the tour, the platform section and the close; it is also the "lit" color on dark grounds (board headers, lamps for reduced actions, headings on ink sections, focus rings on ink) and the keyword color in code.

### Secondary
- **Beak Orange** (beak): the single action accent. The copy segment of the install command, the text caret, the focus ring on code blocks. In the dispatcher board it is also the alarm state: the dropped-action lamp and the "stale run" note. Never a section ground, never body text on yolk.

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

**The One Beak Rule.** Beak marks the thing to press, or a dropped result. If two beak elements compete in one viewport, one of them is wrong.

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
- **Label** (900, 0.78rem, 0.08em tracking, uppercase, width 150%): column headers of the board and matrix only.
- **Code** (400 to 700, 0.86 to 1.5rem, 1.65 in blocks, ligatures off): code blocks, install command, API and package names, test names, board cells, numeric readouts (tabular numerals).

### Named Rules
**The Stamped Mark Rule.** Headings are Anybody at 820 to 900 weight and at least 112% width, with negative tracking. A narrow or light display setting is off-system.

**The Typed Things Rule.** If the reader could type it, run it or find it in a test report, it is set in JetBrains Mono; prose is never set in mono.

## Layout

Content sits in a 1240px wrap with a fluid gutter (clamp(1.25rem, 4vw, 3rem)); section grounds bleed full width behind it. Sections pad vertically at clamp(4rem, 9vw, 7rem) (the close goes to 8rem, the migration band to 6rem), and blocks inside a section start 2.4rem below their heading.

Grids are asymmetric two-column splits on a 12-part feel: the hero runs 7fr / 5fr (copy, then duck) with the question chips spanning the bottom edge above a 3px rule; the answer sheet runs 5fr text / 7fr code with proof stacked under the code; platform and receipts use 5fr / 7fr; the dispatcher console runs 4fr controls and state / 8fr board. Ledgers (API list, package stack, receipts) are two-column definition grids with ruled rows.

Breakpoints: at 960px every split collapses to one column and the duck drops below the copy, right-aligned; at 640px the bar keeps only brand and GitHub, the install command goes full width, the board hides its origin column and lets rows grow, and the render-mode matrix becomes one ruled block per host with inline labels.

## Elevation & Depth

Flat. There are no shadows anywhere. Depth is conveyed by changing ground (yolk to sheet to ink), by 3px ink rules and borders, and by inversion on press. The only lift is a 2px downward translate on the pressed install command, which reads as a key going down, not as elevation.

### Named Rules
**The Printed Object Rule.** No box-shadow, no gradient, no glow, no blur at rest. Separation is a rule, a border or a ground change.

## Shapes

Two families. Controls are full pills (999px) with a 3px ink border: question chips, tabs, console controls, the GitHub link, migration links; smaller pill badges (version chip, INV links, test-kind tags) use 2px or 1.5px borders. Containers are softly rounded rectangles: 14px for the install command, the state panel and the board frame; 16px for code blocks and the matrix frame; 8px for inline API name chips. The speech bubble is the one soft outlier at 22px, with a two-layer triangular tail (ink outline, sheet fill).

Round marks are lamps: filled or outlined circles with a 2px border (board result lamps, matrix "yes" dots, the yolk check discs beside test names).

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

### Don't:
- **Don't** add shadows, gradients, glows or translucent overlays; depth is ground, rule and inversion.
- **Don't** introduce tints of yolk or ink as fills; secondary hues exist only as text on their own ground.
- **Don't** put beak on more than one element per viewport outside the dispatcher board.
- **Don't** use cards with fills and corners as a layout device; use ruled ledgers and frames.
- **Don't** set prose or headings in mono, or code in the display face.
- **Don't** give a section heading a small label above it; headings stand alone.
