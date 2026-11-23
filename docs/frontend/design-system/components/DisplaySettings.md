# DisplaySettings

Settings → Display: the user's own colour scheme and scale (adaptive UI maximum requirement).

- **Theme:** SegmentedControl Light · Dark · System → `data-theme` on `<html>` (System follows `prefers-color-scheme`).
- **Display scale:** 90 / 100 / 125 / 150 % (four equal buttons, one row on phones) → `data-scale` on `<html>`; everything in rem scales, layouts reflow.
- **Reduce motion:** switch → `data-motion="reduce"`.
- A live sample card under the controls shows the effect immediately. Values persist in `localStorage` and the profile and
  are applied before first paint.

The preview below is interactive: try the controls.
