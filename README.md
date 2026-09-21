# TheEye

Native Windows 11 focus/rest timer with a floating triangle companion.
Double-click `artifacts/TheEye-win-x64/TheEye.exe`; no command or runtime
installation is needed. Keep the adjacent Assets and Pets folders together.
Right-click the running taskbar icon to pin it.

![Main window](docs/screenshots/current-main.png)
![Rest screen](docs/screenshots/current-rest.png)

## Use

- Start Working begins a fresh 20-minute session. The companion floats slowly
  left and right above the taskbar, with a soft fade and vertical bob.
- Preview on taskbar, in Settings or the main window, runs the actual desktop
  animation for 25 seconds without changing the session.
- Resting Now minimizes the main window and opens an opaque ivory rest scene.
  Voluntary rest can finish immediately.
- At the work deadline, mandatory rest begins. For two minutes the visible
  Rested button dodges the pointer and rejects clicks, touch and keyboard input.
  The session state also rejects early completion. After the minimum, it settles
  and clicking it starts a fresh full session.
- Closing the main window hides it to the tray. Tray Exit intentionally quits.
  Strict mandatory rest rejects ordinary close, Escape, Alt+F4 and tray Exit.
  It does not prevent termination through Task Manager.

Size, speed, animation, sounds and durations are configurable. Invalid numeric
input prevents saving. Work pauses during Windows lock/suspend; mandatory rest
reconciles on resume. Background applications are unaffected.

## Artwork and display

Main and rest canvases are opaque 1920 x 1080 PNGs. The sprite is a 1920 x 1080
RGBA PNG with a golden aura. Only transparent padding is cropped at load time;
source images are never downsampled. Rest fills physical monitor bounds and was
verified at 1920 x 1080 with 125% Windows scaling. Main is resizable/maximizable.

**Artwork limitation:** the image service rejected regeneration. The character
is extracted from the supplied 404 x 495 reference, retained at native size on
larger canvases. These are not newly generated high-detail character images.
Rest currently retains the supplied pose. See [provenance](artwork/README.md).

## Build, test and publish

Development requires .NET 10 SDK.

```powershell
dotnet build TheEye.sln -c Release
dotnet test TheEye.sln -c Release
dotnet run --project src/TheEye
powershell -File tools/publish.ps1
```

The publisher checks every manifest frame exists, matches its source hash, is
1920 x 1080, and that the sprite has actual transparent background pixels.

- `--dev-timers`: 30-second work and 10-second rest.
- `--preview`: immediate taskbar preview.
- `--verify-ui=<absolute-directory>`: exercise actual WPF windows, save captures
  and a results file. Checks Settings Save/Cancel, published assets, actual
  movement, rest minimization, locked-button rejection and unlocking. It saves
  the current settings unchanged.

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
Launch with Windows always starts idle.

## Limitations

Rest covers the monitor containing the cursor when it begins. Auto-hide and
nonstandard taskbars can leave a small gap. Strict mode is an application guard,
not a system lock. There is no installer/signing yet. Legacy dragon files remain
in the source tree but are excluded from the release.

## License

Application code is [MIT licensed](LICENSE). Supplied character artwork retains
its existing ownership and is not covered by the code license.
