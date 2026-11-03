# Inline settings and log modes

**Goal:** Keep the title and mode bar visible while Auto, Manual, Settings and Log replace the content below them. Use blue selected cards and a quiet opacity transition, with no sliding, scrim or moving card outline.

**Approach:** Keep `RunMode` independent from the visible view so opening Settings or Log does not stop an armed continuation. Keep the existing controls alive in one content host. Fade the old content out for 160 ms, change visibility at zero opacity, then fade the new content in for 280 ms. Replace interrupted opacity animations from their current value and reject stale completion callbacks. Honor Windows reduced animation settings.

- [x] Add regression checks for the persistent header, one selected mode, inline settings/log geometry, fade timing and rapid switching. Run them against the current build and record the expected failures.
- [x] Move the existing settings and log content below the mode bar. Replace the overlay/back button/bottom sheet with four radio modes using the same blue selection face. Bind icon morphing to selection instead of hover.
- [x] Preserve prompt, explicit conversation selection, attachments, manual send time, armed state and detection schedule while inspecting settings/log. Keep the existing stop behavior when changing the actual run mode.
- [x] Run the full WPF suite, inspect both themes and English layout, then publish and verify the desktop executable. Use isolated WPF fixtures for fake data; do not trigger real continuation sending during verification.

**Files:** `src/MainWindow.xaml`, `src/MainWindow.xaml.cs`, `src/App.xaml`, `src/Themes/Light.xaml`, `src/Themes/Dark.xaml`, `src/MorphEngine.cs`, `src/ThemeManager.cs`, `tests/DetectionFrequency.Tests/InlineModeChecks.cs`, `tests/DetectionFrequency.Tests/Program.cs`. Remove only the icon-button style and horizontal page-transition helpers made unused by this change.

**Validation:** `dotnet run --project tests/DetectionFrequency.Tests -c Release --no-restore`; native desktop UI inspection after publishing. Backups and verification output: `_backups/20261102-inline-modes`.

**Results:** 18 checks passed, zero failures. Regression captures cover all four modes in both themes, English settings, outgoing/incoming opacity and interrupted navigation. Native desktop verification confirmed the persistent title/navigation, inline settings and log, selection morphs, keyboard focus and Space activation in both themes. Restored the user's light theme. No real Run action or continuation send was triggered. Existing MorphEngine nullable/unused-local compiler warnings remain outside this change.

**Published:** Desktop and canonical publish executable SHA256 `1B68E50A04B526ECE548A77B93A45193880D78544A0581F6D8424A030853F289`.
