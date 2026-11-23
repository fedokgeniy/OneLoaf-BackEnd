# RecipeCard

A recipe in the workspace list and the public library; the whole card is one link (`<a class="ol-card ol-card--interactive">`).

**Anatomy:** media 3:2 on every screen size (`.ol-card__media`; photo with `object-fit: cover`, or a wheat placeholder) · overline (flour,
"Variant of …") · title in Cormorant 1.5rem · meta (yield, total time, last cost) · footer with a status badge or a
secondary action.

**States:** rest `--shadow-card`; hover lift −4px + `--shadow-raised` (only on `hover: hover` devices); focus ring on
the link; loading → Skeleton card. Laid out with `.ol-cards` (auto-fill, min 18rem).
