namespace TheEye.Core;

public interface IClock
{
    TimeSpan MonotonicNow { get; }

    DateTimeOffset UtcNow { get; }
}
