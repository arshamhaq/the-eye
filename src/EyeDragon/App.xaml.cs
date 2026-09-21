using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EyeDragon.Core;
using EyeDragon.Services;
using EyeDragon.ViewModels;
using EyeDragon.Views;
using Microsoft.Win32;

namespace EyeDragon;

public partial class App : System.Windows.Application
{
    private readonly DispatcherTimer _sessionTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromSeconds(1)
    };
    private SettingsService? _settingsService;
    private StartupService? _startupService;
    private LogService? _log;
    private PetService? _petService;
    private SoundService? _soundService;
    private PetDefinition? _pet;
    private TrayService? _tray;
    private MainWindow? _mainWindow;
    private PetOverlayWindow? _overlay;
    private RestWindow? _restWindow;
    private SettingsWindow? _settingsWindow;
    private AnimationPreviewWindow? _previewWindow;
    private SessionManager? _session;
    private Mutex? _singleInstanceMutex;
    private SessionState _lastLoggedState = SessionState.Idle;
    private bool _developmentTimers;

    public static App CurrentApp => (App)Current;

    public bool IsExiting { get; private set; }

    public AppSettings Settings { get; private set; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\EyeDragon.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            System.Windows.MessageBox.Show("EyeDragon is already running in the system tray.", "EyeDragon", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _log = new LogService();
        _settingsService = new SettingsService();
        _startupService = new StartupService();
        _petService = new PetService(_log);
        _soundService = new SoundService();
        Settings = await _settingsService.LoadAsync();
        _developmentTimers = e.Args.Contains("--dev-timers", StringComparer.OrdinalIgnoreCase) ||
            string.Equals(Environment.GetEnvironmentVariable("EYEDRAGON_DEVELOPMENT_TIMERS"), "1", StringComparison.Ordinal);

        _pet = _petService.Load(Settings.SelectedPet);
        _session = new SessionManager(new SystemClock(), CreateSessionOptions());
        _session.SnapshotChanged += OnSnapshotChanged;
        _session.WarningRaised += OnWarningRaised;
        _sessionTimer.Tick += (_, _) => _session.Tick();

        var mainViewModel = new MainViewModel(_session, _petService.LoadFrame(_pet, "idle"));
        _mainWindow = new MainWindow { DataContext = mainViewModel };
        MainWindow = _mainWindow;
        _overlay = new PetOverlayWindow(_petService, _pet, Settings);
        _tray = new TrayService(
            ShowMainWindow,
            StartWorkingFromTray,
            BeginRestFromTray,
            OpenSettings,
            OpenAnimationPreview,
            RequestExit);

        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        if (!Settings.LaunchMinimized && !e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase))
        {
            _mainWindow.Show();
        }

        var captureArgument = e.Args.FirstOrDefault(argument => argument.StartsWith("--capture-ui=", StringComparison.OrdinalIgnoreCase));
        if (captureArgument is not null)
        {
            var capturePath = captureArgument["--capture-ui=".Length..];
            _mainWindow.ContentRendered += (_, _) => CaptureMainWindow(capturePath);
            if (!_mainWindow.IsVisible)
            {
                _mainWindow.Show();
            }
        }

        if (e.Args.Contains("--preview", StringComparer.OrdinalIgnoreCase))
        {
            OpenAnimationPreview();
        }

        _log.Info($"EyeDragon started{(_developmentTimers ? " with development timers" : string.Empty)}");
    }

    public void ShowMainWindow()
    {
        if (IsStrictRestLocked())
        {
            BringRestForward();
            return;
        }

        _mainWindow?.Show();
        _mainWindow?.Activate();
    }

    public void HideMainWindow() => _mainWindow?.Hide();

    public void OpenSettings()
    {
        if (IsStrictRestLocked())
        {
            BringRestForward();
            return;
        }

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(Settings) { Owner = _mainWindow?.IsVisible == true ? _mainWindow : null };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    public void OpenAnimationPreview()
    {
        if (IsStrictRestLocked())
        {
            BringRestForward();
            return;
        }

        if (_previewWindow is { IsVisible: true })
        {
            _previewWindow.Activate();
            return;
        }

        if (_petService is null || _pet is null)
        {
            return;
        }

        _previewWindow = new AnimationPreviewWindow(_petService, _pet, Settings);
        _previewWindow.Closed += (_, _) => _previewWindow = null;
        _previewWindow.Show();
    }

    public async Task ApplySettingsAsync(AppSettings settings)
    {
        if (_settingsService is null || _startupService is null || _session is null)
        {
            return;
        }

        Settings = settings.Normalize();
        await _settingsService.SaveAsync(Settings);
        try
        {
            _startupService.SetEnabled(Settings.LaunchWithWindows);
        }
        catch (Exception ex)
        {
            _log?.Error("Could not update Windows startup setting", ex);
        }

        _session.UpdateOptions(CreateSessionOptions());
        RecreateOverlay();
        _log?.Info("Settings saved");
    }

    public void RequestExit()
    {
        if (IsStrictRestLocked())
        {
            SystemSounds.Exclamation.Play();
            BringRestForward();
            return;
        }

        IsExiting = true;
        if (_restWindow is not null)
        {
            _restWindow.AllowClose = true;
            _restWindow.Close();
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _sessionTimer.Stop();
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        DispatcherUnhandledException -= App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
        _tray?.Dispose();
        _soundService?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _overlay?.Close();
        _log?.Info("EyeDragon stopped");
        base.OnExit(e);
    }

    private SessionOptions CreateSessionOptions()
    {
        if (_developmentTimers)
        {
            return SessionOptions.Development with
            {
                FiveMinuteWarningEnabled = Settings.FiveMinuteWarningEnabled,
                OneMinuteWarningEnabled = Settings.OneMinuteWarningEnabled,
                StrictModeEnabled = Settings.StrictModeEnabled
            };
        }

        return Settings.ToSessionOptions();
    }

    private void StartWorkingFromTray()
    {
        if (_session?.StartWorking() == true)
        {
            ShowMainWindow();
        }
    }

    private void BeginRestFromTray()
    {
        if (_session?.BeginVoluntaryRest() == true)
        {
            ShowMainWindow();
        }
    }

    private void OnSnapshotChanged(SessionSnapshot snapshot)
    {
        _tray?.Update(snapshot.State);
        if (snapshot.State != _lastLoggedState)
        {
            _log?.Info($"Session: {_lastLoggedState} -> {snapshot.State}");
            _lastLoggedState = snapshot.State;
        }

        if (snapshot.State is SessionState.Working or SessionState.MandatoryRestLocked)
        {
            if (!_sessionTimer.IsEnabled)
            {
                _sessionTimer.Start();
            }
        }
        else
        {
            _sessionTimer.Stop();
        }

        if (snapshot.State is SessionState.VoluntaryRest or SessionState.MandatoryRestLocked or SessionState.MandatoryRestComplete)
        {
            ShowOrUpdateRestWindow(snapshot);
        }
        else if (_restWindow is not null)
        {
            _restWindow.AllowClose = true;
            _restWindow.Close();
            _restWindow = null;
        }

        if (snapshot.State == SessionState.Working && snapshot.OneMinuteWarningRaised)
        {
            _overlay?.ShowCountdown(snapshot.Remaining);
        }
        else if (snapshot.State is not SessionState.Working)
        {
            _overlay?.HidePet();
        }
    }

    private void OnWarningRaised(SessionWarning warning)
    {
        if (_overlay is null || _session is null)
        {
            return;
        }

        if (warning == SessionWarning.FiveMinutesRemaining)
        {
            _overlay.ShowWarning(_developmentTimers ? "15 seconds left!" : "5 minutes left!", "warning", autoHide: true);
        }
        else
        {
            _overlay.ShowCountdown(_session.Snapshot.Remaining);
        }

        if (Settings.WarningSoundsEnabled)
        {
            _soundService?.PlayWarning(Settings.Volume);
        }
    }

    private void ShowOrUpdateRestWindow(SessionSnapshot snapshot)
    {
        if (_session is null || _petService is null || _pet is null)
        {
            return;
        }

        if (_restWindow is null)
        {
            var animation = snapshot.State == SessionState.MandatoryRestComplete ? "happy" : "resting";
            _restWindow = new RestWindow(_session, _petService.LoadFrame(_pet, animation));
            _restWindow.Update(snapshot);
            _restWindow.Show();
            _restWindow.Activate();
            if (Settings.RestStartSoundEnabled)
            {
                _soundService?.PlayRestStart(Settings.Volume);
            }
        }
        else
        {
            _restWindow.Update(snapshot);
            if (snapshot.State == SessionState.MandatoryRestComplete)
            {
                _restWindow.SetPetImage(_petService.LoadFrame(_pet, "happy"));
            }
        }
    }

    private void RecreateOverlay()
    {
        if (_petService is null || _pet is null)
        {
            return;
        }

        _overlay?.HidePet();
        _overlay?.Close();
        _overlay = new PetOverlayWindow(_petService, _pet, Settings);
    }

    private bool IsStrictRestLocked() =>
        _session?.State == SessionState.MandatoryRestLocked && Settings.StrictModeEnabled;

    private void BringRestForward()
    {
        if (_restWindow is not null)
        {
            _restWindow.Topmost = true;
            _restWindow.Activate();
        }
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.Mode == PowerModes.Suspend)
            {
                _session?.Suspend();
                _log?.Info("Work timer paused for system suspend");
            }
            else if (e.Mode == PowerModes.Resume)
            {
                _session?.Resume();
                _log?.Info("Work timer resumed after system resume");
            }
        });
    }

    private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                _session?.Suspend();
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                _session?.Resume();
            }
        });
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log?.Error("Unhandled UI exception", e.Exception);
        System.Windows.MessageBox.Show("EyeDragon encountered an unexpected problem. Timer data may have been reset.", "EyeDragon", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _log?.Error("Unobserved background exception", e.Exception);
        e.SetObserved();
    }

    private void CaptureMainWindow(string path)
    {
        if (_mainWindow is null || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var dpi = VisualTreeHelper.GetDpi(_mainWindow);
            var width = Math.Max(1, (int)Math.Ceiling(_mainWindow.ActualWidth * dpi.DpiScaleX));
            var height = Math.Max(1, (int)Math.Ceiling(_mainWindow.ActualHeight * dpi.DpiScaleY));
            var bitmap = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bitmap.Render(_mainWindow);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using var stream = File.Create(fullPath);
            encoder.Save(stream);
        }
        catch (Exception ex)
        {
            _log?.Error("Could not capture the UI preview", ex);
        }
        finally
        {
            IsExiting = true;
            Shutdown();
        }
    }
}
