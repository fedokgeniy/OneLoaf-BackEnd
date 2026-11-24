# Layout and devices

How OneLoaf adapts to screen size, orientation, pixel density and input type. All breakpoints are in **em** so they
respond to browser zoom and the user's text size, not only to physical pixels.

## Window size classes

| Class | Width | Typical devices | Navigation | Grid | Page padding |
|---|---|---|---|---|---|
| Watch | < 17.5em (280px) | smartwatch, folded cover screen | menu button → full-screen list; icons only | 1 column | `--space-2` |
| Compact | 17.5–37.49em (280–599px) | phones portrait (16:9 … 21:9) | **bottom bar**, 5 items, labels | 4 columns | `--space-4` |
| Medium | 37.5–63.99em (600–1023px) | tablet portrait, unfolded foldable, phone landscape | **navigation rail** | 8 columns | `--space-5` |
| Expanded | 64–89.99em (1024–1439px) | tablet landscape, laptop | **sidebar** (espresso) | 12 columns | `--space-7` |
| Large | 90–159.99em (1440–2559px) | desktop monitor | sidebar + side panel open | 12 columns, max `--content-max` 72rem | `--space-7` |
| Ultra | ≥ 160em (≥2560px) | 4K, ultrawide 21:9 / 32:9 | sidebar | four panes (list · recipe · calculator · plan), max `--content-max-ultra` 140rem | `--space-7` |

**Phone landscape** (`orientation: landscape` and height < 30em) switches from the bottom bar to the rail, so the bar
does not take a third of a short screen.

## The grid

- `.ol-container` centres content with fluid side padding `clamp(1rem, 4vw, 3rem)`.
- `.ol-grid` is a 4 / 8 / 12-column grid (`--ol-cols`), gaps `--space-4` / `--space-5`.
- `.ol-cards` is an auto-fill card grid: `repeat(auto-fill, minmax(min(100%, 18rem), 1fr))` — 1 card per row on phones,
  2–3 on tablets, 3–5 on desktop, more on 4K, never overlapping.
- `.ol-split` is the list–detail layout: stacked on phones, two panes from 64em (recipe list + recipe), four panes from
  160em (list + recipe + calculator + batch plan). On a dual-screen foldable (`horizontal-viewport-segments: 2`) each pane takes one
  screen segment so nothing falls into the hinge.

## How each key screen rebuilds

| Screen | Compact (phone) | Medium (tablet) | Expanded / Large (desktop) |
|---|---|---|---|
| Recipes | card list, search in top bar, FAB-style "New recipe" button above the bottom bar | 2-column cards, rail | list + detail split, filters in a side column |
| Recipe / calculator | tabs: Ingredients · Stages · Cost · Timeline; cost as stacked list | tabs, cost as table | ingredients and stages left, calculator panel right |
| Batch execution | one stage card per row, Start / Complete full-width at the bottom of the card | stage cards with actions on the right | timeline left, current stage and log right |
| Starters | list with "Log feeding" bottom sheet | grid of starter cards | grid + feeding log side panel |
| Assistant | full-height chat, composer pinned above keyboard | chat centred at `--reading-max` | chat + plan preview panel |
| Settings | single column of groups | two columns | two columns, max `--reading-max` per column |

## Device detection (automatic)

Detection is done with CSS media features first (no user-agent sniffing), and a small script only where CSS cannot act:

| What | How |
|---|---|
| Width / size class | `@media (min-width: 37.5em / 64em / 90em / 160em)`; in Blazor `window.matchMedia` listeners for layout logic |
| Orientation | `@media (orientation: portrait / landscape)`; `screen.orientation.type` for logs |
| Aspect ratio | `@media (min-aspect-ratio: 21/9)` (ultrawide: content capped, extra width to panes); `(max-aspect-ratio: 9/19)` tall phones: bottom bar + safe areas |
| Pixel density | `@media (min-resolution: 2dppx)`; raster images via `srcset`/`sizes` (1×, 2×, 3×); icons are SVG so they are sharp at any density |
| Input type | `@media (pointer: coarse)` → larger targets kept, no hover-only actions; `(hover: hover)` → hover states and tooltips |
| Foldables | `@media (horizontal-viewport-segments: 2)` and `env(viewport-segment-*)` |
| Notches, home indicator | `viewport-fit=cover` + `env(safe-area-inset-*)` on top bar, bottom bar, sheets, composer |
| Theme and motion | `prefers-color-scheme`, `prefers-reduced-motion`, `prefers-contrast` |

The frontend exposes the detected values in a `DeviceContext` (size class, orientation, density, pointer) so components can
choose, for example, a bottom sheet (compact) or a centred dialog (medium+).

## Keyboard never covers the UI

- Viewport meta `interactive-widget=resizes-content`: the layout viewport shrinks when the on-screen keyboard opens.
- Full-height screens use `100dvh`, not `100vh`.
- The chat composer and dialog footers are `position: sticky; bottom: 0` inside a scroll container, so they ride above the
  keyboard; focused fields are scrolled into view (`scrollIntoView({block: "center"})`) with a `visualViewport` resize
  listener as fallback for browsers without `interactive-widget`.
- The bottom navigation hides while a text field has focus on compact screens.

## One-hand use and platform patterns

- Primary actions in the lower third: bottom bar, full-width buttons at the end of cards, bottom sheets instead of
  centred dialogs on phones, "Confirm plan" at the bottom of the AI plan card.
- Bottom bar follows the Android navigation bar / iOS tab bar pattern: 3–5 destinations, icon + label, selected item
  marked by tint and weight, not colour only. Destinations: Home, Recipes, Batches, Starters, Assistant; Library, History and
  Settings are reached from Home and the profile menu.
- Swipe-down to close a bottom sheet (handle shown); the close button stays for keyboard and screen-reader users.
