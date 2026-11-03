# Grouped settings layout

**Goal:** Remove the redundant Settings page title and show two clear categories. General contains appearance, accent color and language in one card. Connection contains detection frequency in one card. Category names sit outside the cards with 18px bold text; existing 14px labels, aligned option columns, subtle internal separators and shared mode fades remain consistent.

- [x] Merge appearance, accent and language into one card and keep detection frequency in a separate card. Add localized external category labels and remove the redundant title.
- [x] Run the existing regression suite including layout bounds, inspect Chinese/English and light/dark settings, publish the combined settings and loop-hover update, then verify the native window without running continuation.

**Validation:** Existing `tests/DetectionFrequency.Tests` suite; rendered settings and loop tooltip; native hover and settings checks; matching desktop/publish hashes. Preserve saved preferences. Evidence: `_backups/20261102-settings-groups`; accompanying hover change: `_backups/20261102-loop-hover-help`.

**Results:** All 30 existing grouped checks passed with zero failures. Inspected light/dark settings and English copy, centered accent choices within the grouped row, and confirmed all frequency options and hints fit the fixed viewport. Release publish succeeded; both desktop and canonical publish executables match SHA256 `D2E0970BA49899ACE47FD085E994AEC49CA22F1EAEE7818460B499C73CB68A17`. Native verification confirmed the old page title is absent, General and Connection headings sit outside the two cards, and the loop explanation appears beside the pointer with no icon or click dialog. Left the grouped settings page open. Preferences remain system appearance/language, green accent, idle 1800 seconds and running 300 seconds. No continuation was armed or sent.
