using System.Diagnostics;

namespace TheEye.Core;

public sealed class SystemClock : IClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public TimeSpan MonotonicNow => _stopwatch.Elapsed;

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
