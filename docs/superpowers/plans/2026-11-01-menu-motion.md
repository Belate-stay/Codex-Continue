# Conversation menu motion

Refine the existing connected conversation drawer. Keep the fixed 380-DIP field,
native selection/focus handling, palette and stationary popup bounds.

- The visible scrollbar grows from 3 to 6 DIPs on track hover or during dragging,
  using a 160ms deceleration, and settles back over 220ms. Its 10-DIP hit area
  stays fixed. Dragging keeps it expanded even if the pointer leaves the track.
- The field's lower corners interpolate from 12 to 0 as the drawer opens. A
  rounded reveal moves down the menu over 220ms, with no scaling of text. Closing
  retracts over 140ms while the field's corners recover smoothly.
- The native Popup fade retains the close lifetime. Every reversal begins at the
  current animation value. Windows' client-area animation preference disables
  these new transitions.

Implementation stays in the conversation template and a small WPF attached
animation helper, with no new dependency or change to continuation behavior.

1. [x] Extend the actual WPF menu check to catch abrupt corner changes and missing
   reveal, then run it against the old template to reproduce the missing behavior.
2. [x] Implement the local motion, verify fixed geometry, close lifetime, rapid
   reversals and native thumb dragging; run all existing checks.
3. [x] Inspect the real application, publish, and update the desktop executable
   after confirming it is idle. Preserve the previous version.

## Verification

The old template first failed on its instantaneous lower-corner change. The
extended check now verifies the intermediate rounded reveal, its final height,
smooth corner recovery, close lifetime and a complete reveal after rapid
reversal. It measures scrollbar expansion during the protected dragging state
(5.8 DIPs at the intermediate sample), the 6-DIP active width and 3-DIP resting
width; the existing routed drag/scroll checks remain intact.

Live inspection also found that the selected title inherited the ToggleButton's
system foreground. A check against the actual rendered title reproduced this
in the dark theme. The toggle now passes through the ComboBox foreground.

`dotnet run --project tests/DetectionFrequency.Tests -c Release --no-restore`
passes all 13 checks. Fixed width is 380/380 for short/long titles, with both
horizontal edge deltas and the initial/reopened vertical gap measuring 0px.
Publishing succeeded. The real menu and hover expansion were reviewed in light
and dark themes. The application is back on the home page, idle, with the user's
light/system appearance and 300/60-second frequencies restored. No continuation
command was sent. The Windows reduced-animation setting was respected in code
but was not changed during inspection.

Desktop/publish SHA-256:
`32C4D93FB53A82A4211B7DCDC333B59A7791F62A995703DE79797299F970CDFA`.
Previous executable and edited files: `_backups/20261101-menu-motion/`.

## Label stability follow-up

The open-state border removed its bottom stroke from layout as well as from the
outline. The actual conversation row reproduced a height change from 46 to 45
DIPs and a 1px upward shift of the centered label. Preserve that stroke's space
with 1 DIP of bottom padding in the same open-state trigger. No animation or
control-height workaround is needed.

The regression now hosts the real label and picker row together. During opening,
open, closing, closed and rapid reopening, the row remains 46 DIPs high and the
label's screen-position deltas are 0/0px. All 13 checks pass. Menu edges remain
aligned, with an intentional 1px overlap at the join and no gap.

Published desktop SHA-256:
`9B46DEA0EBC30770D770E8ADDDAFE1097BE1C38CA412C49D4299002946340AA9`.
Prior executable and edited files: `_backups/20261101-menu-label-stability/`.
