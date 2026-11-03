# Single-instance startup

**Goal:** Only one Codex-Continue instance may own the application in the current Windows session. Reopening the executable restores and focuses that same window, including when it is minimized or hidden in the tray.

**Approach:** Acquire a named mutex before constructing any window or tray icon. Open a named auto-reset activation event before acquiring the mutex so simultaneous startup cannot lose the activation request. The primary instance watches the event and dispatches the existing restore method to its UI thread; secondary instances signal it and immediately exit. Grant foreground activation to the existing window's process when available. Release the mutex/event when exiting and recover an abandoned mutex after a crash.

- [x] Add process-based checks using unique test names for duplicate launches, concurrent startup, normal release and abandoned-owner recovery. Confirm failure on the current code.
- [x] Add the startup guard, explicit primary-window construction, activation callback and exit disposal. Reuse the existing tray restore path.
- [ ] Run all regression checks, publish and update the desktop executable. Retire the two old tray-only processes and verify repeated launches keep the same primary PID/window, including restoration from tray/minimized state. Do not arm or send a real continuation.

**Files:** `src/SingleInstance.cs`, `src/App.xaml`, `src/App.xaml.cs`, `src/MainWindow.xaml.cs`, `src/NativeMethods.cs`, `tests/DetectionFrequency.Tests/SingleInstanceChecks.cs`, `tests/DetectionFrequency.Tests/Program.cs`.

**Verification:** `dotnet run --project tests/DetectionFrequency.Tests -c Release --no-restore`. Evidence and backups: `_backups/20261102-single-instance`.
