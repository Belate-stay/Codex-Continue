# Conversation menu refinement

The home-page conversation selector keeps its existing connected-drawer shape and native ComboBox selection, keyboard and focus handling. Refine spacing, hover/selected states, full-name tooltips and a clear selected checkmark. Use a 3–4 DIP rounded scrollbar thumb inside a wider drag target.

The current exit Storyboard runs after Popup.IsOpen becomes false, so its window disappears before that animation is visible. Enable the native transparent-popup fade, which preserves the window for the close transition, and pair it with a short translation instead of stretching text. Keep the native popup lifetime and rapid-reopen cancellation behavior.

- [x] Add a real WPF popup/scroll regression check; confirm it fails before the change.
- [x] Refine only the conversation selector template and its localized running-status label.
- [x] Verify scroll position, selection, delayed popup destruction and rapid reopening; keep existing frequency tests green.
- [x] Review the live menu in both themes, including selecting a conversation and scrolling the list; verify native thumb drag events in the WPF regression check.
- [x] Publish and deploy the desktop executable, preserving the previous version.

## Verification

All 13 checks pass. The new menu check first reproduced the system-width scrollbar, then the popup disappearing before its exit, and finally the collapsed text ignoring the dark theme. It now checks theme-reactive text, the slim rounded thumb, scroll offset/value linkage, native thumb drag events, selection preservation, closing-window lifetime and rapid reopening. Existing detection-frequency and manual-deadline checks remain green.

The real menu was reviewed in light/dark themes, including wheel scrolling, the selected checkmark and a captured mid-close fade after choosing a different conversation. The user's light/system appearance and 300/60-second detection settings were restored after inspection. No continuation command was sent.

Native close lifetime follows WPF's transparent Popup Fade behavior; no custom control, delayed close callback or replacement selection state machine was introduced. The explicit ScrollViewer template ensures the scrollbar style is applied to the actual vertical scrollbar. Independent code review found no blocking issue.

Desktop/publish SHA-256: `10289DED5AF0A8827153B898C6AE8CC5985F4B4173573387DB907DCAD42ECF3D`.

The previous desktop executable and original edited files are preserved under `_backups/20261101-conversation-menu/`.

## Fixed-width and seam follow-up

The selector now has a fixed 380-DIP width, replacing content-dependent 260–360 bounds. Short names leave empty space and long names retain CharacterEllipsis. The popup face continues to bind to the selector's actual width.

The root translation was also a placement bug: after closing and reopening, the popup's transformed root changed the native placement origin. A regression reproduced a 9px gap. The external frame now remains stationary, with translation limited to the clipped internal scroll content; native fade and arrow feedback remain. The top edge overlaps the selector by one logical unit to avoid a rasterized seam.

The real-window regression mimics the horizontal StackPanel used on the home page, tests short and long selections, and measures both edges and the vertical join. Results: width 380/380; left/right deltas 0/0px; initial and reopened gap 0px. Rapid-reopen, close animation and scrolling checks remain green, with all 13 checks passing.

Alignment revision desktop/publish SHA-256: `14C35C588EA7E771458CD447F7E3993B209026715C180B44FA2C88F4725652D1`. Previous executable and edited files are preserved in `_backups/20261101-menu-alignment/`.
