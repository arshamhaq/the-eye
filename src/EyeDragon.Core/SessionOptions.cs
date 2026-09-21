namespace EyeDragon.Core;

public sealed record SessionOptions(
    TimeSpan WorkDuration,
    TimeSpan MandatoryRestDuration,
    TimeSpan FiveMinuteWarningThreshold,
    TimeSpan OneMinuteWarningThreshold,
    bool FiveMinuteWarningEnabled = true,
    bool OneMinuteWarningEnabled = true,
    bool StrictModeEnabled = true)
{
    public static SessionOptions Default { get; } = new(
        TimeSpan.FromMinutes(20),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(1));

    public static SessionOptions Development { get; } = new(
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(5));

    public void Validate()
    {
        if (WorkDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(WorkDuration));
        }

        if (MandatoryRestDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(MandatoryRestDuration));
        }

        if (FiveMinuteWarningThreshold <= OneMinuteWarningThreshold ||
            FiveMinuteWarningThreshold >= WorkDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(FiveMinuteWarningThreshold));
        }

        if (OneMinuteWarningThreshold <= TimeSpan.Zero || OneMinuteWarningThreshold >= WorkDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(OneMinuteWarningThreshold));
        }
    }
}
