# StageItem

One stage of a running batch (US-23, US-24): planned and actual times, deviation and the actions.

`.ol-stage` with state class `.is-running` | `.is-done` | `.is-skipped` (default = upcoming). Marker shows the stage
number (upcoming), a pulsing ring (running) or a check (done). Times in a `<dl>` with tabular figures; deviation
("+45 min over max") in `--warning`. Actions: upcoming → **Start**; running → **Complete**; both with **Adjust** beside them; done → none.

The two buttons always form one full-width row at the bottom of the card on every screen size and share the width
(48px, `.ol-btn--lg`, in the thumb zone on phones): the main action on the left, **Adjust** on the right. Never more
than one primary button per stage. Under the row a slider drawn as a line and a circle (over a range control, which keeps keyboard and touch; 5-minute steps, marks every hour)
chooses by how much **Adjust** changes the time of the stage: more time for a running stage, shorter or longer for one
that has not started. The chosen value is shown as text next to the label ("+30 min"), not by the thumb alone.
