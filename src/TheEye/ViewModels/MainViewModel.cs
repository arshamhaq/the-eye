using System.Windows;
using System.Windows.Media.Imaging;
using TheEye.Core;

namespace TheEye.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly SessionManager _session;

    public MainViewModel(SessionManager session, BitmapSource? petImage, BitmapSource? companionImage)
    {
        _session = session;
        PetImage = petImage;
        CompanionImage = companionImage;
        StartWorkingCommand = new RelayCommand(() => _session.StartWorking(), () => IsIdle);
        StartGamingCommand = new RelayCommand(() => _session.StartGaming(), () => IsIdle);
        SwitchModeCommand = new RelayCommand(() => _session.SwitchMode(
            _session.Mode == SessionMode.Gaming ? SessionMode.Working : SessionMode.Gaming), () => IsWorking);
        RestingNowCommand = new RelayCommand(() => _session.BeginVoluntaryRest(), () => IsWorking);
        RestedCommand = new RelayCommand(() => _session.CompleteRest(), () => CanCompleteRest);
        OpenSettingsCommand = new RelayCommand(() => App.CurrentApp.OpenSettings());
        HideCommand = new RelayCommand(() => App.CurrentApp.HideMainWindow());
        _session.SnapshotChanged += OnSnapshotChanged;
    }

    public BitmapSource? PetImage { get; }
    public BitmapSource? CompanionImage { get; }

    public RelayCommand StartWorkingCommand { get; }

    public RelayCommand StartGamingCommand { get; }

    public RelayCommand SwitchModeCommand { get; }

    public string SwitchModeText => _session.Mode == SessionMode.Gaming ? "Switch to Work Mode" : "Switch to Game Mode";

    public RelayCommand RestingNowCommand { get; }

    public RelayCommand RestedCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand HideCommand { get; }

    public bool IsIdle => _session.State == SessionState.Idle;

    public bool IsWorking => _session.State == SessionState.Working;

    public bool IsVoluntaryRest => _session.State == SessionState.VoluntaryRest;

    public bool CanCompleteRest => _session.Snapshot.CanCompleteRest;

    public Visibility IdleVisibility => IsIdle ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WorkingVisibility => IsWorking ? Visibility.Visible : Visibility.Collapsed;

    public Visibility VoluntaryRestVisibility => IsVoluntaryRest ? Visibility.Visible : Visibility.Collapsed;

    public string RemainingText => FormatRemaining(_session.Snapshot.Remaining);

    public string SessionHeading => _session.Mode == SessionMode.Gaming ? "TIME TO PLAY" : "TIME TO FOCUS";

    public string StatusText => _session.Snapshot.IsSuspended ? "Paused while Windows is away"
        : _session.Mode == SessionMode.Gaming ? "Gaming session" : "Work session";

    private void OnSnapshotChanged(SessionSnapshot snapshot)
    {
        RaisePropertyChanged(nameof(IsIdle));
        RaisePropertyChanged(nameof(IsWorking));
        RaisePropertyChanged(nameof(IsVoluntaryRest));
        RaisePropertyChanged(nameof(CanCompleteRest));
        RaisePropertyChanged(nameof(IdleVisibility));
        RaisePropertyChanged(nameof(WorkingVisibility));
        RaisePropertyChanged(nameof(VoluntaryRestVisibility));
        RaisePropertyChanged(nameof(RemainingText));
        RaisePropertyChanged(nameof(StatusText));
        RaisePropertyChanged(nameof(SessionHeading));
        RaisePropertyChanged(nameof(SwitchModeText));
        StartWorkingCommand.RaiseCanExecuteChanged();
        StartGamingCommand.RaiseCanExecuteChanged();
        SwitchModeCommand.RaiseCanExecuteChanged();
        RestingNowCommand.RaiseCanExecuteChanged();
        RestedCommand.RaiseCanExecuteChanged();
    }

    public static string FormatRemaining(TimeSpan remaining)
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}
