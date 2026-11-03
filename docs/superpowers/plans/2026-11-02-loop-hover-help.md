# Loop send hover explanation

**Goal:** Replace the circled exclamation button and click dialog with the same formal explanation in a rounded tooltip beside the pointer when hovering the Loop send checkbox or label.

- [x] Remove the icon, click handler and unused icon-label strings; attach a MousePoint tooltip with wrapped localized copy and the shared rounded theme style. Keep hover help available while the running checkbox is disabled.
- [x] Adapt the existing lifecycle/layout checks to the revised hover interaction without changing sending behavior.
- [x] Run existing regressions, inspect the rendered tooltip, publish and update the desktop release, then check native hover behavior while idle.

**Validation:** Existing `tests/DetectionFrequency.Tests` suite, rendered tooltip checks in both appearances/languages, native hover verification and matching published/desktop hashes. Evidence and backups: `_backups/20261102-loop-hover-help`.

**Results:** All 30 checks passed, including localized wrapped hover help, rounded appearance and availability on the locked checkbox. Rendered tooltip checks attach the actual tooltip to its placement target before capturing so live appearance resources resolve correctly. Delivered with the grouped-settings update, SHA256 `D2E0970BA49899ACE47FD085E994AEC49CA22F1EAEE7818460B499C73CB68A17`. Native pointer hover over the Loop send label opened the formal explanation without toggling the unchecked option or opening a dialog. No actual continuation was armed or sent.
