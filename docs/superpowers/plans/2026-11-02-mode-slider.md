# Sliding mode indicator

**Goal:** Restore a shared blue sliding indicator over two separate rounded tracks. Hide the indicator in the empty space between the groups. Keep the page content dissolving in place.

**Design:** Reuse the current radio navigation and selected-icon morphs. Draw two themed track shapes and clip one animated indicator to their inner rounded bounds. Animate its position and width from their current values with `Motion.QuartOut`. Use 380 ms for adjacent navigation and 460 ms across groups, paired with 400/440 ms content dissolves. Keep label/icon ink readable while the indicator travels. Recalculate geometry after layout/localization and honor reduced animation settings.

- [x] Add a regression check for shared geometry, hidden middle space, nonlinear movement, two travel durations and interrupted reversal. Run it against the previous implementation.
- [x] Replace individual selection faces with the shared indicator and two track shapes; retain native radio selection and icon morphs.
- [x] Coordinate content fade duration with the route, preserve existing run state behavior and stop rendering callbacks when the animation ends or the window closes.
- [x] Run the full WPF suite, inspect light/dark and English captures, publish, verify matching desktop/publish hashes and inspect the native application without sending a continuation.

**Files:** `src/MainWindow.xaml`, `src/MainWindow.xaml.cs`, `src/App.xaml`, `tests/DetectionFrequency.Tests/InlineModeChecks.cs`, `tests/DetectionFrequency.Tests/Program.cs`.

**Verification:** `dotnet run --project tests/DetectionFrequency.Tests -c Release --no-restore`. Backup and evidence: `_backups/20261102-mode-slider`.

**Results:** 19 checks passed, zero failures. Tests reproduce and verify the first-frame position jump and partially covered white text, check nonlinear deceleration, different travel durations, clipped middle space, width-preserving reversal and render-callback cleanup. Light/dark and English captures were inspected. Native desktop inspection confirmed the white tracks, hidden indicator in the gap, inline content dissolve and final alignment. Windows animation preferences are respected in code; the OS preference was not changed during verification. No real continuation was armed or sent.

**Published:** Desktop and canonical publish executable SHA256 `48673E8F0FF972B2DAD9FE05CA26A609E15F35F2F8A26B05AD3A678F3592CBD3`.
