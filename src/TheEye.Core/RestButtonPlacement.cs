namespace TheEye.Core;

public readonly record struct PlacementPoint(double X, double Y);

public readonly record struct PlacementRectangle(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <summary>Chooses a random button position from the usable screen area.
/// Coordinates are local display-independent units, supplied by the UI.</summary>
public static class RestButtonPlacement
{
    public const double EdgeMargin = 24;
    public const double ContentClearance = 32;
    public const double PointerClearance = 72;
    public const double PreviousPositionClearance = 16;

    public static PlacementPoint? FindDestination(double screenWidth, double screenHeight,
        double buttonWidth, double buttonHeight, IReadOnlyList<PlacementRectangle> protectedAreas,
        PlacementPoint pointer, PlacementPoint previous, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(protectedAreas);
        if (!double.IsFinite(screenWidth) || !double.IsFinite(screenHeight) ||
            !double.IsFinite(buttonWidth) || !double.IsFinite(buttonHeight) ||
            buttonWidth <= 0 || buttonHeight <= 0 ||
            !double.IsFinite(pointer.X) || !double.IsFinite(pointer.Y) ||
            !double.IsFinite(previous.X) || !double.IsFinite(previous.Y)) return null;

        var width = screenWidth - buttonWidth - 2 * EdgeMargin;
        var height = screenHeight - buttonHeight - 2 * EdgeMargin;
        if (width <= 0 || height <= 0) return null;
        var available = new List<PlacementRectangle> { new(EdgeMargin, EdgeMargin, width, height) };

        // Work in the domain of button top-left positions. Expanding each
        // obstacle by the button size guarantees the entire button clears it.
        void Exclude(PlacementRectangle area, double clearance)
        {
            var forbidden = new PlacementRectangle(area.X - buttonWidth - clearance,
                area.Y - buttonHeight - clearance,
                area.Width + buttonWidth + 2 * clearance, area.Height + buttonHeight + 2 * clearance);
            available = available.SelectMany(region => Subtract(region, forbidden)).ToList();
        }

        foreach (var area in protectedAreas)
        {
            if (!double.IsFinite(area.X) || !double.IsFinite(area.Y) || !double.IsFinite(area.Width) ||
                !double.IsFinite(area.Height) || area.Width < 0 || area.Height < 0) return null;
            Exclude(area, ContentClearance);
        }
        Exclude(new PlacementRectangle(pointer.X, pointer.Y, 0, 0), PointerClearance);
        Exclude(new PlacementRectangle(previous.X, previous.Y, buttonWidth, buttonHeight), PreviousPositionClearance);
        if (available.Count == 0) return null; // Keep the lock even when there is no safe destination.

        random ??= Random.Shared;
        // Weight by area so splitting around obstacles does not favor small
        // strips. No rejection loops or fixed fallback corners are needed.
        var totalArea = available.Sum(region => region.Width * region.Height);
        var selection = random.NextDouble() * totalArea;
        var chosen = available[^1];
        foreach (var region in available)
        {
            selection -= region.Width * region.Height;
            if (selection < 0) { chosen = region; break; }
        }
        return new PlacementPoint(chosen.X + random.NextDouble() * chosen.Width,
            chosen.Y + random.NextDouble() * chosen.Height);
    }

    private static IEnumerable<PlacementRectangle> Subtract(PlacementRectangle region, PlacementRectangle obstacle)
    {
        var left = Math.Max(region.X, obstacle.X);
        var top = Math.Max(region.Y, obstacle.Y);
        var right = Math.Min(region.Right, obstacle.Right);
        var bottom = Math.Min(region.Bottom, obstacle.Bottom);
        if (left >= right || top >= bottom)
        {
            yield return region;
            yield break;
        }
        if (top > region.Y) yield return new(region.X, region.Y, region.Width, top - region.Y);
        if (bottom < region.Bottom) yield return new(region.X, bottom, region.Width, region.Bottom - bottom);
        if (left > region.X) yield return new(region.X, top, left - region.X, bottom - top);
        if (right < region.Right) yield return new(right, top, region.Right - right, bottom - top);
    }
}
