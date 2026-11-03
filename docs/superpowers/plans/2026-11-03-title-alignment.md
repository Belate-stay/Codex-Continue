# Title optical alignment

**Goal:** Align the visible Codex-Continue title glyphs with the adjacent vector logo on a shared horizontal centerline.

**Diagnosis:** In the existing WPF fixture capture, the logo ink bounds center is y=23 and the title glyph bounds center is y=21.5. The custom title font's visible ink sits above its layout center. Increase its top margin from 3 to 7 DIP, producing a 2 DIP downward optical correction in the vertically centered row. The logo slot and horizontal spacing stay fixed.

- [x] Apply the single title-margin correction.
- [x] Run existing regressions, inspect the resulting glyph alignment in light/dark captures and the native window, publish and update the idle desktop application while preserving preferences.

**Verification:** Existing `tests/DetectionFrequency.Tests` suite, visible-ink bounds in the generated fixture and native UI inspection. Evidence and backups: `_backups/20261103-title-alignment`.

**Results:** All 31 existing grouped checks passed with zero failures. The visible logo center stayed at y=23; title ink moved from y=21.5 to y=23.5, reducing the visible center difference to 0.5px on the pixel grid. Reviewed light/dark captures and confirmed the optical alignment in the native desktop window. Release publish and desktop update succeeded; canonical publish and desktop SHA256 both match `F050ACD44DD481D5162B470B201AB146EE6B129BA5B014C62FA2CC17FBF8C05D`. Saved system appearance/language, black accent and detection frequencies were preserved. The app remains idle; no continuation was armed or sent.
