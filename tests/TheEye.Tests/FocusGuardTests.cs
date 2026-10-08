using TheEye.Core;

namespace TheEye.Tests;

public sealed class FocusGuardTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TheEye-tests-" + Guid.NewGuid().ToString("N"));
    private DateTimeOffset _now = new(2026, 9, 22, 12, 0, 0, TimeSpan.FromHours(3.5));
    private TimeSpan _uptime = TimeSpan.FromHours(2);
    private string _logonId = "first-logon";
    private string LedgerPath => Path.Combine(_directory, "commitment.json");
    private FocusGuard Load() => new(LedgerPath, () => _now, () => _uptime, _logonId);
    private void Advance(TimeSpan time) { _now += time; _uptime += time; }

    [Fact]
    public void IdleDoesNotConsumeTickets()
    {
        var guard = Load();
        Assert.False(guard.IsCommitted);
        Assert.Equal(3, guard.TicketsRemaining);
        Assert.False(guard.TryUseEmergencyTicket());
        Assert.Equal(3, Load().TicketsRemaining);
    }

    [Fact]
    public void ExactlyThreeExitsPersistAcrossRelaunches()
    {
        for (var ticket = 0; ticket < 3; ticket++)
        {
            var guard = Load();
            guard.BeginWork(SessionOptions.Default);
            Assert.True(Load().IsCommitted);
            Assert.True(guard.TryUseEmergencyTicket());
            Assert.False(guard.IsCommitted);
            Assert.Equal(2 - ticket, Load().TicketsRemaining);
        }
        var exhausted = Load();
        exhausted.BeginWork(SessionOptions.Default);
        Assert.False(exhausted.TryUseEmergencyTicket());
        Assert.True(Load().IsCommitted);
        Assert.Equal(0, Load().TicketsRemaining);
    }

    [Fact]
    public void WeekResetsOnMondayWithoutAccumulatingUnusedTickets()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default);
        guard.TryUseEmergencyTicket();
        Assert.Equal(new DateOnly(2026, 9, 28), guard.NextReset);
        Advance(TimeSpan.FromDays(5) + TimeSpan.FromHours(11) + TimeSpan.FromMinutes(59));
        Assert.Equal(2, guard.TicketsRemaining); // Sunday 23:59
        Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(3, guard.TicketsRemaining);
        Advance(TimeSpan.FromDays(21));
        Assert.Equal(3, guard.TicketsRemaining);
    }

    [Fact]
    public void ClockRollbackDoesNotRefillTickets()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default);
        guard.TryUseEmergencyTicket();
        _now -= TimeSpan.FromDays(7);
        Assert.Equal(2, guard.TicketsRemaining);
        _now += TimeSpan.FromDays(7);
        Assert.Equal(2, guard.TicketsRemaining);
    }

    [Fact]
    public void ShutdownReleasesCommitmentButNotWeeklyUsage()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default);
        guard.TryUseEmergencyTicket();
        guard.BeginWork(SessionOptions.Default);
        guard.NoteShutdownRequest();
        Advance(TimeSpan.FromMinutes(5));
        _logonId = "next-logon"; // Fast Startup: new logon, retained kernel uptime
        Assert.False(Load().IsCommitted);
        Assert.Equal(2, Load().TicketsRemaining);
    }

    [Fact]
    public void CancellingWindowsShutdownDoesNotReleaseCommitment()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default);
        guard.NoteShutdownRequest();
        Advance(TimeSpan.FromSeconds(5));
        Assert.True(Load().IsCommitted); // same boot and logon
        Assert.Equal(3, Load().TicketsRemaining);
    }

    [Fact]
    public void LogoffWithoutShutdownRetainsCommitment()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default);
        Advance(TimeSpan.FromMinutes(5));
        _logonId = "next-logon";
        Assert.True(Load().IsCommitted);
    }

    [Fact]
    public void ChangedBootReturnsIdleWithoutRefillingTickets()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default);
        guard.TryUseEmergencyTicket();
        guard.BeginWork(SessionOptions.Default);
        _now += TimeSpan.FromHours(1);
        _uptime = TimeSpan.FromMinutes(1);
        Assert.False(Load().IsCommitted);
        Assert.Equal(2, Load().TicketsRemaining);
    }

    [Fact]
    public void SameBootRelaunchRestoresCheckpointAndElapsedWorkTime()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default);
        Advance(TimeSpan.FromMinutes(7));
        var relaunched = Load();
        Assert.True(relaunched.IsCommitted);
        var manager = new SessionManager(new FakeClock());
        manager.Restore(relaunched.Recovery!.Snapshot, relaunched.ElapsedSinceCheckpoint);
        Assert.Equal(SessionState.Working, manager.State);
        Assert.Equal(TimeSpan.FromMinutes(13), manager.Snapshot.Remaining);
    }

    [Fact]
    public void RelaunchAfterWorkDeadlineRequiresFullRest()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default);
        Advance(TimeSpan.FromHours(1));
        var manager = new SessionManager(new FakeClock());
        manager.Restore(guard.Recovery!.Snapshot, guard.ElapsedSinceCheckpoint);
        Assert.Equal(SessionState.MandatoryRestLocked, manager.State);
        Assert.Equal(TimeSpan.FromMinutes(2), manager.Snapshot.Remaining);
    }

    [Fact]
    public void TimeWithRestWindowKilledDoesNotSatisfyMinimum()
    {
        var guard = Load();
        var snapshot = new SessionSnapshot(SessionState.MandatoryRestLocked, TimeSpan.FromSeconds(90), false, false, false);
        guard.Checkpoint(snapshot, SessionOptions.Default);
        Advance(TimeSpan.FromHours(1));
        var manager = new SessionManager(new FakeClock());
        manager.Restore(Load().Recovery!.Snapshot, TimeSpan.FromHours(1));
        Assert.False(manager.CompleteRest());
        Assert.Equal(TimeSpan.FromSeconds(90), manager.Snapshot.Remaining);
    }

    [Fact]
    public void CompletingRestOrStoppingDoesNotGrantFreeExit()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default);
        guard.Checkpoint(new SessionSnapshot(SessionState.MandatoryRestComplete, TimeSpan.Zero, true, true, false), SessionOptions.Default);
        Assert.True(guard.IsCommitted);
        guard.Checkpoint(new SessionSnapshot(SessionState.Idle, TimeSpan.Zero, false, false, false), SessionOptions.Default);
        Assert.True(Load().IsCommitted);
    }

    [Fact]
    public void FailedTicketSaveDoesNotReleaseCommitmentOrSpendTicket()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default);
        using (var locked = new FileStream(LedgerPath + ".tmp", FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => guard.TryUseEmergencyTicket());
        Assert.True(guard.IsCommitted);
        Assert.Equal(3, guard.TicketsRemaining);
        Assert.True(Load().IsCommitted);
        Assert.True(guard.TryUseEmergencyTicket());
        Assert.Equal(2, Load().TicketsRemaining);
    }

    [Fact]
    public void CorruptLedgerIsNotSilentlyReset()
    {
        Load();
        File.WriteAllText(LedgerPath, "not valid json");
        Assert.Throws<InvalidDataException>(() => Load());
        Assert.Equal("not valid json", File.ReadAllText(LedgerPath));
    }

    [Fact]
    public void WorkCannotStartIfCommitmentCannotBePersisted()
    {
        var manager = new SessionManager(new FakeClock()) { BeforeFirstStart = () => false };
        Assert.False(manager.StartWorking());
        Assert.Equal(SessionState.Idle, manager.State);
    }

    [Fact]
    public void SuspendedWorkCheckpointRetainsPausedRemaining()
    {
        var clock = new FakeClock();
        var manager = new SessionManager(clock);
        manager.StartWorking();
        clock.Advance(TimeSpan.FromMinutes(5));
        manager.Suspend();
        clock.Advance(TimeSpan.FromHours(2));
        var snapshot = manager.Snapshot;
        Assert.Equal(TimeSpan.FromMinutes(15), snapshot.Remaining);
        var restored = new SessionManager(new FakeClock());
        restored.Restore(snapshot, TimeSpan.FromHours(2));
        Assert.Equal(TimeSpan.FromMinutes(15), restored.Snapshot.Remaining);
    }

    [Fact]
    public void GamingUsesTheSameWeeklyTicketsAndPersistsModeFromFirstStart()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default, SessionMode.Gaming);
        Assert.Equal(SessionMode.Gaming, Load().Recovery!.Snapshot.Mode);
        Assert.Equal(3, guard.TicketsRemaining);
        Assert.True(guard.TryUseEmergencyTicket());
        guard.BeginWork(SessionOptions.Default);
        Assert.Equal(2, Load().TicketsRemaining);
        Assert.True(guard.TryUseEmergencyTicket());
        guard.BeginWork(SessionOptions.Default, SessionMode.Gaming);
        Assert.True(guard.TryUseEmergencyTicket());
        guard.BeginWork(SessionOptions.Default, SessionMode.Gaming);
        Assert.False(guard.TryUseEmergencyTicket());
        Assert.True(Load().IsCommitted);
    }

    [Fact]
    public void LegacyLedgerWithoutModeRestoresWorkingAndExistingTicketBalance()
    {
        var guard = Load();
        guard.BeginWork(SessionOptions.Default);
        guard.TryUseEmergencyTicket();
        guard.BeginWork(SessionOptions.Default);
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(LedgerPath))!;
        var snapshot = json["Recovery"]!["Snapshot"]!.AsObject();
        snapshot.Remove("Mode");
        snapshot.Remove("ThirtySecondWarningRaised");
        File.WriteAllText(LedgerPath, json.ToJsonString());
        var restored = Load();
        Assert.Equal(SessionMode.Working, restored.Recovery!.Snapshot.Mode);
        Assert.False(restored.Recovery.Snapshot.ThirtySecondWarningRaised);
        Assert.Equal(2, restored.TicketsRemaining);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
