using TheEye.Core;

namespace TheEye.Tests;

public sealed class RestButtonPlacementTests
{
    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1536, 864)] // 1920 x 1080 at 125% DPI, in WPF units
    [InlineData(960, 540)] // 1920 x 1080 at 200%
    [InlineData(1920, 1080)]
    [InlineData(3440, 1440)]
    [InlineData(1080, 1920)]
    public void RepeatedDodgesStayOnScreenAndClearOfContentPointerAndOldPosition(double width, double height)
    {
        var scale = Math.Min(width / 1920, height / 1080);
        var offsetX = (width - 1920 * scale) / 2;
        var offsetY = (height - 1080 * scale) / 2;
        PlacementRectangle[] obstacles = [
            new(offsetX + 140 * scale, offsetY + 300 * scale, 720 * scale, 440 * scale),
            new(width - 310, 30, 280, 52)];
        var random = new Random(803);
        var previous = new PlacementPoint(width * .12, height * .8);
        var seen = new HashSet<PlacementPoint>();
        for (var i = 0; i < 1000; i++)
        {
            var pointer = new PlacementPoint(previous.X + 90, previous.Y + 26);
            var next = RestButtonPlacement.FindDestination(width, height, 180, 52, obstacles, pointer, previous, random);
            Assert.NotNull(next);
            var point = next.Value;
            var button = new PlacementRectangle(point.X, point.Y, 180, 52);
            Assert.InRange(button.X, 24, width - 180 - 24);
            Assert.InRange(button.Y, 24, height - 52 - 24);
            foreach (var obstacle in obstacles) Assert.False(Intersects(button, Inflate(obstacle, 32)));
            Assert.False(Intersects(button, new PlacementRectangle(pointer.X - 72, pointer.Y - 72, 144, 144)));
            Assert.False(Intersects(button, Inflate(new PlacementRectangle(previous.X, previous.Y, 180, 52), 16)));
            seen.Add(point);
            previous = point;
        }
        Assert.True(seen.Count > 990, "Dodges must not alternate between a few fixed destinations.");
        Assert.True(seen.Select(point => (int)(point.X / (width / 6))).Distinct().Count() >= 4);
        Assert.True(seen.Select(point => (int)(point.Y / (height / 6))).Distinct().Count() >= 4);
    }

    [Fact]
    public void SamplingCoversTheAvailableAreaInsteadOfOnlyCornersOrEdges()
    {
        var random = new Random(13);
        var points = Enumerable.Range(0, 4000).Select(_ => RestButtonPlacement.FindDestination(1920, 1080, 180, 52,
            [], new(-1000, -1000), new(-1000, -1000), random)!.Value).ToArray();
        var cells = points.Select(p => ((int)((p.X - 24) / 1692 * 4), (int)((p.Y - 24) / 980 * 4)))
            .GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(16, cells.Count);
        Assert.All(cells.Values, count => Assert.InRange(count, 170, 330));
    }

    [Fact]
    public void AThinRemainingSafeRegionIsFoundWithoutRetriesOrUnsafeFallbacks()
    {
        var random = new Random(25);
        // Leaves only x=406..476 as valid left positions, after button width
        // and content clearance are accounted for.
        PlacementRectangle[] obstacles = [new(0, 0, 374, 400)];
        for (var i = 0; i < 50; i++)
        {
            var next = RestButtonPlacement.FindDestination(600, 400, 100, 40, obstacles,
                new(-1000, -1000), new(-1000, -1000), random);
            Assert.NotNull(next);
            Assert.InRange(next.Value.X, 406, 476);
        }
    }

    [Fact]
    public void OverlappingAndOffscreenObstaclesStillProtectAllVisibleContent()
    {
        PlacementRectangle[] obstacles = [new(-50, 100, 400, 200), new(200, 200, 300, 200), new(750, -50, 300, 150)];
        var random = new Random(31);
        for (var i = 0; i < 200; i++)
        {
            var next = RestButtonPlacement.FindDestination(1000, 700, 180, 52, obstacles,
                new(800, 600), new(800, 570), random);
            Assert.NotNull(next);
            var button = new PlacementRectangle(next.Value.X, next.Value.Y, 180, 52);
            Assert.All(obstacles, obstacle => Assert.False(Intersects(button, Inflate(obstacle, 32))));
        }
    }

    [Fact]
    public void NoSafeSpaceReturnsNoDestinationInsteadOfCoveringTextOrTicket()
    {
        Assert.Null(RestButtonPlacement.FindDestination(1920, 1080, 180, 52,
            [new(0, 0, 1920, 1080)], new(0, 0), new(0, 0)));
        Assert.Null(RestButtonPlacement.FindDestination(200, 100, 180, 52, [], new(0, 0), new(0, 0)));
    }

    [Theory]
    [InlineData(double.NaN, 1080)]
    [InlineData(1920, double.PositiveInfinity)]
    [InlineData(-1920, 1080)]
    [InlineData(1920, 0)]
    public void InvalidOrUnmeasuredLayoutCannotCreateAnOffscreenPosition(double width, double height)
    {
        Assert.Null(RestButtonPlacement.FindDestination(width, height, 180, 52, [], new(0, 0), new(0, 0)));
    }

    private static PlacementRectangle Inflate(PlacementRectangle rect, double amount) =>
        new(rect.X - amount, rect.Y - amount, rect.Width + 2 * amount, rect.Height + 2 * amount);

    private static bool Intersects(PlacementRectangle a, PlacementRectangle b) =>
        a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
}
