# EyeDragon

EyeDragon is a lightweight, local-only Windows 11 focus/rest timer with an original purple dragon desktop companion. A session begins only when you press **Start Working**. The dragon warns near the taskbar, then presents a strict application-level rest screen when work time ends.

EyeDragon is inspired by the 20-20-20 concept, but it is not medical software and makes no medical claims. Work and minimum-rest durations are configurable; defaults are 20 minutes of work and 2 minutes of rest.

## Features

- Explicit `Idle → Working → Rest` session state machine
- Deadline-based timers that do not accumulate one-second tick drift
- Five-minute dragon warning that walks in without taking focus
- One-minute taskbar countdown
- Strict rest window that resists ordinary close, Alt+F4, and Escape attempts
- Voluntary early rest that resets the next session to its full duration
- System tray operation; closing the main window hides it rather than ending a session
- Manifest-driven, scalable pet packs and an immediate animation-preview window
- Persistent per-user settings and optional Windows startup
- Pause-on-lock/suspend policy so sleep is not counted as screen work
- Per-monitor DPI-aware positioning using supported desktop work-area APIs
- No account, telemetry, analytics, cloud service, or runtime network requirement

## Screenshots

![EyeDragon main window](docs/screenshots/current-main.png)

- Taskbar warning and countdown
- Transparent meditation rest screen
- Animation preview

## How the timer works

EyeDragon always launches in **Idle**. It never starts a session automatically.

1. Press **Start Working** to begin a fresh 20-minute session.
2. At five minutes remaining, the dragon walks into view near the current display's taskbar, warns you, and leaves.
3. At one minute remaining, the dragon returns and displays the live countdown.
4. At zero, the mandatory rest screen appears.
5. During the default two-minute strict interval, there is no **Rested** button.
6. When the minimum completes, **Rested** appears. The app waits for that deliberate click.
7. Clicking **Rested** begins a completely new full-duration work session.

**Resting Now** cancels the current work session and opens the meditation view immediately. Its **Rested** button starts a new full work session; unused time is never carried forward.

## Strict mode

Strict mode is enabled by default. While mandatory rest is locked, the rest window ignores normal close operations, Alt+F4, Escape, and tray Exit. It does not install hooks or drivers, disable Task Manager, freeze Windows, suspend unrelated programs, or prevent an administrator from terminating the process. This is an application-level guard against accidental dismissal, not a security boundary.

## Animation preview and fast development cycle

Open **Settings → Open Animation Preview** or use the tray's **Animation Preview** command. Every pose can be viewed and looped without starting a timer; the walk preview also travels horizontally.

For a complete short cycle:

```powershell
dotnet run --project src/EyeDragon -- --dev-timers
```

Development timing is 30 seconds of work, a warning at 15 seconds remaining, a countdown at 5 seconds, and a 10-second mandatory rest. Production defaults are unaffected.

Open the animation preview directly:

```powershell
dotnet run --project src/EyeDragon -- --preview
```

## Requirements and build

- Windows 11 (Windows 10 may work but is not the primary target)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), the current LTS target

```powershell
git clone <repository-url>
cd pet-timer
dotnet restore
dotnet build EyeDragon.sln
dotnet test EyeDragon.sln
dotnet run --project src/EyeDragon
```

## Publish

Create a self-contained Windows x64 single-file release:

```powershell
dotnet publish src/EyeDragon/EyeDragon.csproj -p:PublishProfile=win-x64
```

Output is written beneath `src/EyeDragon/bin/Release/net10.0-windows/win-x64/publish/`. Pet and sound assets remain beside the executable so packs can be inspected or replaced.

## Settings and local files

Settings are stored at:

```text
%LOCALAPPDATA%\EyeDragon\settings.json
```

Bounded transition/error logs are stored at:

```text
%LOCALAPPDATA%\EyeDragon\eyedragon.log
```

The previous log is retained as `eyedragon.log.previous` after rotation. EyeDragon does not write timer ticks to disk.

## Architecture

`EyeDragon.Core` contains session policy and has no WPF dependency. `SessionManager` owns every valid transition and calculates remaining time from a monotonic deadline. WPF windows observe immutable `SessionSnapshot` values; they do not own timer rules.

The application layer contains local settings/logging/startup services, pet loading, Windows display positioning, tray integration, and views. A one-second dispatcher timer runs only while a timed state is active. Sprite timers run only while an animated pet is visible, and decoded frames are cached in memory.

```text
src/
  EyeDragon.Core/          state machine, clock, settings and pet models
  EyeDragon/
    Services/              local persistence, logging, tray, sound and pets
    ViewModels/            presentation state and commands
    Views/                 main, settings, overlay, preview and rest windows
    Pets/PurpleDragon/     manifest and original redistributable artwork
tests/EyeDragon.Tests/     deterministic state/timing/settings tests
```

## Adding a pet pack

Create `Pets/<PetId>/pet.json` and its asset folder. A manifest describes dimensions, scale, FPS, anchors, and named animation frame lists. Rendering code selects animations by manifest key and contains no `if pet == PurpleDragon` branches.

Required animation keys for the current UI are:

- `idle`
- `walk`
- `warning`
- `countdown`
- `goRest`
- `resting`
- `happy`

Frame paths are resolved inside the pack directory; paths that escape it are rejected. Missing or invalid assets are logged and timer behavior continues with a fallback visual.

## Suspend, lock, and clock policy

The work timer uses monotonic elapsed time rather than wall-clock ticks. Work pauses on Windows suspend or workstation lock and resumes with the same remaining duration, preventing sleep from being counted as active screen work. A mandatory rest may complete while Windows is suspended; its completion is reconciled on resume.

## Performance goals

- No web runtime, server, background service, polling loop, or network dependency
- No timer while idle
- One low-frequency UI update per second during timed states
- Animation only while visible, capped at 15 FPS
- Cached, taskbar-sized pet frames
- Bounded logging and settings writes only when settings change

## Known limitations

- The strict rest overlay covers the monitor containing the cursor when rest begins, not every connected display.
- Taskbar placement uses the supported monitor work area. Auto-hide and nonstandard taskbar tools may leave a small visual gap.
- The tray icon uses classic Win32 notification-area behavior because WPF has no built-in tray API.
- Strict mode cannot and should not prevent deliberate termination through Task Manager or administrative tools.
- There is no installer yet; use the self-contained publish output.

## Roadmap

- Optional all-monitor rest overlays
- Additional original pet packs
- Signed release artifacts and a lightweight installer
- Optional accessibility themes and reduced-motion mode

## Contributing

Keep timer policy in `EyeDragon.Core`, avoid high-frequency work, add deterministic tests for state changes, and include licensing/provenance for every contributed asset. Run `dotnet build` and `dotnet test` before opening a change.

## License

Code and original project assets are available under the [MIT License](LICENSE).
