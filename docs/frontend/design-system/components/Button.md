# Button

Pill buttons for every action; the primary button is the one main action of a screen or card.

**Variants:** `.ol-btn--primary` (filled `--primary`, text `--ink-on-primary`, 4.98:1), `.ol-btn--secondary` (outline
`--primary`, text `--ink`), `.ol-btn--ghost` (text `--primary`), `.ol-btn--danger` (filled `--danger`). Icon-only:
`.ol-icon-btn` (44 × 44, needs `aria-label`).

**Sizes:** default `--target-min` (44px); `.ol-btn--lg` `--target-comfortable` (48px) for stage actions and plan
confirmation on phones; `.ol-btn--block` full width (phones, card footers).

**States:** hover → `--primary-strong`; pressed → `--primary-strong` + 1px shift; focus → 2px `--focus` ring;
disabled → `--surface-sunken` with `--ink-muted` text and `disabled` attribute; loading → `.is-loading` spinner,
label kept for screen readers via `aria-busy="true"`.

**Rules:** one primary per view; destructive actions name the object ("Delete recipe") and ask for confirmation in a
dialog; icon + label for important actions, icon-only only in dense rows (table actions) with a tooltip on hover devices.
