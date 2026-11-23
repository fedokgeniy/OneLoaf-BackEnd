# TextField

Labelled single-line input, select and textarea for every form (recipes, prices, feedings, kitchen settings).

**Anatomy:** `.ol-field` > `.ol-field__label` (uppercase label, always visible — never placeholder-only) >
`.ol-field__control` > `.ol-input` | `.ol-select` | `.ol-textarea` > optional `.ol-field__suffix` (unit: g, %, kWh,
EUR) > `.ol-field__help` or `.ol-field__error`.

**States:** default border `--border-control` (≥ 3:1); hover `--ink-muted`; focus `--focus` border + ring; error
`aria-invalid="true"` → 2px `--danger` border, alert icon and message linked with `aria-describedby`; disabled → wheat
fill, dashed border.

**Rules:** height ≥ 44px, text 1rem (prevents iOS zoom on focus); numeric fields use `inputmode="decimal"` and a unit
suffix; validation messages say how to fix ("Enter a weight between 1 and 50 000 g"). On phones forms are one column;
from 37.5em two columns (`.ol-grid` spans).
