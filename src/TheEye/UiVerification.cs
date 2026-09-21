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
            ShowMainWindow();
            await Task.Delay(300);
            Capture(_mainWindow!, Path.Combine(directory, "main.png"));
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
            var transform = (TranslateTransform)image.RenderTransform;
            var before = transform.X;
            await Task.Delay(1800);
            Check(Math.Abs(before - transform.X) > 1, "Floating sprite actually travels horizontally");
            Check(image.ActualHeight + 12 < _previewOverlay.ActualHeight, "Sprite and hat fit inside overlay");
            Capture(_previewOverlay, Path.Combine(directory, "taskbar-overlay.png"));
            StopPreview();
            _session!.StartWorking();
            Check(_overlay is { IsVisible: false }, "No companion during ordinary work");
            OpenAnimationPreview();
            StopPreview();
            Check(_overlay is { IsVisible: false }, "Ending preview does not enable constant floating");
            RecreateOverlay();
            Check(_overlay is { IsVisible: false }, "Changing settings does not enable constant floating");
            _session.BeginVoluntaryRest();
            await Task.Delay(300);
            Check(_mainWindow!.WindowState == WindowState.Minimized, "Main window minimizes for rest");
            Check(_restWindow is { IsVisible: true }, "Rest window opens");
            var restImage = (System.Windows.Controls.Image)_restWindow!.FindName("PetImage");
            Check(restImage.Source is BitmapSource { PixelWidth: 1920, PixelHeight: 1080 }, "Rest loads full 1920x1080 artwork");
            Capture(_restWindow, Path.Combine(directory, "rest.png"));
            _session.CompleteRest();
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
