namespace TheEye.Core;

public sealed record SessionSnapshot(
    SessionState State,
    TimeSpan Remaining,
    bool FiveMinuteWarningRaised,
    bool OneMinuteWarningRaised,
    bool IsSuspended)
{
    public bool CanStartWorking => State == SessionState.Idle;

    public bool CanBeginVoluntaryRest => State == SessionState.Working;

    public bool CanCompleteRest =>
        State is SessionState.VoluntaryRest or SessionState.MandatoryRestComplete;
}
