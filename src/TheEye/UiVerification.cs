using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheEye.Core;
using TheEye.Views;
using Button = System.Windows.Controls.Button;

namespace TheEye;

public partial class App
{
    private async Task VerifyUiAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var results = new List<string>();
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS " + message);
        }
        try
        {
            // Exercise the actual UI with a controllable clock, including the
            // full configured manual-rest minimum, without waiting minutes.
            var manualClock = new VerificationClock();
            _session!.SnapshotChanged -= OnSnapshotChanged;
            _session.WarningRaised -= OnWarningRaised;
            _session = new SessionManager(manualClock, CreateSessionOptions());
            _session.SnapshotChanged += OnSnapshotChanged;
            _session.WarningRaised += OnWarningRaised;
            _mainWindow!.DataContext = new ViewModels.MainViewModel(_session,
                _petService!.LoadFrame(_pet!, "landscape"), _petService.LoadFrame(_pet!, "walk"));
            ShowMainWindow();
            await Task.Delay(300);
            Capture(_mainWindow!, Path.Combine(directory, "main.png"));
            var hero = (System.Windows.Controls.Image)_mainWindow!.FindName("HeroImage");
            Check(hero.Source is BitmapSource { PixelWidth: 912, PixelHeight: 1120 }, "Main uses the native HQ character, not the old cropped sprite");
            Check(hero.Width == 780, "Main character is enlarged independently of taskbar size");
            var halo = (Border)_mainWindow.FindName("WindowHalo");
            Check(_mainWindow.AllowsTransparency && halo.Effect is System.Windows.Media.Effects.DropShadowEffect { ShadowDepth: 0 }, "Main has a separate golden outer glow");
            ((Button)_mainWindow.FindName("MaximizeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(150);
            Check(_mainWindow.WindowState == WindowState.Maximized && halo.Margin.Left == 0, "Custom maximize removes the glow gutter");
            var mainScreen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle);
            var mainDpi = VisualTreeHelper.GetDpi(_mainWindow);
            Check(Math.Abs(_mainWindow.ActualWidth * mainDpi.DpiScaleX - mainScreen.WorkingArea.Width) <= 1 &&
                  Math.Abs(_mainWindow.ActualHeight * mainDpi.DpiScaleY - mainScreen.WorkingArea.Height) <= 1,
                "Maximized main fits the monitor work area without covering the taskbar");
            Capture(_mainWindow, Path.Combine(directory, "main-maximized.png"));
            ((Button)_mainWindow.FindName("MaximizeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(_mainWindow.WindowState == WindowState.Normal && halo.Margin.Left == 24, "Restore returns the outer glow gutter");
            ((Button)_mainWindow.FindName("MinimizeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(_mainWindow.WindowState == WindowState.Minimized, "Custom minimize works");
            ShowMainWindow();
            // Raise NotifyIcon's native mouse event, exercising the registered
            // handler rather than calling the open callback directly.
            var notify = (System.Windows.Forms.NotifyIcon)typeof(Services.TrayService)
                .GetField("_notifyIcon", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(_tray)!;
            var mouseClick = typeof(System.Windows.Forms.NotifyIcon).GetMethod("OnMouseClick",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            _mainWindow.Hide();
            mouseClick.Invoke(notify, [new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Right, 1, 0, 0, 0)]);
            Check(!_mainWindow.IsVisible, "Right-click tray action does not unexpectedly open the main window");
            mouseClick.Invoke(notify, [new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left, 1, 0, 0, 0)]);
            Check(_mainWindow.IsVisible, "Single left-click on tray icon opens the main window");
            Check(notify.Icon is not null, "Notification area loads the packaged eye icon");
            foreach (var pair in _pet!.Animations)
                Check(_petService!.LoadFrame(_pet, pair.Key) is not null, $"Published asset: {pair.Key}");
            OpenSettings();
            await Task.Delay(100);
            Capture(_settingsWindow!, Path.Combine(directory, "settings.png"));
            ((Button)_settingsWindow!.FindName("CancelSettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(_settingsWindow is null, "Settings Cancel closes without DialogResult exception");
            OpenSettings();
            await Task.Delay(100);
            ((Button)_settingsWindow!.FindName("SaveSettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var i = 0; i < 50 && _settingsWindow is not null; i++) await Task.Delay(100);
            Check(_settingsWindow is null, "Settings Save persists and closes without exception");
            OpenAnimationPreview();
            await Task.Delay(400);
            Check(_previewOverlay is { IsVisible: true }, "Preview uses the actual taskbar overlay");
            var image = (System.Windows.Controls.Image)_previewOverlay!.FindName("PetImage");
            Check(image.Source is BitmapSource { PixelWidth: 912, PixelHeight: 1120 }, "Taskbar loads the entire native HQ sprite without cropping");
            var transform = (TranslateTransform)image.RenderTransform;
            var before = transform.X;
            await Task.Delay(1800);
            Check(Math.Abs(before - transform.X) > 1, "Floating sprite actually travels horizontally");
            Check(image.ActualHeight + 12 < _previewOverlay.ActualHeight, "Sprite and hat fit inside overlay");
            Capture(_previewOverlay, Path.Combine(directory, "taskbar-overlay.png"));
            StopPreview();
            _session.UpdateOptions(_session.Options with { StrictModeEnabled = true });
            ((ViewModels.MainViewModel)_mainWindow.DataContext).StartWorkingCommand.Execute(null);
            Check(_overlay is { IsVisible: false }, "No companion during ordinary work");
            OpenAnimationPreview();
            StopPreview();
            Check(_overlay is { IsVisible: false }, "Ending preview does not enable constant floating");
            RecreateOverlay();
            Check(_overlay is { IsVisible: false }, "Changing settings does not enable constant floating");
            ((ViewModels.MainViewModel)_mainWindow.DataContext).RestingNowCommand.Execute(null);
            await Task.Delay(300);
            Check(_mainWindow!.WindowState == WindowState.Minimized, "Main window minimizes for rest");
            Check(_restWindow is { IsVisible: true }, "Rest window opens");
            Check(_session.State == SessionState.MandatoryRestLocked &&
                  _session.Snapshot.Remaining == _session.Options.MandatoryRestDuration,
                "Resting Now starts the full configured locked-rest timer");
            Check(!_session.CompleteRest() && !_session.StartWorking(), "Manual rest cannot be completed or restarted early");
            var manualRestButton = (Button)_restWindow!.FindName("RestedButton");
            manualRestButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(_session.State == SessionState.MandatoryRestLocked, "Manual Rested button cannot bypass the minimum");
            Check(manualRestButton.Background is LinearGradientBrush, "Rest button uses the pink-white gradient style");
            var restImage = (System.Windows.Controls.Image)_restWindow!.FindName("PetImage");
            Check(restImage.Source is BitmapSource { PixelWidth: 2240, PixelHeight: 1260 }, "Rest loads the full-resolution meditation landscape");
            Capture(_restWindow, Path.Combine(directory, "rest.png"));
            var restCapture = new BitmapImage(new Uri(Path.Combine(directory, "rest.png")));
            var restScreen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(_restWindow).Handle);
            Check(restCapture.PixelWidth == restScreen.Bounds.Width && restCapture.PixelHeight == restScreen.Bounds.Height, "Rest render matches physical display resolution at current Windows DPI");
            manualClock.MonotonicNow += _session.Options.MandatoryRestDuration - TimeSpan.FromSeconds(1);
            _session.Tick();
            Check(!_session.CompleteRest(), "Manual rest stays locked until the final second");
            manualClock.MonotonicNow += TimeSpan.FromSeconds(1);
            _session.Tick();
            Check(manualRestButton.Focusable, "Manual Rested unlocks after the minimum");
            Capture(_restWindow, Path.Combine(directory, "rest-unlocked.png"));
            manualRestButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(_restWindow is null && _session.State == SessionState.Working, "Rested starts a fresh work session");
            _session.Stop();

            var clock = new VerificationClock();
            var warningSession = new SessionManager(clock, SessionOptions.Default);
            warningSession.WarningRaised += OnWarningRaised;
            warningSession.SnapshotChanged += OnSnapshotChanged;
            warningSession.StartWorking();
            clock.MonotonicNow = TimeSpan.FromMinutes(15);
            warningSession.Tick();
            Check(_overlay is { IsVisible: true }, "Five-minute warning appears");
            Check(((TextBlock)_overlay!.FindName("BubbleText")).Text == "5 min to rest", "Five-minute message is correct");
            await Task.Delay(TimeSpan.FromSeconds(36 / Math.Clamp(Settings.AnimationSpeed, 0.5, 2) + 1));
            Check(!_overlay.IsVisible, "Five-minute pass hides automatically after left and right");
            warningSession.Tick();
            Check(!_overlay.IsVisible, "Ordinary timer ticks do not replay five-minute warning");
            clock.MonotonicNow = TimeSpan.FromMinutes(19);
            warningSession.Tick();
            Check(_overlay.IsVisible, "Final minute appears");
            Check(((TextBlock)_overlay.FindName("BubbleText")).Text == "01:00", "Countdown begins at 60 seconds");
            clock.MonotonicNow += TimeSpan.FromSeconds(1);
            warningSession.Tick();
            Check(((TextBlock)_overlay.FindName("BubbleText")).Text == "00:59", "Countdown decrements to 59 seconds");
            clock.MonotonicNow += TimeSpan.FromSeconds(58);
            warningSession.Tick();
            Check(((TextBlock)_overlay.FindName("BubbleText")).Text == "00:01", "Countdown reaches one second");
            clock.MonotonicNow += TimeSpan.FromSeconds(1);
            warningSession.Tick();
            Check(!_overlay.IsVisible && _restWindow is { IsVisible: true }, "At zero the overlay yields to rest");
            _restWindow!.AllowClose = true;
            _restWindow.Close();
            _restWindow = null;
            _sessionTimer.Stop();
            warningSession.WarningRaised -= OnWarningRaised;
            warningSession.SnapshotChanged -= OnSnapshotChanged;
            clock.MonotonicNow = TimeSpan.Zero;
            var testSession = new SessionManager(clock, SessionOptions.Development);
            testSession.StartWorking();
            clock.MonotonicNow = TimeSpan.FromSeconds(31);
            testSession.Tick();
            var lockedRest = new RestWindow(testSession, _petService!.LoadFrame(_pet, "resting"));
            lockedRest.Update(testSession.Snapshot);
            lockedRest.Show();
            await Task.Delay(100);
            var button = (Button)lockedRest.FindName("RestedButton");
            var originalPosition = new System.Windows.Point(Canvas.GetLeft(button), Canvas.GetTop(button));
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(testSession.State == SessionState.MandatoryRestLocked, "Button cannot bypass mandatory minimum");
            Check(originalPosition != new System.Windows.Point(Canvas.GetLeft(button), Canvas.GetTop(button)), "Locked button dodges an activation attempt");
            lockedRest.Close();
            Check(lockedRest.IsVisible, "Ordinary close cannot dismiss mandatory rest");
            clock.MonotonicNow += TimeSpan.FromSeconds(11);
            testSession.Tick();
            lockedRest.Update(testSession.Snapshot);
            Check(button.Focusable, "Rested unlocks after the minimum");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(testSession.State == SessionState.Working, "Unlocked Rested starts work");
            lockedRest.AllowClose = true;
            lockedRest.Close();
            File.WriteAllLines(Path.Combine(directory, "results.txt"), results);
        }
        catch (Exception ex)
        {
            results.Add("FAIL " + ex);
            File.WriteAllLines(Path.Combine(directory, "results.txt"), results);
            Environment.ExitCode = 1;
        }
        finally { RequestExit(); }
    }
    private static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var visual = (FrameworkElement)window.Content;
        var dpi = VisualTreeHelper.GetDpi(visual);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(visual.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
    private sealed class VerificationClock : IClock
    {
        public TimeSpan MonotonicNow { get; set; }
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
