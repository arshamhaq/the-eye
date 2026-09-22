namespace TheEye.Core;

public sealed class SessionManager
{
    private readonly IClock _clock;
    private SessionOptions _options;
    private TimeSpan _deadline;
    private TimeSpan _suspendedRemaining;
    private bool _fiveMinuteWarningRaised;
    private bool _oneMinuteWarningRaised;
    private bool _isSuspended;

    public SessionManager(IClock clock, SessionOptions? options = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _options = options ?? SessionOptions.Default;
        _options.Validate();
    }

    public SessionState State { get; private set; } = SessionState.Idle;

    public SessionOptions Options => _options;

    public SessionSnapshot Snapshot => CreateSnapshot();

    public event Action<SessionSnapshot>? SnapshotChanged;

    public event Action<SessionWarning>? WarningRaised;

    public void UpdateOptions(SessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        PublishSnapshot();
    }

    public bool StartWorking()
    {
        if (State is not (SessionState.Idle or SessionState.VoluntaryRest or SessionState.MandatoryRestComplete))
        {
            return false;
        }

        _fiveMinuteWarningRaised = false;
        _oneMinuteWarningRaised = false;
        _isSuspended = false;
        _deadline = _clock.MonotonicNow + _options.WorkDuration;
        TransitionTo(SessionState.Working);
        return true;
    }

    public bool BeginVoluntaryRest()
    {
        if (State != SessionState.Working)
        {
            return false;
        }

        // Choosing an early break uses the same minimum and completion guard
        // as reaching the work deadline; it is not a shortcut around rest.
        EnterMandatoryRest();
        return true;
    }

    public bool CompleteRest()
    {
        if (State is not (SessionState.VoluntaryRest or SessionState.MandatoryRestComplete))
        {
            return false;
        }

        return StartWorking();
    }

    public void Stop()
    {
        _fiveMinuteWarningRaised = false;
        _oneMinuteWarningRaised = false;
        _isSuspended = false;
        TransitionTo(SessionState.Idle);
    }

    public void Tick()
    {
        if (_isSuspended)
        {
            return;
        }

        switch (State)
        {
            case SessionState.Working:
                TickWorking();
                break;
            case SessionState.MandatoryRestLocked:
                TickMandatoryRest();
                break;
            default:
                break;
        }
    }

    public void Suspend()
    {
        if (_isSuspended)
        {
            return;
        }

        if (State == SessionState.Working)
        {
            _suspendedRemaining = RemainingUntilDeadline();
            _isSuspended = true;
            PublishSnapshot();
        }
    }

    public void Resume()
    {
        if (!_isSuspended)
        {
            Tick();
            return;
        }

        _deadline = _clock.MonotonicNow + _suspendedRemaining;
        _isSuspended = false;
        PublishSnapshot();
    }

    private void TickWorking()
    {
        var remaining = RemainingUntilDeadline();
        if (remaining <= TimeSpan.Zero)
        {
            EnterMandatoryRest();
            return;
        }

        if (_options.OneMinuteWarningEnabled &&
            !_oneMinuteWarningRaised &&
            remaining <= _options.OneMinuteWarningThreshold)
        {
            _fiveMinuteWarningRaised = true;
            _oneMinuteWarningRaised = true;
            WarningRaised?.Invoke(SessionWarning.OneMinuteRemaining);
        }
        else if (_options.FiveMinuteWarningEnabled &&
                 !_fiveMinuteWarningRaised &&
                 remaining <= _options.FiveMinuteWarningThreshold)
        {
            _fiveMinuteWarningRaised = true;
            WarningRaised?.Invoke(SessionWarning.FiveMinutesRemaining);
        }

        PublishSnapshot();
    }

    private void EnterMandatoryRest()
    {
        _isSuspended = false;
        if (_options.StrictModeEnabled && _options.MandatoryRestDuration > TimeSpan.Zero)
        {
            _deadline = _clock.MonotonicNow + _options.MandatoryRestDuration;
            TransitionTo(SessionState.MandatoryRestLocked);
        }
        else
        {
            TransitionTo(SessionState.MandatoryRestComplete);
        }
    }

    private void TickMandatoryRest()
    {
        if (RemainingUntilDeadline() <= TimeSpan.Zero)
        {
            TransitionTo(SessionState.MandatoryRestComplete);
            return;
        }

        PublishSnapshot();
    }

    private void TransitionTo(SessionState state)
    {
        State = state;
        PublishSnapshot();
    }

    private SessionSnapshot CreateSnapshot()
    {
        var remaining = State is SessionState.Working or SessionState.MandatoryRestLocked
            ? RemainingUntilDeadline()
            : TimeSpan.Zero;

        return new SessionSnapshot(
            State,
            remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining,
            _fiveMinuteWarningRaised,
            _oneMinuteWarningRaised,
            _isSuspended);
    }

    private TimeSpan RemainingUntilDeadline() => _deadline - _clock.MonotonicNow;

    private void PublishSnapshot() => SnapshotChanged?.Invoke(CreateSnapshot());
}
