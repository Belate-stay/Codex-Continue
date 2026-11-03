# Borderless controls implementation plan

**Goal:** Remove visible solid control outlines throughout the existing interface while preserving the startup title transition and its slow-to-fast motion.

**Design:** Give action buttons a small, soft shadow on a separate background caster so live type remains sharp. Keep settings selections and mode indicators flat, with rounded theme-color fills and gentle opacity changes. Use filled wells and soft focus halos for inputs, checkboxes and keyboard focus. Remove visible popup/card/tooltip strokes; keep subtle internal separators, icon line art and the native resize frame. Preserve component sizes, dropdown alignment, red semantic icons and the fixed red Close feedback.

- [x] Reproduce remaining solid strokes with a native WPF surface check covering home/manual/settings, light/dark appearances and three accents.
- [x] Update the shared button/selection/focus styles and local dropdown/frequency surfaces without changing business behavior or layout geometry.
- [x] Inspect rendered normal, selected and disabled states, rerun startup-motion and full regression checks, publish both changes together and verify current user preferences survive replacement.

**Files:** `src/App.xaml`, local surfaces in `src/MainWindow.xaml`, `src/TrayMenu.xaml`, focused `BorderlessChecks.cs`, test `Program.cs`, `PRODUCT.md`. Keep the current transparent 1 DIP input inset where it aligns text and placeholder geometry. No new package or setting.

**Focused evidence:** `red.log` reproduces the solid outline on the selected mode. `surface.log` passes the actual WPF visual-tree checks across Automatic/Manual/Settings, two appearances and three accents, including sharp live button text, separate shadow casters, flat settings selections and a soft keyboard-focus cue. The six `borderless-*` captures show the default-accent light/dark surfaces. Tooltip shadows also use a separate caster. Transparent input insets retain the prior text and dropdown geometry.

**Tray regression:** Native activation in `TrayIcon.Opened` could synchronously move WPF keyboard focus to the visible main window and close the menu. `tray-diagnostic-2.log` captures the dependency-property stack through `SetForegroundWindow`; `tray-corrected.log` passes the real popup/keyboard check after avoiding redundant native activation when this process already owns foreground. Keep the normal outside-click behavior; no test-only stay-open setting or debug hooks remain.

**Delivery:** All 41 groups pass in `regression-verified.log`; the final dedicated tray check also passes with its transparent-outline assertion (`tray-final.log`). Light/dark native WPF renders and the actual desktop home/settings views were inspected. The single-file Beta 0.9.1 build was copied to the canonical publish folder and `D:\Desktop\CodexContinue.exe`. Matching hashes and unchanged user-preference hashes are recorded in `deployment.json`; the prior executable and current preferences are backed up alongside it. The shared startup-title animation was shipped in the same build and reviewed without running a continuation.
