# Quota change trail and live color

Use the current flat meter and easing language. Add a muted lavender layer behind
the main fill. A real decrease preserves its former endpoint for 240ms, then
retracts with a gentler cubic ease-out, distinct from the main QuartOut curve.
Its duration is 1.25 times the main duration, with a 650ms floor.
Successive decreases continue from the visible trailing endpoint. Recovery,
initial reveal and untrusted/offline state clear the loss trail.

The main fill, percentage and percent sign share an instance-owned brush. Choose
the blue/amber/red band from the currently animated fill, retaining the existing
boundaries (>50, 10-50, <10). Crossing a boundary crossfades from the current ink
to its new theme color over 180ms. Theme switches refresh this local brush from
the palette. Windows' reduced-animation preference skips the added movement.

Keep one active digit timer, and replace it when a fresher quota arrives. Same
value renders must not restart either animation. Preserve the existing numeral
reveal, fixed meter geometry, connection states and continuation logic.

- [x] Reproduce the missing trail and premature color switch with the actual WPF
  quota card, using fake readings and no network or continuation sends.
- [x] Implement only the quota layers, theme trail colors and quota animation
  paths; verify decrease, interruption/recovery, band crossfades, zero and offline.
- [x] Run the complete regression suite, inspect light/dark fixtures, publish,
  replace the idle desktop executable and retain the previous version.

## Verification

All 14 checks pass with `dotnet run --project tests/DetectionFrequency.Tests -c
Release --no-restore`. The real WPF quota card is hosted with the application's
pixel-rounding settings, using simulated readings. The old code first failed
because it jumped to the target color before the visible bar crossed the band.

Checks cover 100-to-60 delay/settling, distinct nonlinear trailing movement,
same-value renders, successive losses preserving their original endpoint,
blue/amber/red intermediate colors, interrupted recovery, theme changes, zero,
offline cancellation and the first numerical reveal. Exactly 10% and 50% remain
amber. A 10.01-to-9.99 change also reaches red when movement is smaller than a
rendered pixel. Color samples follow the animated width, with final values anchored
to the actual reading; float noise from converting widths cannot misclassify a
boundary.

Light/dark card frames and a color crossfade were visually inspected. Copies are
saved with the original edited files, prior desktop executable and verification
logs under `_backups/20261101-quota-trail/`. The existing menu and frequency
checks remain green. No continuation command was sent or real quota data changed.
Windows' reduced-animation preference is honored in code, but the OS setting was
not changed during inspection.

Publishing succeeded and desktop/publish SHA-256 matches:
`929179818F59AB28BE9C142E72A8F43EB24296622CC05EFDB2A313E728A09E6E`.
