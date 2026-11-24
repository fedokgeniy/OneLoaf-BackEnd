# Alert

Inline messages about the current screen, and a toast for short confirmations.

`.ol-alert--info | --success | --warning | --danger`: tint background, 1px border and icon in the state colour, text in
`--ink` (contrast ≥ 13:1). Use: info → AI disclosure, planned items; success → saved; warning → incomplete cost, missing
prices (with an action link); danger → failed request, assistant unavailable (with "Use the manual form").
Live region: `role="status"` (polite) or `role="alert"` for errors.

`.ol-toast`: ink pill above the bottom bar (phones) or bottom-centre (tablet+), max 30rem, auto-dismiss after 5 s, with an
optional action ("Undo"); never the only place an error is shown.
