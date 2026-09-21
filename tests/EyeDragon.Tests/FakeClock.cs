using EyeDragon.Core;

namespace EyeDragon.Tests;

internal sealed class FakeClock : IClock
{
    public TimeSpan MonotonicNow { get; private set; }

    public DateTimeOffset UtcNow { get; private set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan duration)
    {
        MonotonicNow += duration;
        UtcNow += duration;
    }
}
