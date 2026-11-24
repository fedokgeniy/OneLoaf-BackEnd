# CostTable

The cost breakdown of a recipe or batch (US-14): a line per ingredient, then electricity, packaging, loss, labor and the
total.

`.ol-table-wrap` > `table.ol-table.ol-table--stack`. Numbers right-aligned in tabular figures; total row
`tr.is-total` (bold, wheat, stronger rule); missing price `tr.is-missing` (warning colour + "price missing" text and an
"Add price" link). On compact screens (< 37.5em) each row becomes a two-line block: name on the first line, the cells with
their `data-label` below — no horizontal scrolling. Headers stay in the DOM for screen readers.
