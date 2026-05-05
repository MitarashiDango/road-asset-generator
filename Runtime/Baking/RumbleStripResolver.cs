using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>1 本の rumble strip を描画するために必要な、すべてのパスで共通のパラメータ。</summary>
    internal struct RumbleStripParams
    {
        public float widthPx;
        public float periodPx;
        public float offsetPx;
        public int xStart;
        public int xEnd;
    }

    /// <summary><see cref="LaneConfig"/> から rumble strip 描画パラメータを解決する。</summary>
    internal static class RumbleStripResolver
    {
        public static bool TryResolve(LaneConfig lane, LaneRange laneRange, in BakeContext ctx, out RumbleStripParams parameters)
        {
            parameters = default;
            if (!lane.rumbleStrip)
            {
                return false;
            }

            var widthM = Mathf.Max(0.05f, lane.rumbleStripWidthMeters);
            var spacingM = Mathf.Max(widthM + 0.05f, lane.rumbleStripSpacingMeters);
            var periodM = widthM + spacingM;
            var insetPx = lane.rumbleStripInsetMeters * ctx.pxPerMx;

            parameters.widthPx = widthM * ctx.pxPerMy;
            parameters.periodPx = periodM * ctx.pxPerMy;
            parameters.offsetPx = lane.rumbleStripStartOffsetMeters * ctx.pxPerMy;
            parameters.xStart = Mathf.Max(0, laneRange.xStart + Mathf.RoundToInt(insetPx));
            parameters.xEnd = Mathf.Min(ctx.W, laneRange.xEnd - Mathf.RoundToInt(insetPx));
            return parameters.xEnd > parameters.xStart;
        }
    }
}
