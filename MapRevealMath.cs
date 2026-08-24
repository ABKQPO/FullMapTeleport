namespace FullMapTeleport
{
    public struct MapRevealBounds
    {
        public MapRevealBounds(int left, int top, int rightExclusive, int bottomExclusive)
        {
            Left = left;
            Top = top;
            RightExclusive = rightExclusive;
            BottomExclusive = bottomExclusive;
        }

        public int Left { get; }
        public int Top { get; }
        public int RightExclusive { get; }
        public int BottomExclusive { get; }
    }

    public static class MapRevealMath
    {
        public static MapRevealBounds GetBounds(int maxTilesX, int maxTilesY, int margin)
        {
            int safeMargin = margin < 0 ? 0 : margin;
            int left = safeMargin;
            int top = safeMargin;
            int right = maxTilesX - safeMargin;
            int bottom = maxTilesY - safeMargin;
            if (right < left) right = left;
            if (bottom < top) bottom = top;
            return new MapRevealBounds(left, top, right, bottom);
        }
    }
}
