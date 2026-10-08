using TheEye.Core;

namespace TheEye.Tests;

public sealed class GamingSessionTests
{
    [Fact]
    public void GamingIsSilentUntilThirtySecondsThenCountsDownToSameRestMinimum()
    {
        var clock = new FakeClock();
        var session = new SessionManager(clock);
        var warnings = new List<SessionWarning>();
        session.WarningRaised += warnings.Add;
        Assert.True(session.StartGaming());
        Assert.Equal(SessionMode.Gaming, session.Snapshot.Mode);
        Assert.Equal(TimeSpan.FromMinutes(20), session.Snapshot.Remaining);

        for (var elapsed = 0; elapsed < 1170; elapsed++)
        {
            session.Tick();
            Assert.Empty(warnings);
            Assert.False(session.Snapshot.IsCountdownVisible);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        for (var remaining = 30; remaining > 0; remaining--)
        {
            session.Tick();
            session.Tick(); // duplicate ticks must not repeat a warning/sound
            Assert.Equal([SessionWarning.ThirtySecondsRemaining], warnings);
            Assert.True(session.Snapshot.IsCountdownVisible);
            Assert.Equal(TimeSpan.FromSeconds(remaining), session.Snapshot.Remaining);
            Assert.False(session.Snapshot.FiveMinuteWarningRaised);
            Assert.False(session.Snapshot.OneMinuteWarningRaised);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        session.Tick();
        Assert.False(session.Snapshot.IsCountdownVisible);
        Assert.Equal(SessionState.MandatoryRestLocked, session.State);
        Assert.Equal(TimeSpan.FromMinutes(2), session.Snapshot.Remaining);
        Assert.False(session.CompleteRest());
        Assert.False(session.StartWorking());
        Assert.False(session.StartGaming());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AutomaticAndEarlyBreaksKeepGamingForTheNextFullSession(bool early)
    {
        var clock = new FakeClock();
        var session = new SessionManager(clock);
        session.StartGaming();
        if (early) Assert.True(session.BeginVoluntaryRest());
        else
        {
            clock.Advance(TimeSpan.FromMinutes(20));
            session.Tick();
        }
        clock.Advance(TimeSpan.FromSeconds(119));
        session.Tick();
        Assert.False(session.CompleteRest());
        clock.Advance(TimeSpan.FromSeconds(1));
        session.Tick();
        Assert.True(session.CompleteRest());
        Assert.Equal(SessionMode.Gaming, session.Mode);
        Assert.Equal(TimeSpan.FromMinutes(20), session.Snapshot.Remaining);
        Assert.False(session.Snapshot.ThirtySecondWarningRaised);
        Assert.False(session.Snapshot.IsCountdownVisible);
    }

    [Fact]
    public void ModeCannotChangeOrRestartTheTimerDuringAnActiveSession()
    {
        var clock = new FakeClock();
        var session = new SessionManager(clock);
        session.StartGaming();
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.False(session.StartWorking());
        Assert.False(session.StartGaming());
        Assert.Equal(SessionMode.Gaming, session.Mode);
        Assert.Equal(TimeSpan.FromMinutes(15), session.Snapshot.Remaining);
    }

    [Fact]
    public void WorkReminderSettingsDoNotSuppressGamingCountdownOrChangeMode()
    {
        var clock = new FakeClock();
        var session = new SessionManager(clock);
        session.StartGaming();
        session.UpdateOptions(SessionOptions.Default with { FiveMinuteWarningEnabled = false, OneMinuteWarningEnabled = false });
        clock.Advance(TimeSpan.FromSeconds(1170));
        session.Tick();
        Assert.Equal(SessionMode.Gaming, session.Mode);
        Assert.True(session.Snapshot.IsCountdownVisible);
        Assert.Equal(TimeSpan.FromSeconds(30), session.Snapshot.Remaining);
    }

    [Fact]
    public void SuspendHidesAndPausesGamingCountdownThenResumeRestoresIt()
    {
        var clock = new FakeClock();
        var session = new SessionManager(clock);
        session.StartGaming();
        clock.Advance(TimeSpan.FromSeconds(1170));
        session.Tick();
        session.Suspend();
        Assert.False(session.Snapshot.IsCountdownVisible);
        clock.Advance(TimeSpan.FromHours(1));
        session.Tick();
        session.Resume();
        Assert.True(session.Snapshot.IsCountdownVisible);
        Assert.Equal(TimeSpan.FromSeconds(30), session.Snapshot.Remaining);
    }

    [Theory]
    [InlineData(1180, SessionState.Working, 20)]
    [InlineData(1200, SessionState.MandatoryRestLocked, 120)]
    public void RecoveryPreservesGamingAndHandlesMissedWarningsOrDeadline(int elapsed, SessionState state, int remaining)
    {
        var original = new SessionManager(new FakeClock());
        original.StartGaming();
        var recovered = new SessionManager(new FakeClock());
        var warnings = new List<SessionWarning>();
        recovered.WarningRaised += warnings.Add;
        recovered.Restore(original.Snapshot, TimeSpan.FromSeconds(elapsed));
        Assert.Equal(SessionMode.Gaming, recovered.Mode);
        Assert.Equal(state, recovered.State);
        Assert.Equal(TimeSpan.FromSeconds(remaining), recovered.Snapshot.Remaining);
        if (state == SessionState.Working) Assert.Equal([SessionWarning.ThirtySecondsRemaining], warnings);
        else Assert.Empty(warnings);
    }

    [Fact]
    public void RestoringAnActiveCountdownDoesNotReplayItsWarning()
    {
        var clock = new FakeClock();
        var original = new SessionManager(clock);
        original.StartGaming();
        clock.Advance(TimeSpan.FromSeconds(1170));
        original.Tick();
        var recovered = new SessionManager(new FakeClock());
        var warnings = new List<SessionWarning>();
        recovered.WarningRaised += warnings.Add;
        recovered.Restore(original.Snapshot, TimeSpan.FromSeconds(5));
        Assert.True(recovered.Snapshot.IsCountdownVisible);
        Assert.Equal(TimeSpan.FromSeconds(25), recovered.Snapshot.Remaining);
        Assert.Empty(warnings);
    }

    [Fact]
    public void GamingStartRequiresSuccessfulCommitmentPersistence()
    {
        var session = new SessionManager(new FakeClock());
        session.BeforeFirstStart = () => { Assert.Equal(SessionMode.Gaming, session.Mode); return false; };
        Assert.False(session.StartGaming());
        Assert.Equal(SessionState.Idle, session.State);
        Assert.Equal(SessionMode.Working, session.Mode);
    }

    [Fact]
    public void DevelopmentTimersShowThirtySecondsImmediately()
    {
        var session = new SessionManager(new FakeClock(), SessionOptions.Development);
        session.StartGaming();
        Assert.True(session.Snapshot.IsCountdownVisible);
        Assert.Equal(TimeSpan.FromSeconds(30), session.Snapshot.Remaining);
    }
}
