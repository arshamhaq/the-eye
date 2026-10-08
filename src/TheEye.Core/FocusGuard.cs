using System.Text.Json;

namespace TheEye.Core;

/// <summary>Durable, local commitment and weekly emergency-exit allowance.
/// This is an application guard, not protection against an administrator.</summary>
public sealed class FocusGuard
{
    public const int WeeklyAllowance = 3;
    private readonly string _path;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<TimeSpan> _uptime;
    private readonly string? _windowsLogonId;
    private GuardState _state;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FocusGuard(string path, Func<DateTimeOffset>? now = null, Func<TimeSpan>? uptime = null, string? windowsLogonId = null)
    {
        _path = path;
        _now = now ?? (() => DateTimeOffset.Now);
        _uptime = uptime ?? (() => TimeSpan.FromMilliseconds(Environment.TickCount64));
        _windowsLogonId = windowsLogonId;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (File.Exists(path))
        {
            try
            {
                _state = JsonSerializer.Deserialize<GuardState>(File.ReadAllText(path), JsonOptions)
                    ?? throw new InvalidDataException("Emergency ticket ledger is empty.");
                if (_state.Version != 1 || _state.UsedTickets is < 0 or > WeeklyAllowance ||
                    _state.WeekStart == default || _state.WeekStart.DayOfWeek != DayOfWeek.Monday)
                    throw new InvalidDataException("Emergency ticket ledger is invalid.");
                if (_state.Recovery is { } recovery)
                {
                    recovery.Options.Validate();
                    if (recovery.Snapshot.State is not (SessionState.Working or SessionState.MandatoryRestLocked or SessionState.MandatoryRestComplete) ||
                        recovery.Snapshot.Remaining < TimeSpan.Zero || recovery.CapturedUptime < TimeSpan.Zero ||
                        !Enum.IsDefined(recovery.Snapshot.Mode))
                        throw new InvalidDataException("Saved commitment is invalid.");
                }
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or NullReferenceException)
            {
                throw new InvalidDataException("Cannot read the emergency ticket ledger; it has not been reset.", ex);
            }
        }
        else
        {
            _state = new GuardState { WeekStart = WeekOf(_now()) };
            Save(_state);
        }
        // WPF exits on the shutdown QUERY, which Windows can later cancel.
        // Only release after a changed boot, or a requested shutdown followed
        // by a new Windows logon (also handles Fast Startup's retained uptime).
        if (_state.Recovery is { } saved)
        {
            var completedShutdown = _state.ShutdownRequested && saved.WindowsLogonId is not null &&
                _windowsLogonId is not null && saved.WindowsLogonId != _windowsLogonId;
            if (!SameBoot(saved, _now(), _uptime()) || completedShutdown)
                Save(_state with { Recovery = null, ShutdownRequested = false });
            else if (_state.ShutdownRequested)
                Save(_state with { ShutdownRequested = false });
        }
        RefreshWeek();
    }

    public bool IsCommitted => _state.Recovery is not null;
    public int TicketsRemaining { get { RefreshWeek(); return WeeklyAllowance - _state.UsedTickets; } }
    public DateOnly NextReset { get { RefreshWeek(); return _state.WeekStart.AddDays(7); } }
    public SessionRecovery? Recovery => _state.Recovery;

    public static DateOnly WeekOf(DateTimeOffset localTime)
    {
        var date = DateOnly.FromDateTime(localTime.Date);
        return date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    }

    public static bool SameBoot(SessionRecovery saved, DateTimeOffset now, TimeSpan uptime) =>
        uptime >= saved.CapturedUptime &&
        ((now.ToUniversalTime() - uptime) - (saved.CapturedUtc - saved.CapturedUptime)).Duration() < TimeSpan.FromMinutes(2);

    public void BeginWork(SessionOptions options, SessionMode mode = SessionMode.Working)
    {
        if (IsCommitted) throw new InvalidOperationException("An existing commitment must be resumed.");
        Checkpoint(new SessionSnapshot(SessionState.Working, options.WorkDuration, false, false, false, mode), options);
    }

    public void Checkpoint(SessionSnapshot snapshot, SessionOptions options)
    {
        // An incidental Stop/Idle transition must not grant a free exit.
        if (snapshot.State == SessionState.Idle) return;
        options.Validate();
        Save(_state with { Recovery = new SessionRecovery(snapshot, options, _now().ToUniversalTime(), _uptime(), _windowsLogonId), ShutdownRequested = false });
    }

    public bool TryUseEmergencyTicket()
    {
        RefreshWeek();
        if (!IsCommitted || _state.UsedTickets >= WeeklyAllowance) return false;
        // Debit and release in ONE durable transaction before permitting exit.
        Save(_state with { UsedTickets = _state.UsedTickets + 1, Recovery = null, ShutdownRequested = false });
        return true;
    }

    public void NoteShutdownRequest() => Save(_state with { ShutdownRequested = true });

    public TimeSpan ElapsedSinceCheckpoint => _state.Recovery is { } saved
        ? TimeSpan.FromTicks(Math.Max(0, (_uptime() - saved.CapturedUptime).Ticks)) : TimeSpan.Zero;

    private void RefreshWeek()
    {
        var week = WeekOf(_now());
        // Clock rollback must not replenish tickets already spent this week.
        if (week > _state.WeekStart) Save(_state with { WeekStart = week, UsedTickets = 0 });
    }

    private void Save(GuardState next)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(next, JsonOptions);
        var temporary = _path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                   4096, FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, _path, overwrite: true);
        _state = next; // Never spend an in-memory ticket on a failed write.
    }

    private sealed record GuardState
    {
        public int Version { get; init; } = 1;
        public DateOnly WeekStart { get; init; }
        public int UsedTickets { get; init; }
        public SessionRecovery? Recovery { get; init; }
        public bool ShutdownRequested { get; init; }
    }
}

public sealed record SessionRecovery(SessionSnapshot Snapshot, SessionOptions Options,
    DateTimeOffset CapturedUtc, TimeSpan CapturedUptime, string? WindowsLogonId = null);
