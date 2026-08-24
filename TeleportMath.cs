using System;

namespace FullMapTeleport
{
    public struct WorldPoint
    {
        public WorldPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public override string ToString() => $"({X}, {Y})";
    }

    public struct TilePoint : IEquatable<TilePoint>
    {
        public TilePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(TilePoint other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is TilePoint other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(TilePoint left, TilePoint right) => left.Equals(right);

        public static bool operator !=(TilePoint left, TilePoint right) => !left.Equals(right);
    }

    public static class TeleportMath
    {
        public static TilePoint MapScreenToTile(
            int mouseX,
            int mouseY,
            int screenWidth,
            int screenHeight,
            TilePoint mapCenter,
            float mapScale)
        {
            return MapScreenToTile(mouseX, mouseY, screenWidth, screenHeight,
                mapCenter.X, mapCenter.Y, mapScale);
        }

        public static TilePoint MapScreenToTile(
            int mouseX,
            int mouseY,
            int screenWidth,
            int screenHeight,
            float mapCenterX,
            float mapCenterY,
            float mapScale)
        {
            if (mapScale <= 0f || float.IsNaN(mapScale) || float.IsInfinity(mapScale))
                return new TilePoint((int)Math.Floor(mapCenterX), (int)Math.Floor(mapCenterY));

            int tileX = (int)Math.Floor(mapCenterX + (mouseX - screenWidth * 0.5f) / mapScale);
            int tileY = (int)Math.Floor(mapCenterY + (mouseY - screenHeight * 0.5f) / mapScale);
            return new TilePoint(tileX, tileY);
        }

        public static TilePoint Clamp(TilePoint point, int minX, int maxXExclusive, int minY, int maxYExclusive)
        {
            if (maxXExclusive <= minX || maxYExclusive <= minY)
                return new TilePoint(minX, minY);

            int x = Math.Max(minX, Math.Min(maxXExclusive - 1, point.X));
            int y = Math.Max(minY, Math.Min(maxYExclusive - 1, point.Y));
            return new TilePoint(x, y);
        }

        public static WorldPoint MapTileToPlayerTopLeft(TilePoint tile, int playerWidth, int playerHeight)
        {
            return new WorldPoint(
                tile.X * 16f + 8f - playerWidth * 0.5f,
                tile.Y * 16f + 8f - playerHeight * 0.5f);
        }

        public static bool IsInstantReturnItem(int itemType)
        {
            switch (itemType)
            {
                case 50:   // Magic Mirror
                case 3199: // Ice Mirror
                case 3124: // Cell Phone
                case 5358: // Shellphone
                case 5359: // Shellphone (spawn)
                case 5360: // Shellphone (ocean)
                case 5361: // Shellphone (underworld)
                    return true;
                default:
                    return false;
            }
        }

        public static float PylonWorldY(int tileY, float heightOffset)
        {
            return tileY * 16f - heightOffset;
        }
    }

    public sealed class ButtonPressLatch
    {
        private bool _wasDown;

        public bool ConsumeNewPress(bool isDown)
        {
            bool isNewPress = isDown && !_wasDown;
            _wasDown = isDown;
            return isNewPress;
        }

        public void Reset()
        {
            _wasDown = false;
        }
    }
}
