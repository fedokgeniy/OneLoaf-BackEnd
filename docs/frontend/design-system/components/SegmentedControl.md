# SegmentedControl

A row of 2–5 mutually exclusive options; used for theme (Light · Dark · System), display scale and compact view
switches (Plan · Actual).

`.ol-segmented` (role `radiogroup` or a group of toggle buttons) > `.ol-segmented__opt` with `aria-checked` /
`aria-pressed`. Selected: `--surface-raised` pill with `--shadow-card`, `--ink` text, weight 600; unselected `--ink-muted`.
The container adds 0.25rem padding so each option keeps a 44px target. Scrolls horizontally on watch-size screens instead of
wrapping.

`.ol-segmented--fill` stretches the control to the full width with equal option widths: use it on phones and in settings cards, so every option is a visible button (keep it to 2–4 options).
