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
            _session = new SessionManager(manualClock, CreateSessionOptions()) { BeforeFirstStart = PersistFirstStart };
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
            var exitItem = (System.Windows.Forms.ToolStripMenuItem)typeof(Services.TrayService)
                .GetField("_exitItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(_tray)!;
            Check(exitItem.Text == "Exit" && exitItem.Enabled, "Idle permits an ordinary exit without using a ticket");
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
            Check(_focusGuard is { IsCommitted: true, TicketsRemaining: 3 }, "Starting work durably commits without spending a ticket");
            Check(exitItem.Text?.Contains("Use emergency ticket") == true && exitItem.Text.Contains("3 left"), "Tray replaces Exit with weekly emergency-ticket action");
            RequestExit();
            Check(!IsExiting && _session!.State == SessionState.Working, "Ordinary exit cannot bypass a working commitment");
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
            var emergencyButton = (Button)_restWindow.FindName("EmergencyTicketButton");
            Check(emergencyButton.IsEnabled && emergencyButton.Content.ToString()!.Contains("3 left"), "Emergency exit is accessible on the full-screen rest window");
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
            Check(_focusGuard!.IsCommitted, "Finishing the rest minimum does not grant a free exit");
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

            // Exercise gaming through the real main-window command and the
            // same host callbacks, with a fresh isolated ticket ledger.
            _session!.SnapshotChanged -= OnSnapshotChanged;
            _session.WarningRaised -= OnWarningRaised;
            var gamingClock = new VerificationClock();
            _session = new SessionManager(gamingClock, SessionOptions.Default) { BeforeFirstStart = PersistFirstStart };
            _session.SnapshotChanged += OnSnapshotChanged;
            _session.WarningRaised += OnWarningRaised;
            _focusGuard = new FocusGuard(Path.Combine(_verificationDataDirectory!, "gaming", "commitment.json"));
            _mainWindow.DataContext = new ViewModels.MainViewModel(_session,
                _petService.LoadFrame(_pet, "landscape"), _petService.LoadFrame(_pet, "walk"));
            _lastLoggedState = SessionState.Idle;
            ShowMainWindow();
            var gamingButton = (Button)_mainWindow.FindName("StartGamingButton");
            Check(gamingButton.IsVisible && gamingButton.Command!.CanExecute(null), "Start Gaming is available alongside Start Working");
            gamingButton.Command!.Execute(null);
            Check(_session.Mode == SessionMode.Gaming && _session.Snapshot.Remaining == TimeSpan.FromMinutes(20), "Start Gaming begins a full twenty-minute session");
            Check(_focusGuard.Recovery!.Snapshot.Mode == SessionMode.Gaming && _focusGuard.TicketsRemaining == 3,
                "Gaming is durably saved before starting and does not spend a ticket");
            Check(((ViewModels.MainViewModel)_mainWindow.DataContext).StatusText == "Gaming session", "Main window identifies the selected gaming mode");
            Capture(_mainWindow, Path.Combine(directory, "gaming-main.png"));
            Check(_overlay is { IsVisible: false }, "Gaming begins with no overlay");
            gamingClock.MonotonicNow = TimeSpan.FromMinutes(15);
            _session.Tick();
            Check(!_overlay!.IsVisible, "Gaming has no five-minute floating warning");
            gamingClock.MonotonicNow = TimeSpan.FromMinutes(19);
            _session.Tick();
            Check(!_overlay.IsVisible, "Gaming has no one-minute countdown");
            RecreateOverlay();
            Check(!_overlay!.IsVisible, "Recreating the overlay does not show an early gaming warning");
            gamingClock.MonotonicNow = TimeSpan.FromSeconds(1169);
            _session.Tick();
            Check(!_overlay.IsVisible, "Gaming stays hidden at thirty-one seconds remaining");
            gamingClock.MonotonicNow += TimeSpan.FromSeconds(1);
            _session.Tick();
            Check(_overlay.IsVisible && ((TextBlock)_overlay.FindName("BubbleText")).Text == "00:30", "Gaming countdown begins at exactly thirty seconds");
            var gamingImage = (System.Windows.Controls.Image)_overlay.FindName("PetImage");
            var gamingMotion = (TranslateTransform)gamingImage.RenderTransform;
            var gamingBubble = (Border)_overlay.FindName("Bubble");
            Check(gamingImage.Source is BitmapSource { IsFrozen: true } && !gamingMotion.HasAnimatedProperties && !_overlay.HasAnimatedProperties,
                "Gaming uses a cached frozen sprite with no movement, bobbing or opacity animation clocks");
            Check(gamingBubble.Effect is null && _overlay.Width < 400, "Gaming uses a compact surface without the bubble shadow effect");
            var gamingPosition = new System.Windows.Point(_overlay.Left, _overlay.Top);
            var gamingOffset = new System.Windows.Point(gamingMotion.X, gamingMotion.Y);
            await Task.Delay(2200);
            Check(gamingPosition == new System.Windows.Point(_overlay.Left, _overlay.Top) &&
                gamingOffset == new System.Windows.Point(gamingMotion.X, gamingMotion.Y) && _overlay.Opacity == 1,
                "Gaming companion remains stationary and fully opaque over time");
            Capture(_overlay, Path.Combine(directory, "gaming-countdown.png"));
            gamingClock.MonotonicNow += TimeSpan.FromSeconds(1);
            _session.Tick();
            Check(((TextBlock)_overlay.FindName("BubbleText")).Text == "00:29", "Gaming countdown updates once the next second elapses");
            _session.Suspend();
            Check(!_overlay.IsVisible, "Suspending gaming hides its countdown");
            gamingClock.MonotonicNow += TimeSpan.FromHours(1);
            _session.Resume();
            Check(_overlay.IsVisible && ((TextBlock)_overlay.FindName("BubbleText")).Text == "00:29", "Resuming restores the paused gaming countdown");
            await ApplySettingsAsync(Settings);
            gamingMotion = (TranslateTransform)((System.Windows.Controls.Image)_overlay!.FindName("PetImage")).RenderTransform;
            Check(_overlay.IsVisible && !gamingMotion.HasAnimatedProperties && _overlay.Width < 400 && _session.Mode == SessionMode.Gaming,
                "Saving settings retains gaming mode and its stationary countdown");
            OpenAnimationPreview();
            var previewMotion = (TranslateTransform)((System.Windows.Controls.Image)_previewOverlay!.FindName("PetImage")).RenderTransform;
            Check(!previewMotion.HasAnimatedProperties && !_previewOverlay.HasAnimatedProperties, "Explicit previews also stay stationary during gaming");
            _session.Tick();
            Check(_previewOverlay is null && _overlay.IsVisible, "A live gaming countdown replaces the preview without duplicate overlays");
            gamingClock.MonotonicNow += TimeSpan.FromSeconds(28);
            _session.Tick();
            Check(((TextBlock)_overlay.FindName("BubbleText")).Text == "00:01", "Gaming countdown reaches the final second");
            gamingClock.MonotonicNow += TimeSpan.FromSeconds(1);
            _session.Tick();
            await Task.Delay(250);
            Check(!_overlay.IsVisible && _restWindow is { IsVisible: true }, "Gaming transitions directly from countdown to locked rest");
            Check(_session.State == SessionState.MandatoryRestLocked && !_session.CompleteRest(), "Gaming retains the full mandatory rest lock");
            var gamingRestImage = (System.Windows.Controls.Image)_restWindow!.FindName("PetImage");
            Check(ReferenceEquals(gamingRestImage.Source, _petService.LoadFrame(_pet, "gaming-rest")) &&
                gamingRestImage.Source is BitmapSource { PixelWidth: 1280, PixelHeight: 720 },
                "Gaming rest displays the clean supplied landscape at its native resolution");
            Check(((TextBlock)_restWindow.FindName("HeadingText")).Foreground is SolidColorBrush { Color.R: 255 },
                "Gaming rest uses light text against its dark background");
            Capture(_restWindow, Path.Combine(directory, "gaming-rest.png"));
            var gamingRestCapture = new BitmapImage(new Uri(Path.Combine(directory, "gaming-rest.png")));
            Check(gamingRestCapture.PixelWidth == restScreen.Bounds.Width && gamingRestCapture.PixelHeight == restScreen.Bounds.Height,
                "Gaming rest covers the full physical monitor at the current Windows DPI");
            var gamingRestButton = (Button)_restWindow.FindName("RestedButton");
            gamingRestButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(_session.State == SessionState.MandatoryRestLocked, "Gaming Rested button cannot skip the minimum");
            gamingClock.MonotonicNow += TimeSpan.FromMinutes(2);
            _session.Tick();
            gamingRestButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(_restWindow is null && _session.Mode == SessionMode.Gaming && _session.State == SessionState.Working &&
                _session.Snapshot.Remaining == TimeSpan.FromMinutes(20) && !_overlay.IsVisible,
                "Rested starts a fresh twenty-minute gaming session with the overlay hidden");
            _session.BeginVoluntaryRest();
            Check(_restWindow is not null && ReferenceEquals(((System.Windows.Controls.Image)_restWindow.FindName("PetImage")).Source,
                _petService.LoadFrame(_pet, "gaming-rest")), "Early gaming breaks also use the gaming artwork");
            gamingClock.MonotonicNow += TimeSpan.FromMinutes(2);
            _session.Tick();
            _session.CompleteRest();
            ShowMainWindow();
            var switchButton = (Button)_mainWindow.FindName("SwitchModeButton");
            Check(switchButton.IsVisible && switchButton.Content.ToString() == "Switch to Work Mode", "Gaming exposes a clearly labeled switch to work button");
            var switchStart = gamingClock.MonotonicNow;
            var ticketsBeforeSwitch = _focusGuard.TicketsRemaining;
            gamingClock.MonotonicNow += TimeSpan.FromMinutes(15);
            _session.Tick();
            switchButton.Command!.Execute(null);
            Check(_session.Mode == SessionMode.Working && _session.Snapshot.Remaining == TimeSpan.FromMinutes(5) &&
                switchButton.Content.ToString() == "Switch to Game Mode", "Switch button changes mode and label without restarting time");
            Check(_overlay!.IsVisible && ((TranslateTransform)((System.Windows.Controls.Image)_overlay.FindName("PetImage")).RenderTransform).HasAnimatedProperties,
                "Switching to work in the five-minute window starts its floating warning");
            Check(_focusGuard.Recovery!.Snapshot.Mode == SessionMode.Working, "Mode switch is checkpointed immediately, within the five-second throttle");
            switchButton.Command.Execute(null);
            Check(!_overlay.IsVisible && _session.Mode == SessionMode.Gaming && _previewOverlay is null,
                "Switching to gaming immediately stops and hides a floating warning");
            Check(_focusGuard.Recovery!.Snapshot.Mode == SessionMode.Gaming && _focusGuard.TicketsRemaining == ticketsBeforeSwitch,
                "Switching back immediately persists gaming without spending or replenishing tickets");
            switchButton.Command.Execute(null);
            Check(!_overlay.IsVisible, "Switching back to work does not replay the old five-minute pass");
            gamingClock.MonotonicNow = switchStart + TimeSpan.FromMinutes(19);
            _session.Tick();
            Check(_overlay.IsVisible, "Work countdown still begins at one minute after a mode switch");
            switchButton.Command.Execute(null);
            Check(!_overlay.IsVisible, "Switching at one minute hides work countdown until gaming's last thirty seconds");
            gamingClock.MonotonicNow += TimeSpan.FromSeconds(30);
            _session.Tick();
            Check(_overlay.IsVisible && !((TranslateTransform)((System.Windows.Controls.Image)_overlay.FindName("PetImage")).RenderTransform).HasAnimatedProperties,
                "Switched gaming session starts its stationary countdown at thirty seconds");
            switchButton.Command.Execute(null);
            Check(_overlay.IsVisible && ((TranslateTransform)((System.Windows.Controls.Image)_overlay.FindName("PetImage")).RenderTransform).HasAnimatedProperties && _overlay.Width > 400,
                "Switching to work converts an active stationary countdown to the normal animated overlay");
            switchButton.Command.Execute(null);
            Check(_overlay.IsVisible && !((TranslateTransform)((System.Windows.Controls.Image)_overlay.FindName("PetImage")).RenderTransform).HasAnimatedProperties && _overlay.Width < 400,
                "Switching back converts the active countdown to a compact stationary overlay");
            OpenAnimationPreview();
            switchButton.Command.Execute(null);
            Check(_previewOverlay is null, "Switching modes closes any preview using the previous mode");
            Capture(_mainWindow, Path.Combine(directory, "mode-switch.png"));
            gamingClock.MonotonicNow += TimeSpan.FromSeconds(30);
            _session.Tick();
            Check(_session.State == SessionState.MandatoryRestLocked && !switchButton.Command.CanExecute(null),
                "Switching never delays the original break deadline, and is disabled during rest");
            Check(ReferenceEquals(((System.Windows.Controls.Image)_restWindow!.FindName("PetImage")).Source,
                _petService.LoadFrame(_pet, "resting")), "Next break uses the final selected work mode's background");
            _session.Stop();
            Check(_verificationMode, "UI verification uses an isolated ledger, never the user's tickets");
            _session!.Stop();
            for (var i = 0; i < 3; i++)
            {
                Check(_focusGuard!.TryUseEmergencyTicket(), $"Isolated emergency ticket {i + 1} can be spent");
                _focusGuard.BeginWork(_session.Options);
            }
            RefreshCommitmentUi();
            Check(_focusGuard!.TicketsRemaining == 0 && !_focusGuard.TryUseEmergencyTicket(), "Fourth weekly emergency exit is rejected");
            Check(!exitItem.Enabled && exitItem.Text?.Contains("0 left") == true, "Tray disables emergency exit when weekly tickets are exhausted");
            File.WriteAllLines(Path.Combine(directory, "results.txt"), results);
        }
        catch (Exception ex)
        {
            results.Add("FAIL " + ex);
            File.WriteAllLines(Path.Combine(directory, "results.txt"), results);
            Environment.ExitCode = 1;
        }
        finally { ExitApplication(); } // Isolated verification ledger, never real tickets.
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
