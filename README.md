# TheEye

Native Windows 11 focus/rest timer with a floating triangle companion.
Double-click `artifacts/TheEye-win-x64/TheEye.exe`; no command or runtime
installation is needed. Keep the adjacent Assets and Pets folders together.
Right-click the running taskbar icon to pin it.

![Main window](docs/screenshots/current-main.png)
![Rest screen](docs/screenshots/current-rest.png)

## Use

- Start Working begins a fresh 20-minute session with the companion hidden.
- At five minutes remaining it shows “5 min to rest” for six seconds, makes
  one slow left-and-right pass, then hides. The pass takes 36 seconds at
  default speed and respects the animation-speed slider.
- At one minute remaining it returns with a live 01:00 to 00:01 countdown;
  at zero the rest screen replaces it. It stays hidden between these reminders.
- Preview on taskbar, in Settings or the main window, runs the actual desktop
  animation for 25 seconds without changing the session.
- Resting Now minimizes the main window and opens the supplied meditation pose
  on an opaque ivory/pink cloud scene.
  An early break starts the same configured minimum rest lock as an automatic
  break (when strict mode is enabled); it cannot be completed early.
- At the work deadline, mandatory rest begins. For two minutes the visible
  Rested button dodges the pointer and rejects clicks, touch and keyboard input.
  The session state also rejects early completion. After the minimum, it settles
  and clicking it starts a fresh full session.
- Closing the main window hides it to the tray. Before starting work, tray Exit
  quits normally. After starting, it becomes **Use emergency ticket**: three
  exits per local Monday-Sunday week, with confirmation before spending one.
  Tickets do not accumulate or replenish when the app is reopened.
  Single left-click the tray icon to reopen it; right-click opens its menu.
- The commitment continues through working, locked rest and completed rest.
  With no tickets left, ordinary app exit is unavailable until the weekly reset
  or a PC shutdown/restart. A ticket button is also available on the rest screen.
- A normal PC shutdown/restart does not spend tickets; TheEye launches idle
  when Windows next starts. Canceling a shutdown or merely logging off does not
  release a saved commitment. Launch with Windows remains enabled.
- If forcibly terminated and reopened during the same boot, TheEye restores
  the session. Work time advances while closed; a missed work deadline opens a
  full rest minimum. Time spent with the rest window killed does not satisfy
  its minimum. A checkpoint is saved at transitions and every five seconds.

Size, speed, animation, sounds and durations are configurable. Invalid numeric
input prevents saving. Work pauses during Windows lock/suspend; mandatory rest
reconciles on resume. Background applications are unaffected.

### What this can and cannot enforce

This is a local commitment aid, **not an unkillable or tamper-proof application**.
Task Manager, Windows process termination, an administrator, uninstallation or
editing/removing local data can bypass it. TheEye does not disable Task Manager,
change process security, install a service/driver/watchdog, or prevent Windows
shutdown. If killed, it cannot display reminders until launched again.

The ledger lives in `%LOCALAPPDATA%/TheEye/commitment.json`. Ticket spending and
commitment release are saved atomically before exit. A failed save does not
authorize exit, and unreadable ledger data is not silently reset to three tickets.
Windows uptime/boot-time estimates and the current logon identity distinguish
relaunches from shutdowns, including normal Fast Startup shutdowns. Abrupt power
loss and major system-clock changes cannot be tested here and may affect recovery.

## Artwork and display

The two new supplied originals are preserved at native 912 x 1120 resolution.
The standing character is extracted with real alpha, preserving its white eye
and yellow aura while removing the white matte. No character asset is downsampled.
The main view layers this larger character over the native 1672 x 941 mountain
background. Rest uses a 2240 x 1260 landscape containing every original
meditation pixel unchanged, with the cloud background extended for the text.
Rest fills physical monitor bounds (including 1920 x 1080 at 125% Windows DPI).
Display scaling never changes the saved source images.

Buttons and the main window have matching golden glows. The outer glow uses
its own backing shape so image/text rendering stays sharp. Custom titlebar
controls support minimize, maximize/restore and hiding to the tray; drag the
titlebar to move the window, or double-click it to maximize/restore.
See [artwork provenance](artwork/README.md).

## Build, test and publish

Development requires .NET 10 SDK.

```powershell
dotnet build TheEye.sln -c Release
dotnet test TheEye.sln -c Release
dotnet run --project src/TheEye
powershell -File tools/publish.ps1
```

The publisher checks every manifest frame exists, matches its source hash,
meets its expected dimensions, and that the sprite has actual transparency.
Artwork regression checks: `python tools/test_triangle_assets.py` (Pillow and
NumPy required). Rebuild artwork with `python tools/compose_triangle.py`
(also requires OpenCV and SciPy). The supplied originals are never overwritten.

- `--dev-timers`: 30-second work and 10-second rest.
- `--preview`: immediate taskbar preview.
- `--verify-ui=<absolute-directory>`: exercise actual WPF windows, save captures
  and a results file. Checks Settings Save/Cancel, published assets, actual
  movement, rest minimization, locked-button rejection and unlocking. It saves
  the current settings unchanged.
  Ticket tests use an isolated temporary ledger and never spend real tickets.
  Verification/capture overrides are ignored while a real commitment is active.

## Architecture

- src/TheEye.Core: deadline-based session state machine, monotonic clock,
  settings and pet manifest models.
- src/TheEye: WPF UI, tray, monitor positioning, full-resolution image cache,
  click-through overlay and local persistence.
- tests/TheEye.Tests: deterministic timing, transition and settings tests.
- src/TheEye/Pets/Triangle: active pack. Named animations support optional
  sourceRect pixel bounds; asset paths cannot escape their package.

The session owns completion rules. One-second ticks update countdowns. WPF
animation clocks move the cached sprite and stop when hidden. The overlay does
not take focus or intercept taskbar clicks. No Explorer injection, drivers,
runtime network, accounts or telemetry.

Settings: `%LOCALAPPDATA%/TheEye/settings.json`.
Bounded error log: `%LOCALAPPDATA%/TheEye/theeye.log`.
Launch after a completed shutdown starts idle; an interrupted commitment during
the same boot resumes instead. Weekly ticket usage survives both.

## Limitations

Rest covers the monitor containing the cursor when it begins. Auto-hide and
nonstandard taskbars can leave a small gap. Strict mode is an application guard,
not a system lock. There is no installer/signing yet. Legacy dragon files remain
in the source tree but are excluded from the release.

## License

Application code is [MIT licensed](LICENSE). Supplied character artwork retains
its existing ownership and is not covered by the code license.
