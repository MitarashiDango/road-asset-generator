using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>レーンの走行可能領域を表すピクセル範囲 [xStart, xEnd)。</summary>
    internal struct LaneRange
    {
        public int xStart;
        public int xEnd;
    }

    /// <summary><see cref="RoadConfig"/> の各レーンに対するピクセル範囲を計算する。</summary>
    internal static class LaneRangeResolver
    {
        public static LaneRange[] Resolve(in BakeContext ctx)
        {
            var config = ctx.config;
            config.EnsureLineCount();
            var ranges = new LaneRange[config.lanes.Count];
            var pos = config.leftShoulder.widthMeters;
            for (var b = 0; b <= config.lanes.Count; b++)
            {
                config.lines[b].ComputeSlotInfo(out _, out _, out var slotWidth);
                pos += slotWidth;
                if (b < config.lanes.Count)
                {
                    var xs = Mathf.RoundToInt(pos * ctx.pxPerMx);
                    var laneEndM = pos + config.lanes[b].widthMeters;
                    var xe = Mathf.RoundToInt(laneEndM * ctx.pxPerMx);
                    ranges[b] = new LaneRange { xStart = xs, xEnd = xe };
                    pos = laneEndM;
                }
            }
            return ranges;
        }
    }
}
