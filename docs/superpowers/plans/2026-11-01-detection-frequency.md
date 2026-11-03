# Detection Frequency Implementation Plan

**Goal:** Persist separate idle and running detection frequencies, including continuous serialized detection while running and continued idle retries when disconnected.

**Architecture:** Extend AppSettings with validated seconds values (idle 300, running 60; running 0 means continuous, paced at one second). Resolve the timer from the current armed state. Coalesce scans so fast polling and manual checks cannot overlap. Reuse the settings radio controls and their existing theme-aware selection fades.

**Tech stack:** .NET 9, WPF, System.Text.Json; a small executable regression suite with no NuGet dependencies.

- [x] Write and run failing regression checks in `tests/DetectionFrequency.Tests`: defaults, legacy JSON, all ten choices, invalid values, round-trip persistence, state-dependent timer intervals while offline and in flight, and scan coalescing.
- [x] Extend `src/AppSettings.cs` with validated idle/running settings and `GetScanInterval(bool running)`. Make the settings checks pass.
- [x] Add the two five-choice groups in `src/MainWindow.xaml`, localize them in `src/Loc.cs`, and restore/save their selected values in `src/MainWindow.xaml.cs`.
- [x] Replace hard-coded startup/arm/disarm intervals with `UpdateScanInterval()`. Share an active scan task. Keep disconnected idle detection scheduled. Make the runtime checks pass.
- [x] Bound RPC initialization reads so a cold start cannot stall serialized detection indefinitely; verify with a blocked anonymous pipe.
- [x] Give manual sends an independent deadline timer so long detection intervals do not delay them. Cancel on stop, preserve retry pacing, and contain transport/UIA exceptions in the existing failure path.
- [x] Build Release, inspect light/dark and Chinese/English settings with Computer Use, verify persistence through restart, and exercise the frequency selections.
- [x] Publish the single-file executable, preserve the previous desktop binary, deploy to `D:\Desktop\CodexContinue.exe`, and verify its hash and launch state.

Verification commands:

```powershell
dotnet restore tests/DetectionFrequency.Tests/DetectionFrequency.Tests.csproj --configfile tests/NuGet.Config
dotnet run --project tests/DetectionFrequency.Tests -c Release --no-restore
dotnet publish src/CodexContinue.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true --no-restore -o publish
```

The folder is not a Git repository; originals are preserved in `_backups/20261101-detection-frequency` for focused diffs and rollback. No Git repository or worktree will be created.

## Verification

- All 12 regression checks pass. The suite creates an in-process WPF window and never sends a real continuation message.
- UI reviewed in light/dark themes and Chinese/English. Selection fades, immediate application feedback and the realtime hint work.
- A one-minute idle interval updated real quota readings while the settings page was open. Persisted one-minute/realtime choices survived closing the test instance and launching the deployed executable.
- Final saved configuration restored to the user's existing light/system appearance and requested defaults: idle 300 seconds, running 60 seconds.
- Published and desktop SHA-256: `DCA418DFEC37D602615C09AF239B774C40780234AA05C21E2198942E443FA8D4`.
- Previous desktop executable preserved as `_backups/20261101-detection-frequency/CodexContinue.before.exe`.
- Independent code review confirmed the timeout, manual deadline and send-exception fixes; no remaining blocking findings.
- No real continuation message or quota-exhaustion/recovery cycle was triggered during verification. Existing warnings in `MorphEngine.cs` remain outside this change.
