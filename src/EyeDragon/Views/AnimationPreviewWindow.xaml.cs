using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EyeDragon.Core;
using EyeDragon.Services;

namespace EyeDragon.Views;

public partial class AnimationPreviewWindow : Window
{
    private readonly PetService _petService;
    private readonly PetDefinition _pet;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;
    private string _animation = "idle";
    private int _frameIndex;
    private bool _playing = true;

    public AnimationPreviewWindow(PetService petService, PetDefinition pet, AppSettings settings)
    {
        InitializeComponent();
        _petService = petService;
        _pet = pet;
        _settings = settings;
        _timer = new DispatcherTimer(DispatcherPriority.Background);
        _timer.Tick += (_, _) => Step(1);
        AnimationPicker.ItemsSource = pet.Animations.Keys.Order(StringComparer.OrdinalIgnoreCase);
        AnimationPicker.SelectedItem = pet.Animations.ContainsKey("idle") ? "idle" : pet.Animations.Keys.FirstOrDefault();
        Closed += (_, _) => _timer.Stop();
    }

    private void AnimationPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AnimationPicker.SelectedItem is not string animation)
        {
            return;
        }

        _animation = animation;
        _frameIndex = 0;
        ConfigureTimer();
        UpdateFrame();
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        _playing = !_playing;
        PlayPauseButton.Content = _playing ? "Pause" : "Play";
        ConfigureTimer();
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e) => Step(-1);

    private void NextButton_Click(object sender, RoutedEventArgs e) => Step(1);

    private void Step(int amount)
    {
        if (!_pet.Animations.TryGetValue(_animation, out var sequence) || sequence.Frames.Count == 0)
        {
            return;
        }

        _frameIndex = (_frameIndex + amount + sequence.Frames.Count) % sequence.Frames.Count;
        UpdateFrame();
    }

    private void ConfigureTimer()
    {
        _timer.Stop();
        if (_playing && _settings.AnimationEnabled && _pet.Animations.TryGetValue(_animation, out var sequence) && sequence.Frames.Count > 1)
        {
            var fps = Math.Clamp(sequence.FramesPerSecond * _settings.AnimationSpeed, 1, 15);
            _timer.Interval = TimeSpan.FromSeconds(1 / fps);
            _timer.Start();
        }
    }

    private void UpdateFrame() => PreviewImage.Source = _petService.LoadFrame(_pet, _animation, _frameIndex);
}
