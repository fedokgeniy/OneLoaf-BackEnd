# OneLoaf Design System

**OneLoaf** is a web application (Blazor, PWA) for home and small-bakery sourdough bakers: recipes with variants, a cost
and timeline calculator, production batches, a starter tracker, baking history and an AI assistant. This design system is
the single source for its look on every device — phone, tablet, desktop, 4K, foldables and watch-size screens — in a light
and a dark theme.

It grows out of the existing OneLoaf pages (public site and admin panel): the cream-and-terracotta palette, Cormorant
Garamond headings with DM Sans text, pill buttons, soft 16px cards and 44px touch targets. Where those pages failed
WCAG 2.1 AA the values were corrected and the change is noted next to the token.

## Principles

1. **The kitchen is the main device.** Floury hands, a phone on the counter, one hand free: primary actions sit in the
   thumb zone (bottom navigation, full-width buttons at the bottom of cards), targets are at least 44px
   (`--target-min`), stage actions are 48px (`--target-comfortable`).
2. **Numbers are the product.** Quantities, costs and times are set in DM Sans with tabular figures (`metric` style,
   `.ol-num`) so columns line up; every number shows its unit ("1 500 g", "11.22 EUR", "02:00–04:00").
3. **Warm, calm, readable.** Cream and espresso grounds, one terracotta accent, generous spacing. No gradients behind text,
   no decorative noise in working screens.
4. **Status is never colour alone.** Every status has a word and a dot or icon (`Planned`, `Running`, `Done`, `Overdue`).
5. **The same app everywhere.** One component set, three navigation patterns, one grid that rebuilds itself by window size
   (see *Layout and devices*).

## Colour

Two themes, `light` (default) and `dark`, switched by `data-theme` on `<html>` (see *Accessibility and display settings*).
Use semantic tokens, never raw hex values.

| Role | Token | Light | Dark | Use |
|---|---|---|---|---|
| Ground | `--bg` | #faf7f2 cream | #1a0f06 espresso | page background |
| Card | `--surface` | #fff9f4 | #241811 | cards, tables, panels |
| Band | `--surface-sunken` | #f5ede0 wheat | #120a04 | table headers, wells, skeletons |
| Overlay | `--surface-raised` | #ffffff | #2e2017 | dialogs, sheets, menus, bottom bar |
| Text | `--ink` / `--ink-muted` | #2c1d0e / #7a5c42 | #f5ede0 / #c9b29b | text and icons |
| Action | `--primary` / `--primary-strong` | #aa5a32 / #8f4726 | #e08a5e / #eba27a | buttons, links, active states |
| Selected | `--primary-tint` | #f6e3d8 | #3d2416 | selected nav, chips, AI plan card |
| Brand | `--brand-terra` | #c4683a | #c4683a | logo heart, large display headings, illustration |
| Focus | `--focus` | #2557b3 | #8db4ff | focus ring on every control |
| States | `--success` `--warning` `--danger` `--info` (+ `-tint`) | | | badges, alerts, stage states |

**Contrast rules (WCAG 2.1 AA):**

- Body text ≥ 4.5:1 on its ground in both themes; large text (≥ 24px, or 18.66px bold) and UI parts ≥ 3:1.
- `--brand-terra` #c4683a, the original accent, gives only 3.9:1 with white and 3.65:1 on cream. It is therefore
  **decorative and large-text only**. Buttons and links use `--primary` #aa5a32 (4.98:1 with white text, 4.66:1 on cream).
- `--primary` is never used as text on `--surface-sunken` (4.28:1) or `--primary-tint` (4.01:1): use `--primary-strong`.
- Control borders use `--border-control` (≥ 3:1); `--border-hairline` is decoration only.
- The original 11px uppercase labels and `rgba(196,104,58,.28)` input borders were raised to 12px and `--border-control`.

## Typography

| Style | Family | Size (rem) | Use |
|---|---|---|---|
| `display` | Cormorant Garamond 600 | 3.5 (2.5 on phones) | landing hero, empty states |
| `h1` | Cormorant Garamond 600 | 2.5 (2 on phones) | page title |
| `h2` | Cormorant Garamond 600 | 2 (1.625 on phones) | section title |
| `h3` | Cormorant Garamond 600 | 1.5 | card and dialog titles |
| `title` | DM Sans 600 | 1.125 | list titles, top bar |
| `body` / `body-strong` | DM Sans 400 / 600 | 1 | text, button labels |
| `small` | DM Sans 400 | 0.875 | secondary info, helper text |
| `label` | DM Sans 600, uppercase, +0.08em | 0.75 | overlines, form labels, table headers |
| `metric` | DM Sans 600, tabular | 1.75 | calculator figures |

All sizes are **rem**, so they follow the browser font size, browser zoom and the in-app display scale. Fonts load from
Google Fonts (`components/bundle.css` imports them); fallbacks are Georgia and the system UI font. Paragraph text never goes
below 1rem; line length is capped at `--reading-max` (42rem).

## Spacing, radius, elevation

- Spacing scale on a 4px base: `--space-1` 0.25rem … `--space-8` 4rem. Phones use `--space-4` page padding, desktop
  `--space-7`; gutters `--space-4` (phone) and `--space-5` (tablet+).
- Radii: `--radius-sm` 8px (icon buttons), `--radius-md` 10px (inputs, nav items), `--radius-lg` 16px (cards),
  `--radius-xl` 20px (dialogs), `--radius-pill` (buttons, chips, badges).
- Elevation: `--shadow-card` resting, `--shadow-raised` hover and bottom bar, `--shadow-modal` dialogs. Dark theme
  shadows are deeper; surfaces also get lighter as they rise (`bg` → `surface` → `surface-raised`).

## Iconography

- 31 line icons on a 24px grid, 1.75px stroke, round caps and joins (group **Icons**): navigation (home, recipes,
  calculator, batches, starter, history, assistant, library, settings), actions (plus, edit, trash, check, close,
  chevrons, play, check-circle, timer-plus, send, search, logout, menu), status (alert, info, bell) and display settings
  (sun, moon, monitor, text-size, user).
- In the app, icons are inline SVG with `stroke="currentColor"` and class `.ol-icon` (`--icon-md` 1.25rem; `--icon-sm`,
  `--icon-lg`), so they take the text colour and the theme. The files in Icons are drawn in `--ink` #2c1d0e for preview.
- Decorative icons get `aria-hidden="true"`; icon-only buttons get an `aria-label` ("Delete ingredient").
- No emoji in the interface.

## Logo

The OneLoaf wordmark is set in Cormorant Garamond 600 as “One L♥af”: the heart from the original site replaces the
second “o” (group **Logos**: `heart-terra.svg` on light grounds, `heart-blush.svg` on espresso). On phones the top bar
shows the wordmark at 1.375rem; on watch-size screens only the heart.

## Voice and content

- English UI, sentence case for buttons and titles ("Start stage", "Add price").
- Buttons say what happens: "Confirm plan", not "OK". Destructive actions name the object: "Delete recipe".
- Numbers always with units and in the user's time zone; ranges with an en dash: "14–16 h".
- The assistant is introduced as an AI ("I'm the OneLoaf assistant, an AI…") and advice on substitutions carries the
  disclaimer "Informational only — no guarantee of food safety or allergens."
- Errors explain and offer the next step: "Price for rye flour is missing. Add price".

## Components

Buttons · Text field · Selection controls · Segmented control · Tabs · Chip · Status badge · Recipe card · Stat tile ·
Cost table · Stage item · Navigation · Top bar · Dialog / bottom sheet · Alert / toast · Assistant chat · Empty state ·
Skeleton · Display settings. Each card shows its states: default, hover, focus, pressed/selected, disabled, error, loading
where it applies. Components are plain CSS classes (`.ol-*` in `components/bundle.css`) so they map 1:1 to Blazor
components in the frontend.

## Using it in the frontend

1. Copy `tokens.css` (generated from `tokens.json`) and `components/bundle.css` into `wwwroot/css/`.
2. Put `<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover, interactive-widget=resizes-content">`
   in the host page.
3. Set `data-theme` and `data-scale` on `<html>` from the user's display settings before first paint.
4. Build each Blazor component on the matching `.ol-*` classes; never hard-code colours or pixel font sizes.
