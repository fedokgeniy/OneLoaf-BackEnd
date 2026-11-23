# Dialog

Modal for short tasks and confirmations: log a feeding, add a price, confirm deletion.

`.ol-scrim` (`--scrim`, blur) > `.ol-dialog` (`role="dialog"`, `aria-modal="true"`, `aria-labelledby` the title) >
`__handle` · `__head` (title + close button) · `__body` (scrolls) · `__foot` (actions; primary last on desktop, first and
full-width on phones).

Compact: a **bottom sheet** — full width, top radius `--radius-xl`, handle, swipe-down to close, actions in the thumb
zone; footer stays above the keyboard. Medium+: centred dialog, max 36rem, `--shadow-modal`. Focus moves into the dialog,
is trapped, Esc closes, focus returns to the trigger.
