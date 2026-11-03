# Explicit conversation selection and command validation

Prefill `/Continue` once at startup. Leave the conversation unselected until the
user chooses it; refreshes preserve only that exact ID, never a matching name,
running conversation, first item or latest rollout. An empty selection displays
a localized placeholder. Existing one-shot clearing after success is retained.

The Run button is enabled only with a selected conversation and non-whitespace
prompt. The click and final arm paths recheck this condition. Stop stays enabled
while armed, and the already chosen running target is retained across a missing
scan result. No attachments-only shortcut is allowed.

Use the keycap's themed gray disabled state and enable ToolTipService.ShowOnDisabled
for Run. Its incomplete-state tooltip is `请查看内容填写是否完整`, with an English
equivalent. Detection shows `检测Codex连接与额度`, also localized. A global ToolTip template supplies 8-DIP corners, existing surface/ink
colors, restrained border and padding for all hover hints.

- [x] Reproduce current auto-selection/default/validation behavior in WPF checks.
- [x] Implement the local state updates, guarded arming, empty-selection hint and
  shared rounded tooltip style. Update existing fixtures to choose test targets.
- [x] Verify all input combinations, exact-ID preservation, target disappearance,
  Stop availability and light/dark/localized tooltip styling.
- [x] Run the existing suite, inspect the native default form and rounded tooltip rendering,
  publish, preserve the old executable and update the desktop app.

## Verification

All 16 checks pass. The original code first failed on the empty initial prompt
and active-blue disabled face. New checks exercise no selection, empty/whitespace
prompts, attachments-only input, custom prompts, list reorder/rename, same-name
replacement, guarded arming, target disappearance while armed, Stop availability,
disabled-hover configuration and both language variants of both hints.

The real desktop home page shows `/Continue`, `请选择对话` and a disabled gray Run
button. Tooltips were rendered independently in light/dark WPF templates and
visually checked for their 8-DIP corners, text, surface and border. Native hover
capture and selection inspection were interrupted by user input; no continuation
was triggered. Tooltip renderings and verification logs are preserved beside the
original files and previous executable under `_backups/20261101-command-validation/`.

The canonical publish executable was temporarily locked. Publishing to the local
release staging folder succeeded, and its binary was copied to both desktop and
canonical publish after closing the confirmed-idle instance. Both SHA-256 values:
`752E06FBF02E71A327A0B3A8EECF42669240828F0770F50070957F7799A62892`.
