namespace EyeDragon.Core;

public sealed class AppSettings
{
    public int WorkDurationMinutes { get; set; } = 20;

    public int MandatoryRestMinutes { get; set; } = 2;

    public bool StrictModeEnabled { get; set; } = true;

    public bool LaunchWithWindows { get; set; }

    public bool LaunchMinimized { get; set; }

    public bool FiveMinuteWarningEnabled { get; set; } = true;

    public bool OneMinuteWarningEnabled { get; set; } = true;

    public string SelectedPet { get; set; } = "PurpleDragon";

    public double PetScale { get; set; } = 1.0;

    public bool AnimationEnabled { get; set; } = true;

    public double AnimationSpeed { get; set; } = 1.0;

    public bool WarningSoundsEnabled { get; set; } = true;

    public bool RestStartSoundEnabled { get; set; } = true;

    public double Volume { get; set; } = 0.7;

    public AppSettings Normalize()
    {
        WorkDurationMinutes = Math.Clamp(WorkDurationMinutes, 1, 180);
        MandatoryRestMinutes = Math.Clamp(MandatoryRestMinutes, 0, 30);
        PetScale = Math.Clamp(PetScale, 0.5, 2.0);
        AnimationSpeed = Math.Clamp(AnimationSpeed, 0.5, 2.0);
        Volume = Math.Clamp(Volume, 0, 1);
        SelectedPet = string.IsNullOrWhiteSpace(SelectedPet) ? "PurpleDragon" : SelectedPet.Trim();
        return this;
    }

    public SessionOptions ToSessionOptions()
    {
        var work = TimeSpan.FromMinutes(WorkDurationMinutes);
        var oneMinute = TimeSpan.FromTicks(Math.Min(TimeSpan.FromMinutes(1).Ticks, work.Ticks / 4));
        var fiveMinutes = TimeSpan.FromTicks(Math.Min(TimeSpan.FromMinutes(5).Ticks, work.Ticks / 2));

        return new SessionOptions(
            work,
            TimeSpan.FromMinutes(MandatoryRestMinutes),
            fiveMinutes,
            oneMinute,
            FiveMinuteWarningEnabled,
            OneMinuteWarningEnabled,
            StrictModeEnabled);
    }
}
