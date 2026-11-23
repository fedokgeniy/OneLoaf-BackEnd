# StageItem

One stage of a running batch (US-23, US-24): planned and actual times, deviation and the actions.

`.ol-stage` with state class `.is-running` | `.is-done` | `.is-skipped` (default = upcoming). Marker shows the stage
number (upcoming), a pulsing ring (running) or a check (done). Times in a `<dl>` with tabular figures; deviation
("+45 min over max") in `--warning`. Actions: upcoming → **Start**; running → **Complete** and **+30 min**; done → none.

Actions always form one full-width row at the bottom of the card on every screen size; the buttons share the width
(48px, `.ol-btn--lg`, in the thumb zone on phones). Never more than one primary button per stage.
