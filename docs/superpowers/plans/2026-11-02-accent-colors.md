# Accent color presets

**Goal:** Add Blue, Black, Deep green, Light gray and Pink presets, defaulting to Blue. Apply the chosen accent to buttons, the mode indicator and selected settings cards. Preserve quota numbers/meter, semantic status ink and log highlights. Persist the choice and transition smoothly between presets.

**Design:** Keep appearance mode and accent choice independent. Reuse theme brushes and animate mutable interactive brushes over 320 ms from their currently displayed color. Provide dark/light palette variants, readable contrasting button ink and selection outlines. Use tinted secondary buttons, solid selected cards, and neutral disabled controls. Keep semantic/log brushes separate from the changing accent. Reduced animation settings use immediate values.

- [x] Add regression checks for default/legacy/invalid/persisted presets, five exact UI choices, changing button/mode/settings colors, unchanged quota/semantic/log colors, readable selected cards, interrupted transitions and theme/localization layout. Verify missing-feature failures.
- [x] Implement validated persistence, animated palettes, the preset row and localization. Update interactive styles and selected settings feedback while retaining special ink.
- [x] Run the full regression suite, inspect rendered light/dark/English settings, publish and update the desktop executable. Verify native operation without arming or sending a real continuation.

**Files:** `src/AppSettings.cs`, `src/ThemeManager.cs`, `src/App.xaml`, `src/MainWindow.xaml`, `src/MainWindow.xaml.cs`, `src/Themes/Light.xaml`, `src/Themes/Dark.xaml`, `src/Loc.cs`, `tests/DetectionFrequency.Tests/AccentColorChecks.cs`, `tests/DetectionFrequency.Tests/Program.cs`, `tests/DetectionFrequency.Tests/InlineModeChecks.cs`.

**Validation:** `dotnet run --project tests/DetectionFrequency.Tests -c Release --no-restore`. Evidence and backups: `_backups/20261102-accent-colors`.

**Results:** All 25 grouped checks passed with zero failures. Inspected rendered light, dark and English settings; verified all five palettes, selected-card contrast, interrupted color transitions and unchanged quota/semantic/log ink. Published Release and verified matching desktop/publish SHA256 `FFE68969C17267E083B142CDA0334A742CAC00A59FE8102F4BED92439F93C954`. In the native desktop release, switched Blue to Deep green and Pink, checked themed settings cards and homepage buttons/mode feedback while the 80% quota number and meter stayed blue, then restored Blue. Appearance and language remain system, idle detection remains 1800 seconds and running detection remains 300 seconds. No actual continuation was armed or sent.
