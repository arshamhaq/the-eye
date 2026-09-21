namespace TheEye.Core;

public sealed class PetDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0";

    public string Author { get; set; } = string.Empty;

    public double RecommendedScale { get; set; } = 1.0;

    public double DefaultWidth { get; set; } = 150;

    public double DefaultHeight { get; set; } = 180;

    public double AnchorX { get; set; } = 0.5;

    public double AnchorY { get; set; } = 1.0;

    public Dictionary<string, PetAnimation> Animations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
