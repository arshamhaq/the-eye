namespace EyeDragon.Core;

public sealed class PetAnimation
{
    public double FramesPerSecond { get; set; } = 8;

    public bool Loop { get; set; } = true;

    public List<string> Frames { get; set; } = [];
}
