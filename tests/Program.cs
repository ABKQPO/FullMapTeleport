using FullMapTeleport;

static class Program
{
    private static int _checks;

    private static void AssertEqual(TilePoint expected, TilePoint actual)
    {
        _checks++;
        if (expected != actual)
            throw new InvalidOperationException($"Expected {expected}, got {actual}");
    }

    private static void Main()
    {
        AssertEqual(new TilePoint(100, 80),
            TeleportMath.MapScreenToTile(960, 540, 1920, 1080, new TilePoint(100, 80), 4f));
        AssertEqual(new TilePoint(150, 80),
            TeleportMath.MapScreenToTile(1160, 540, 1920, 1080, new TilePoint(100, 80), 4f));
        AssertEqual(new TilePoint(100, 55),
            TeleportMath.MapScreenToTile(960, 440, 1920, 1080, new TilePoint(100, 80), 4f));
        AssertEqual(new TilePoint(10, 10),
            TeleportMath.Clamp(new TilePoint(-4, 1), 10, 200, 10, 150));
        AssertEqual(new TilePoint(199, 149),
            TeleportMath.Clamp(new TilePoint(999, 999), 10, 200, 10, 150));

        AssertTrue(TeleportMath.IsInstantReturnItem(50), "magic mirror is instant");
        AssertTrue(TeleportMath.IsInstantReturnItem(3199), "ice mirror is instant");
        AssertTrue(TeleportMath.IsInstantReturnItem(3124), "cell phone is instant");
        AssertTrue(TeleportMath.IsInstantReturnItem(5358), "shellphone is instant");
        AssertTrue(TeleportMath.IsInstantReturnItem(5359), "spawn shellphone is instant");
        AssertTrue(TeleportMath.IsInstantReturnItem(5360), "ocean shellphone is instant");
        AssertTrue(TeleportMath.IsInstantReturnItem(5361), "hell shellphone is instant");
        AssertFalse(TeleportMath.IsInstantReturnItem(2350), "recall potion keeps vanilla timing");

        var playerPosition = TeleportMath.MapTileToPlayerTopLeft(new TilePoint(100, 80), 20, 42);
        AssertNear(1598f, playerPosition.X, "player X is centered on the map tile");
        AssertNear(1267f, playerPosition.Y, "player Y is centered on the map tile");

        var rightClick = new ButtonPressLatch();
        AssertTrue(rightClick.ConsumeNewPress(true), "first right-click is detected");
        AssertFalse(rightClick.ConsumeNewPress(true), "holding right-click does not repeat");
        AssertFalse(rightClick.ConsumeNewPress(false), "release does not teleport");
        AssertTrue(rightClick.ConsumeNewPress(true), "second right-click is detected without reopening the map");

        _checks++;
        if (TeleportMath.PylonWorldY(200, 3f) != 3197)
            throw new InvalidOperationException("Pylon world coordinate conversion is incorrect");

        Console.WriteLine($"{_checks} checks passed");
    }

    private static void AssertTrue(bool value, string name)
    {
        _checks++;
        if (!value)
            throw new InvalidOperationException($"Expected true: {name}");
    }

    private static void AssertFalse(bool value, string name)
    {
        _checks++;
        if (value)
            throw new InvalidOperationException($"Expected false: {name}");
    }

    private static void AssertNear(float expected, float actual, string name)
    {
        _checks++;
        if (Math.Abs(expected - actual) > 0.001f)
            throw new InvalidOperationException($"Expected {expected}, got {actual}: {name}");
    }
}
