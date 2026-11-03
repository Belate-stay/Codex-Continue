# Vector title logo

**Goal:** Replace the blurry ICO title image with the same OpenAI Blossom artwork rendered as vector geometry. Retain the existing 20 DIP image slot, alignment and visibility lifecycle, with neutral ink matching light/dark appearance.

- [x] Obtain the SVG from the official OpenAI Agents SDK repository, retain the original asset/provenance, and use its nonzero path in a native WPF DrawingImage without adding an SVG rendering dependency.
- [x] Run existing regressions, inspect vector rendering at multiple sizes and in both appearances, publish and update the idle desktop application. Verify the native title remains aligned and preserves saved preferences.

**Files:** `src/MainWindow.xaml`, `src/CodexContinue.csproj`, `src/Assets/OpenAI.svg`, `src/Assets/README.md`.

**Validation:** Existing `tests/DetectionFrequency.Tests` suite, rendered vector preview, native window check and matching desktop/publish hashes. Version remains Beta 0.9.0. Evidence and backups: `_backups/20261103-title-vector-logo`.

**Results:** All 31 existing grouped regression checks passed with zero failures. Rendered the 20 DIP vector at 100%, 150%, 200% and 300% DPI in light and dark palettes; reviewed the rendered title in the actual WPF theme fixtures. Release publish succeeded and both desktop and canonical publish executables match SHA256 `8A6C33DDD75285C8D97C9B7023244317189F6DA20D8A8850A3343B8AD5F180D2`. Native verification confirmed the vector title logo is visible, cleanly aligned beside the unchanged title text, and the application remains idle. Saved system appearance/language, black accent, idle 1800 seconds and running 300 seconds remain intact. No continuation was armed or sent.
