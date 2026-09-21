using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EyeDragon.Core;
using EyeDragon.Services;
using EyeDragon.ViewModels;
using Forms = System.Windows.Forms;

namespace EyeDragon.Views;

public partial class PetOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private readonly PetService _petService;
    private readonly PetDefinition _pet;
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _hideTimer;
    private string _animation = "idle";
    private int _frameIndex;

    public PetOverlayWindow(PetService petService, PetDefinition pet, AppSettings settings)
    {
        InitializeComponent();
        _petService = petService;
        _pet = pet;
        PetImage.Width = pet.DefaultWidth * settings.PetScale;
        PetImage.Height = pet.DefaultHeight * settings.PetScale;
        _animationTimer = new DispatcherTimer(DispatcherPriority.Background);
        _animationTimer.Tick += (_, _) => AdvanceFrame();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _hideTimer.Tick += (_, _) => HidePet();
    }

    public void ShowWarning(string message, string animation, bool autoHide)
    {
        BubbleText.Text = message;
        Bubble.Visibility = Visibility.Visible;
        StartAnimation(animation);
        PositionNearTaskbar();
        if (!IsVisible)
        {
            Show();
        }

        if (autoHide)
        {
            _hideTimer.Stop();
            _hideTimer.Start();
        }
    }

    public void ShowCountdown(TimeSpan remaining)
    {
        ShowWarning(MainViewModel.FormatRemaining(remaining), "countdown", autoHide: false);
    }

    public void HidePet()
    {
        _hideTimer.Stop();
        _animationTimer.Stop();
        Hide();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style | WsExNoActivate | WsExToolWindow));
    }

    private void StartAnimation(string animation)
    {
        _animation = _pet.Animations.ContainsKey(animation) ? animation : "idle";
        _frameIndex = 0;
        UpdateFrame();
        _animationTimer.Stop();
        if (_pet.Animations.TryGetValue(_animation, out var sequence) && sequence.Frames.Count > 1)
        {
            _animationTimer.Interval = TimeSpan.FromSeconds(1 / Math.Clamp(sequence.FramesPerSecond, 1, 15));
            _animationTimer.Start();
        }
    }

    private void AdvanceFrame()
    {
        _frameIndex++;
        UpdateFrame();
    }

    private void UpdateFrame() => PetImage.Source = _petService.LoadFrame(_pet, _animation, _frameIndex);

    private void PositionNearTaskbar()
    {
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = screen.WorkingArea.Right / dpi.DpiScaleX - Width - 20;
        Top = screen.WorkingArea.Bottom / dpi.DpiScaleY - Height;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
