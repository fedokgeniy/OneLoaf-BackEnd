# Navigation

The main navigation is always on screen; its form follows the window size class.

| Size class | Pattern | Class |
|---|---|---|
| Compact (< 37.5em), phone portrait | Bottom bar, 5 destinations, icon + label, `env(safe-area-inset-bottom)` | `.ol-nav--bottom` + `.ol-show-compact` |
| Medium (37.5–64em) and phone landscape | Rail, 5–7 destinations, icon over label | `.ol-nav--rail` + `.ol-show-medium` |
| Expanded (≥ 64em) | Sidebar on espresso with wordmark, all destinations, sign-out at the bottom | `.ol-nav--sidebar` + `.ol-show-expanded` |
| Watch (< 17.5em) | Bottom bar icons only (labels visually hidden), or a menu button | — |

Current page: `aria-current="page"` → `--primary-tint` background, `--primary-strong` text, weight 600 (on espresso:
`--espresso-selected` / `--espresso-accent`). Hover: `--surface-sunken`. Every item ≥ 44 × 44px. The app renders all three
inside `.ol-shell`; CSS shows the one that fits and rebuilds live when the window is resized or rotated.
