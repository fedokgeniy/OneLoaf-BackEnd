# Adaptive UI requirements — compliance

Mapping of the diploma "Adaptive UI Requirements (Web/Mobile)" to this design system and the frontend. Status: ☑ defined
in the system · ◐ defined here, verified in the frontend implementation.

## Minimum requirements

| Requirement | Where it is met | Status |
|---|---|---|
| 3 device types (phone, tablet, desktop); phone aspect ratios 16:9 … 21:9; portrait and landscape | Size classes and landscape rule in *Layout and devices*; mockups in 3 variants per screen | ☑ |
| No overlapping when resolution / orientation changes | Grid, auto-fill cards, wrapping toolbars, stacked tables on phones | ◐ |
| Relative units | rem for type, spacing, radii, sizes; em breakpoints; % / fr for layout | ☑ |
| Works with browser zoom and system font settings | rem + em breakpoints; reflow at 320 CSS px | ◐ |
| Fonts, icons, images scale without distortion | SVG icons; `object-fit: cover` + fixed aspect ratios for photos; `srcset` | ☑ |
| Touch targets ≥ 44px | `--target-min` on every interactive component | ☑ |
| Contrast WCAG 2.1 AA (≥ 4.5:1 text) | Checked token pairs in both themes; brand terra restricted to large text and decoration | ☑ |
| Consistent palette and typography on all devices | One token set and one type scale for all size classes | ☑ |
| Responsive grid | `.ol-grid` 4/8/12, `.ol-cards`, `.ol-split` | ☑ |
| Blocks scale with width | Fluid containers, `minmax()`, `clamp()` padding | ☑ |
| Main navigation always available | Bottom bar / rail / sidebar, one of them always visible; watch: menu button | ☑ |

## Maximum requirements

| Requirement | Where it is met | Status |
|---|---|---|
| Any screen type: 4K, ultrawide, foldable, smartwatch | Ultra class (`--content-max-ultra`, four panes), aspect-ratio rules, viewport segments, watch class | ☑ / ◐ |
| Automatic device detection (density, orientation, aspect ratio) | Media features table in *Layout and devices*; `DeviceContext` in Blazor | ◐ |
| Design System | This system: tokens, type, components with states, assets, guidelines | ☑ |
| Light / dark theme | Two themes in `tokens.json`; theme switch in Display settings | ☑ |
| SVG icons | Icons group (31 SVG) | ☑ |
| User display settings (scale, colour scheme) | Display settings component: theme + 4-step scale | ☑ |
| Layout rebuilds on resize | Size classes swap navigation pattern and pane count live (CSS + `matchMedia`) | ◐ |
| One-hand use, platform patterns (bottom nav / tab bar) | Bottom bar, bottom sheets, actions in the thumb zone | ☑ |
| UI not covered by the keyboard | `interactive-widget=resizes-content`, `dvh`, sticky composer, `visualViewport` fallback | ◐ |

## Document requirements (the mockup deliverable)

| Requirement | Where |
|---|---|
| Clear labels on pages and components | Each artboard and component card is named (e.g. "Batch execution — Phone") |
| 3 variants for different devices | Every key screen on the Design canvas as Phone 390×844, Tablet 834×1194, Desktop 1440×900 |
| Library of reusable components | Components section of this system |
| States of interactive elements | Each component card shows default, hover, focus, pressed/selected, disabled, error, loading |
| Format | Exported as PDF from the design canvas |
