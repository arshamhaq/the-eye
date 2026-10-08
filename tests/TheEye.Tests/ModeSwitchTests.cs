using TheEye.Core;

namespace TheEye.Tests;

public sealed class ModeSwitchTests
{
    [Theory]
    [InlineData(SessionMode.Working)]
    [InlineData(SessionMode.Gaming)]
    public void SwitchingBothWaysPreservesDeadlineAndDoesNotStartAnotherCommitment(SessionMode initial)
    {
        var clock = new FakeClock();
        var starts = 0;
        var session = new SessionManager(clock) { BeforeFirstStart = () => { starts++; return true; } };
        if (initial == SessionMode.Gaming) session.StartGaming(); else session.StartWorking();
        clock.Advance(TimeSpan.FromMinutes(7));
        var remaining = session.Snapshot.Remaining;
        Assert.True(session.SwitchMode(initial == SessionMode.Gaming ? SessionMode.Working : SessionMode.Gaming));
        Assert.Equal(remaining, session.Snapshot.Remaining);
        Assert.True(session.SwitchMode(initial));
        Assert.Equal(remaining, session.Snapshot.Remaining);
        Assert.Equal(1, starts);
        clock.Advance(remaining);
        session.Tick();
        Assert.Equal(SessionState.MandatoryRestLocked, session.State);
        Assert.Equal(TimeSpan.FromMinutes(2), session.Snapshot.Remaining);
    }

    [Theory]
    [InlineData(900, SessionWarning.FiveMinutesRemaining)]
    [InlineData(1140, SessionWarning.OneMinuteRemaining)]
    [InlineData(1190, SessionWarning.OneMinuteRemaining)]
    public void SwitchingToWorkEmitsTheDueWarningOnce(int elapsed, SessionWarning expected)
    {
        var clock = new FakeClock();
        var session = new SessionManager(clock);
        var warnings = new List<SessionWarning>();
        session.WarningRaised += warnings.Add;
        session.StartGaming();
        clock.Advance(TimeSpan.FromSeconds(elapsed));
        Assert.True(session.SwitchMode(SessionMode.Working));
        session.Tick();
        Assert.Equal([expected], warnings);
        session.SwitchMode(SessionMode.Gaming);
        warnings.Clear();
        session.SwitchMode(SessionMode.Working);
        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData(60, false)]
    [InlineData(31, false)]
    [InlineData(30, true)]
    [InlineData(1, true)]
    public void SwitchingToGamingHidesWorkCountdownUntilItsOwnBoundary(int remaining, bool visible)
    {
        var clock = new FakeClock();
        var session = new SessionManager(clock);
        session.StartWorking();
        clock.Advance(TimeSpan.FromSeconds(1200 - remaining));
        session.Tick();
        Assert.True(session.Snapshot.IsCountdownVisible);
        var warnings = new List<SessionWarning>();
        session.WarningRaised += warnings.Add;
        session.SwitchMode(SessionMode.Gaming);
        Assert.Equal(visible, session.Snapshot.IsCountdownVisible);
        Assert.Equal(TimeSpan.FromSeconds(remaining), session.Snapshot.Remaining);
        if (visible) Assert.Equal([SessionWarning.ThirtySecondsRemaining], warnings);
        else Assert.Empty(warnings);
    }

    [Fact]
    public void SwitchingWhileSuspendedKeepsPausedTimeAndSuppressesWarningsUntilResume()
    {
        var clock = new FakeClock();
        var session = new SessionManager(clock);
        session.StartWorking();
        clock.Advance(TimeSpan.FromSeconds(1175));
        session.Suspend();
        clock.Advance(TimeSpan.FromHours(2));
        var warnings = new List<SessionWarning>();
        session.WarningRaised += warnings.Add;
        session.SwitchMode(SessionMode.Gaming);
        Assert.True(session.Snapshot.IsSuspended);
        Assert.Equal(TimeSpan.FromSeconds(25), session.Snapshot.Remaining);
        Assert.False(session.Snapshot.IsCountdownVisible);
        Assert.Empty(warnings);
        session.Resume();
        session.Tick();
        Assert.True(session.Snapshot.IsCountdownVisible);
        Assert.Equal(TimeSpan.FromSeconds(25), session.Snapshot.Remaining);
    }

    [Fact]
    public void SwitchingCannotStartIdleOrBypassRestOrAnExpiredDeadline()
    {
        var clock = new FakeClock();
        var session = new SessionManager(clock);
        Assert.False(session.SwitchMode(SessionMode.Gaming));
        session.StartWorking();
        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.False(session.SwitchMode(SessionMode.Gaming));
        Assert.Equal(SessionMode.Working, session.Mode);
        Assert.Equal(SessionState.MandatoryRestLocked, session.State);
        Assert.False(session.SwitchMode(SessionMode.Gaming));
        clock.Advance(TimeSpan.FromMinutes(2));
        session.Tick();
        Assert.False(session.SwitchMode(SessionMode.Gaming));
        Assert.True(session.CompleteRest());
        Assert.Equal(SessionMode.Working, session.Mode);
    }

    [Fact]
    public void NewModeSurvivesRestAndRecoveryAndUsesWorkWarningSettings()
    {
        var clock = new FakeClock();
        var session = new SessionManager(clock, SessionOptions.Default with { FiveMinuteWarningEnabled = false, OneMinuteWarningEnabled = false });
        session.StartGaming();
        clock.Advance(TimeSpan.FromMinutes(19));
        var warnings = new List<SessionWarning>();
        session.WarningRaised += warnings.Add;
        session.SwitchMode(SessionMode.Working);
        Assert.Empty(warnings);
        Assert.False(session.Snapshot.IsCountdownVisible);
        session.SwitchMode(SessionMode.Gaming);
        var restored = new SessionManager(clock);
        restored.Restore(session.Snapshot, TimeSpan.Zero);
        Assert.Equal(SessionMode.Gaming, restored.Mode);
        restored.BeginVoluntaryRest();
        clock.Advance(TimeSpan.FromMinutes(2));
        restored.Tick();
        restored.CompleteRest();
        Assert.Equal(SessionMode.Gaming, restored.Mode);
    }

    [Fact]
    public void SameModeIsANoopAndInvalidModesAreRejected()
    {
        var session = new SessionManager(new FakeClock());
        session.StartWorking();
        var changes = 0;
        session.SnapshotChanged += _ => changes++;
        Assert.False(session.SwitchMode(SessionMode.Working));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SwitchMode((SessionMode)42));
        Assert.Equal(0, changes);
        Assert.Equal(SessionMode.Working, session.Mode);
    }
}
