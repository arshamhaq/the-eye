# EyeDragon

EyeDragon is a lightweight Windows 11 focus/rest timer built around an original purple dragon desktop companion.

Development is in progress. The application starts idle, uses an explicit session state machine, and will provide taskbar warnings plus a strict, application-level minimum rest period.

## Build

```powershell
dotnet restore
dotnet build
dotnet test
```

EyeDragon currently targets `net9.0-windows`, matching the Windows desktop SDK available in the development environment.

## Privacy

EyeDragon is local-only: no account, telemetry, analytics, cloud service, or internet connection is required at runtime.

## License

MIT. See [LICENSE](LICENSE).
