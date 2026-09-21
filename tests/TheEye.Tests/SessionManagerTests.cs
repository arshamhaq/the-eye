using TheEye.Core;

namespace TheEye.Tests;

public sealed class SessionManagerTests
{
    private static readonly SessionOptions Options = new(
        TimeSpan.FromMinutes(20),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(1));

    [Fact]
    public void StartsIdleAndExplicitStartBeginsFullWorkSession()
    {
        var (manager, _) = Create();

        Assert.Equal(SessionState.Idle, manager.State);
        Assert.True(manager.StartWorking());
        Assert.Equal(SessionState.Working, manager.State);
        Assert.Equal(TimeSpan.FromMinutes(20), manager.Snapshot.Remaining);
    }

    [Fact]
    public void FiveMinuteWarningFiresOnceAtBoundary()
    {
        var (manager, clock) = Create();
        var warnings = new List<SessionWarning>();
        manager.WarningRaised += warnings.Add;
        manager.StartWorking();

        clock.Advance(TimeSpan.FromMinutes(15));
        manager.Tick();
        manager.Tick();

        Assert.Equal([SessionWarning.FiveMinutesRemaining], warnings);
    }

    [Fact]
    public void OneMinuteWarningFiresOnceAndSuppressesLateFiveMinuteWarning()
    {
        var (manager, clock) = Create();
        var warnings = new List<SessionWarning>();
        manager.WarningRaised += warnings.Add;
        manager.StartWorking();

        clock.Advance(TimeSpan.FromMinutes(19));
        manager.Tick();
        manager.Tick();

        Assert.Equal([SessionWarning.OneMinuteRemaining], warnings);
    }

    [Fact]
    public void WorkDeadlineTransitionsToMandatoryRest()
    {
        var (manager, clock) = Create();
        manager.StartWorking();

        clock.Advance(TimeSpan.FromMinutes(20));
        manager.Tick();

        Assert.Equal(SessionState.MandatoryRestLocked, manager.State);
        Assert.Equal(TimeSpan.FromMinutes(2), manager.Snapshot.Remaining);
    }

    [Fact]
    public void MandatoryRestCannotCompleteEarly()
    {
        var (manager, clock) = Create();
        EnterMandatoryRest(manager, clock);

        clock.Advance(TimeSpan.FromSeconds(119));
        manager.Tick();

        Assert.False(manager.CompleteRest());
        Assert.Equal(SessionState.MandatoryRestLocked, manager.State);
    }

    [Fact]
    public void CompletedMandatoryRestStartsFreshSessionAfterRested()
    {
        var (manager, clock) = Create();
        EnterMandatoryRest(manager, clock);
        clock.Advance(TimeSpan.FromMinutes(2));
        manager.Tick();

        Assert.Equal(SessionState.MandatoryRestComplete, manager.State);
        Assert.True(manager.CompleteRest());
        Assert.Equal(TimeSpan.FromMinutes(20), manager.Snapshot.Remaining);
    }

    [Fact]
    public void VoluntaryRestCancelsWorkAndRestedStartsFreshSession()
    {
        var (manager, clock) = Create();
        manager.StartWorking();
        clock.Advance(TimeSpan.FromMinutes(5));
        manager.Tick();

        Assert.True(manager.BeginVoluntaryRest());
        Assert.Equal(SessionState.VoluntaryRest, manager.State);
        Assert.True(manager.CompleteRest());
        Assert.Equal(TimeSpan.FromMinutes(20), manager.Snapshot.Remaining);
    }

    [Fact]
    public void CancelledSessionDoesNotEmitStaleWarnings()
    {
        var (manager, clock) = Create();
        var warnings = new List<SessionWarning>();
        manager.WarningRaised += warnings.Add;
        manager.StartWorking();
        manager.BeginVoluntaryRest();

        clock.Advance(TimeSpan.FromMinutes(30));
        manager.Tick();

        Assert.Empty(warnings);
    }

    [Fact]
    public void NewSessionResetsWarningFlags()
    {
        var (manager, clock) = Create();
        manager.StartWorking();
        clock.Advance(TimeSpan.FromMinutes(15));
        manager.Tick();
        manager.BeginVoluntaryRest();
        manager.CompleteRest();

        Assert.False(manager.Snapshot.FiveMinuteWarningRaised);
        Assert.False(manager.Snapshot.OneMinuteWarningRaised);
    }

    [Fact]
    public void DelayedTickUsesDeadlineRatherThanTickCount()
    {
        var (manager, clock) = Create();
        manager.StartWorking();

        clock.Advance(TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(13));
        manager.Tick();

        Assert.Equal(TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(47), manager.Snapshot.Remaining);
    }

    [Fact]
    public void WarningDoesNotFireBeforeBoundary()
    {
        var (manager, clock) = Create();
        var warnings = new List<SessionWarning>();
        manager.WarningRaised += warnings.Add;
        manager.StartWorking();

        clock.Advance(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59));
        manager.Tick();

        Assert.Empty(warnings);
        Assert.False(manager.Snapshot.FiveMinuteWarningRaised);
    }

    [Fact]
    public void DisabledWarningsRemainSilent()
    {
        var clock = new FakeClock();
        var options = Options with
        {
            FiveMinuteWarningEnabled = false,
            OneMinuteWarningEnabled = false
        };
        var manager = new SessionManager(clock, options);
        var warnings = new List<SessionWarning>();
        manager.WarningRaised += warnings.Add;
        manager.StartWorking();

        clock.Advance(TimeSpan.FromMinutes(19));
        manager.Tick();

        Assert.Empty(warnings);
    }

    [Fact]
    public void NonStrictModeMakesRestImmediatelyCompletable()
    {
        var clock = new FakeClock();
        var manager = new SessionManager(clock, Options with { StrictModeEnabled = false });
        manager.StartWorking();

        clock.Advance(TimeSpan.FromMinutes(20));
        manager.Tick();

        Assert.Equal(SessionState.MandatoryRestComplete, manager.State);
        Assert.True(manager.Snapshot.CanCompleteRest);
    }

    [Fact]
    public void SuspendPausesWorkingTimeUntilResume()
    {
        var (manager, clock) = Create();
        manager.StartWorking();
        clock.Advance(TimeSpan.FromMinutes(4));
        manager.Suspend();

        clock.Advance(TimeSpan.FromHours(8));
        manager.Tick();
        manager.Resume();

        Assert.Equal(SessionState.Working, manager.State);
        Assert.Equal(TimeSpan.FromMinutes(16), manager.Snapshot.Remaining);
    }

    private static (SessionManager Manager, FakeClock Clock) Create()
    {
        var clock = new FakeClock();
        return (new SessionManager(clock, Options), clock);
    }

    private static void EnterMandatoryRest(SessionManager manager, FakeClock clock)
    {
        manager.StartWorking();
        clock.Advance(TimeSpan.FromMinutes(20));
        manager.Tick();
    }
}
