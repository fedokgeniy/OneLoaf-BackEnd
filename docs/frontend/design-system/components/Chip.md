# Chip

Toggleable filter pills (history filters, library categories, flour type), from the original site's category pills.

`.ol-chip` (`<button aria-pressed>`), 44px high, 2px `--border-control` outline. Hover: `--primary` border,
`--primary-strong` text. Selected (`aria-pressed="true"`): filled `--primary`, `--ink-on-primary` text. Disabled:
dashed border. A row of chips wraps on narrow screens (`flex-wrap`), never scrolls off-screen without a hint.
