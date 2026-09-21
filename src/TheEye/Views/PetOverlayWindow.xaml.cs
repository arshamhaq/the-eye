using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TheEye.Core;
using TheEye.Services;
using TheEye.ViewModels;

namespace TheEye.Views;

public partial class PetOverlayWindow : Window
{
    private readonly PetService _petService;
    private readonly PetDefinition _pet;
    private readonly AppSettings _settings;
    private readonly TranslateTransform _motion = new();
    private bool _running;
    private readonly DispatcherTimer _warningTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    private int _motionGeneration;
    private bool _finitePass;
    public PetOverlayWindow(PetService petService, PetDefinition pet, AppSettings settings)
    {
        InitializeComponent();
        _petService = petService;
        _pet = pet;
        _settings = settings;
        PetImage.RenderTransform = _motion;
        Bubble.RenderTransform = _motion;
        _warningTimer.Tick += (_, _) =>
        {
            _warningTimer.Stop();
            if (_settings.AnimationEnabled) Bubble.Visibility = Visibility.Collapsed;
            else HidePet();
        };
        Closed += (_, _) => StopMotion();
    }
    public void ShowWorkingCompanion()
    {
        Bubble.Visibility = Visibility.Collapsed;
        EnsureVisible();
    }
    public void ShowWarning(string message, string animation, bool autoHide)
    {
        if (autoHide || _finitePass) StopMotion();
        EnsureVisible(autoHide);
        BubbleText.Text = message;
        Bubble.Visibility = Visibility.Visible;
        if (autoHide) _warningTimer.Start();
    }
    public void ShowCountdown(TimeSpan remaining) => ShowWarning(MainViewModel.FormatRemaining(remaining), "countdown", false);
    private void EnsureVisible(bool finitePass = false)
    {
        if (_running && IsVisible) return;
        PetImage.Source = _petService.LoadFrame(_pet, "walk")
            ?? throw new InvalidOperationException("The floating sprite is missing.");
        if (!IsVisible) Show();
        PositionAtTaskbar();
        UpdateLayout();
        _running = true;
        _finitePass = finitePass;
        var travel = Math.Max(0, ActualWidth - PetImage.Width - 48);
        _motion.X = travel;
        _motion.Y = 0;
        Opacity = 1;
        if (!_settings.AnimationEnabled) return;
        var speed = Math.Clamp(_settings.AnimationSpeed, 0.5, 2);
        var movement = new DoubleAnimation(travel, 24, TimeSpan.FromSeconds(18 / speed))
        {
            AutoReverse = true,
            RepeatBehavior = finitePass ? new RepeatBehavior(1) : RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        var generation = ++_motionGeneration;
        if (finitePass) movement.Completed += (_, _) => { if (_motionGeneration == generation) HidePet(); };
        _motion.BeginAnimation(TranslateTransform.XProperty, movement);
        _motion.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -12, TimeSpan.FromSeconds(2.8 / speed))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.72, TimeSpan.FromSeconds(4.5 / speed))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }
    public void HidePet() { StopMotion(); Hide(); }
    private void StopMotion()
    {
        _motionGeneration++;
        _warningTimer.Stop();
        _finitePass = false;
        _running = false;
        _motion.BeginAnimation(TranslateTransform.XProperty, null);
        _motion.BeginAnimation(TranslateTransform.YProperty, null);
        BeginAnimation(OpacityProperty, null);
    }
    private void PositionAtTaskbar()
    {
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var handle = new WindowInteropHelper(this).Handle;
        // Move onto the target monitor before reading its DPI.
        SetWindowPos(handle, new IntPtr(-1), screen.WorkingArea.Left, screen.WorkingArea.Top, 0, 0, 0x0011);
        var scale = GetDpiForWindow(handle) / 96.0;
        var requested = _pet.DefaultHeight * _settings.PetScale;
        var height = Math.Min(requested, screen.WorkingArea.Height / scale * 0.48);
        PetImage.Height = height;
        PetImage.Width = height * PetImage.Source.Width / PetImage.Source.Height;
        Width = screen.WorkingArea.Width / scale;
        Height = height + 80;
        var physicalHeight = (int)Math.Ceiling(Height * scale);
        SetWindowPos(handle, new IntPtr(-1), screen.WorkingArea.Left,
            screen.WorkingArea.Bottom - physicalHeight, screen.WorkingArea.Width, physicalHeight, 0x0050);
    }
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, -20).ToInt64();
        SetWindowLongPtr(handle, -20, new IntPtr(style | 0x08000000 | 0x80 | 0x20));
    }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
