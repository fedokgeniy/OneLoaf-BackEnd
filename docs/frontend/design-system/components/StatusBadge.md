# StatusBadge

Non-interactive label for the state of a batch, stage, starter or cost.

| Class | Meaning | Examples |
|---|---|---|
| `.ol-badge--planned` | info | Planned batch, upcoming stage |
| `.ol-badge--running` | warning (pulsing dot) | Stage in progress, feeding due soon, cost incomplete |
| `.ol-badge--done` | success | Stage done, batch done, cost complete |
| `.ol-badge--overdue` | danger | Feeding overdue, stage over its maximum time |
| `.ol-badge--neutral` | muted | Skipped, cancelled, archived |

Always a word plus a dot; colours from the `-tint` / base pairs (≥ 4.75:1).
