using System.Diagnostics;

namespace EyeDragon.Core;

public sealed class SystemClock : IClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public TimeSpan MonotonicNow => _stopwatch.Elapsed;

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
