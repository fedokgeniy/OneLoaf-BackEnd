# Accessibility and display settings

Target: **WCAG 2.1 level AA** (the BA document aims at WCAG 2.2 AA for the main flows, which includes 2.1 AA).

## Checklist built into the system

| Requirement | How |
|---|---|
| Text contrast ≥ 4.5:1 (large ≥ 3:1) | Every text token pair checked in both themes; values in the token `usage` notes |
| UI component contrast ≥ 3:1 | `--border-control`, `--focus`, icons in `--ink-muted` or stronger |
| Touch targets ≥ 44 × 44px | `--target-min` 2.75rem on buttons, chips, nav items, icon buttons, checkboxes (via label), segmented options (container padding) and text links (`.ol-link`: inline-flex, min-height 44px) |
| Visible focus | 2px `--focus` outline with 2px offset on every interactive element (`:focus-visible`) |
| Not colour alone | Status badges have text and a dot; invalid fields get a thicker border, an icon and a message |
| Text resize to 200% | rem everywhere, no fixed heights on text containers, reflow without horizontal scrolling at 320 CSS px |
| Reduced motion | `prefers-reduced-motion: reduce` disables animations (skeleton shimmer, spinner, card lift) |
| Semantics | Real `<button>`, `<a>`, `<input>`, `<label>`; `aria-current="page"` in navigation; `aria-pressed` on chips and segmented options; `aria-invalid` + `aria-describedby` on fields; dialogs with `aria-modal` and focus trap |
| Language and AI disclosure | `lang` on `<html>`; the assistant identifies itself as AI (EU AI Act Art. 50) |

## User display settings (Settings → Display)

| Setting | Options | Implementation |
|---|---|---|
| Theme | Light · Dark · System (default) | `data-theme="light|dark"` on `<html>`; System follows `prefers-color-scheme` and updates live |
| Display scale | 90 % · 100 % · 125 % · 150 % (4 buttons that fit one row on a phone) | `data-scale` on `<html>` sets `--ol-user-scale`; `html { font-size: calc(100% * var(--ol-user-scale)) }` multiplies the browser's own size, so everything in rem (text, spacing, targets, icons) scales together |
| Reduce motion | follow system · on | adds `data-motion="reduce"` (same rules as the media query) |

The settings are stored in `localStorage` (and in the user profile when signed in) and applied by a small inline script
in `<head>` before the first paint, so there is no flash of the wrong theme.

## Testing

- Lighthouse accessibility ≥ 90 and axe-core with 0 critical violations on main pages (NFR-10).
- Manual pass at 320, 360, 768, 1024, 1440 and 2560 px, in both orientations, at browser zoom 200 %, with display scale 150 %
  and in both themes.
- Keyboard-only pass of the batch execution and assistant flows; screen reader pass (TalkBack / VoiceOver) of the same.
