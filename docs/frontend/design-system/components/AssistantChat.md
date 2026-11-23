# AssistantChat

The AI assistant conversation (US-34–US-38): messages, a proposed batch plan to confirm, typing indicator and composer.

- `.ol-msg--user` (right, `--primary-tint`) and `.ol-msg--assistant` (left, `--surface`), max 36rem, inside `.ol-chat`
  (max `--reading-max`). Assistant messages carry a meta line "OneLoaf assistant · AI" and, for advice, the
  `.ol-msg__disclaimer`.
- `.ol-plan`: the pending action card — title, the numbers from the tools (amounts, cost, ready window, starter) in a
  `<dl>` with tabular figures, and **Confirm plan** / **Change** buttons (full width on phones). Nothing is saved before
  Confirm.
- `.ol-typing`: three pulsing dots with `aria-label="Assistant is typing"`.
- `.ol-composer`: sticky at the bottom, auto-growing textarea (max 9rem) and a 44px send button; stays above the
  on-screen keyboard (`interactive-widget=resizes-content`, `100dvh`).
