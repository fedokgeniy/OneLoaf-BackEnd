# StatTile

One calculated figure with label and hint: dough weight, kneading cycles, starter amount, total cost, ready window.

`.ol-stats` (auto-fit, min 10rem) > `.ol-stat` > `.ol-stat__label` · `.ol-stat__value` (`metric` style, tabular
figures, wraps instead of overflowing) · `.ol-stat__hint`. Values come only from the backend calculation; while loading
show a skeleton bar, never a fake number.
