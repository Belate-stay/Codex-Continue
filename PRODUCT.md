# Codex-Continue

## Register

product

## Users and purpose

Windows Codex users monitor quota and send a continuation command when quota recovers, or once at a chosen time. Automatic continuation defaults to one successful send and can optionally repeat after each new quota exhaustion/recovery cycle until stopped. Manual Timer always sends once. The current application is C# / WPF on .NET 9.

## Design principles

- Preserve the established quiet, modern desktop interface and native window behavior.
- Reuse existing controls, theme brushes, typography and state animations.
- Keep controls free of visible solid outlines. Give action buttons restrained shadows on separate background layers to keep live text sharp; use flat theme-colored selections, filled input wells and soft keyboard-focus feedback. Preserve subtle internal dividers and icon strokes.
- Switching between Automatic Continue and Manual Timer fades only the send-time and loop controls; shared content stays in place and fully visible. Settings and log retain page transitions.
- Mode-navigation hover uses only the hand cursor, without a tinted card or foreground change. The GitHub mark retains its small eased lift, tilt and scale feedback, with no tinted hover card. Selected-mode cards and click-driven indicator/icon transitions remain distinct. Release details keep the full publication date in a fixed bottom-right footer below the scrolling notes. All Follow system settings use the same plain label without a Windows logo.
- Show actual connection and quota state; an offline window must not imply fresh quota data.
- Keep settings immediate and persistent, with the same behavior in Chinese and English.
- Default transparency effects to off. When enabled on supported Windows, use native Desktop Acrylic across the client/title region, translucent matte surface tints and opaque readable text; keep normal WPF rendering and native resize behavior. Respect system transparency/high contrast and opaque fallback. Keep the underlying startup/main content hidden behind translucent pages, and retain native blur until an opaque exit fade settles. Accent Follow system reads Windows colorization and updates when the OS color changes, with contrast-aware foreground colors.
- Center the loop check's vector drawing in its checkbox. Its pointer help contains only the enabled looping mechanism, in two short paragraphs with comfortable line spacing.
- Retain keyboard and accessibility semantics for new controls; verify both themes in the running application.
- Support native maximize/restore, title-bar double-click and border resizing. Below 960 DIP, stack the command and status cards and move settings choices below their labels; keep essential actions reachable through vertical scrolling. Bound wide content and grow the prompt editor moderately instead of scaling the whole interface.
- Use PerMonitorV2 DPI awareness with vector icons, native text and refreshed animation bitmap caches when DPI changes. Start at 1120 × 730 DIP, fit smaller monitor work areas and preserve a maximized window when restoring from minimize or tray.
- Keep the tray menu in this order: Open Codex-Continue, Settings, Run/Stop, Exit. Use a rounded WPF card with consistent row spacing and eased hover feedback; keep the run triangle and stop square semantic red across accents. Tray actions share the main continuation validation/state, and Settings preserves an armed task.
- Keep the three caption feedback faces 32 DIP tall and flush with the window's top edge, inside the existing 48 DIP title-bar hit area. The Close highlight stays red independently of the accent.
- Connect splash and connection screens through one moving title: start at the central title's measured bounds, shrink and move slowly then faster into the actual header, and finish with a short landing. Reveal the vector logo and connection content in an overlapping sequence on an opaque canvas. Motion must settle cleanly on skip, retry, settings, release details, hide/close and reduced motion; keep connection probing independent.

## Existing design context

The project history in `_progress_1030.md` specifies restrained feedback and rejects decorative glow, strong gradients, heavy shadows and bouncy scaling. `src/App.xaml` defines the reusable controls; `src/Themes/Light.xaml` and `Dark.xaml` define the palette. This feature extends the settings surface without redesigning the rest of the application.

## Release and updates in Version 1.0

Startup checks public GitHub Releases, including Beta releases, in the background. Newer versions appear in a dismissible bottom-right notification. Release details keep the title bar, hide the navigation menu, and preserve the underlying mode, command and continuation. About includes a manual check with current-version and network-error feedback. Native release notes support headings, lists, code and web links.

Download streams the selected Windows package into `%LOCALAPPDATA%\CodexContinue\Updates`, displays progress, verifies its size and published SHA256 when available, and offers Open folder on completion. Downloads continue when returning to the main page or hiding to tray. The app does not automatically run installers.

The release displays `Version 1.0` and uses numeric version `1.0.0`. Publish the Windows x64 self-contained single-file package as `Codex-Continue.exe`; keep the internal `CodexContinue` assembly identity for WPF resources. Startup displays no forced preview card: only a successfully detected newer release triggers the notification. Current/older releases and failed background checks stay quiet. Update comparisons treat `Version 1.0`, `v1.0`, and `v1.0.0` as the same version.
