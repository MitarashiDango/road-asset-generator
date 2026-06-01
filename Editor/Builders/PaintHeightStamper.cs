using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// 塗装マーク (境界線ストローク、レーンの減速マーク、減速帯) の高さを高さマップに加算し、
    /// 法線マップに塗装の凸が現れるようにする。lineWear が 1 に近づくにつれて塗装高さは減衰する。
    /// </summary>
    internal static class PaintHeightStamper
    {
        // 塗装マークの高さマップ寄与の単位値。strength = 1、factor = 1、lineWear = 0 のときに
        // 法線マップ上で塗装が見えつつアスファルト微細ノイズを覆い尽くさない値を選んでいる。
        private const float PaintHeightUnit = 25f;

        public static void Apply(float[] heightMap, in BakeContext ctx, List<LineStroke> strokes, LaneRange[] laneRanges)
        {
            var config = ctx.config;
            var globalWear = config.weathering.lineWear;
            var strength = Mathf.Max(0f, config.weathering.paintHeightStrength);
            if (strength <= 0f)
            {
                return;
            }

            var W = ctx.W;
            var H = ctx.H;

            foreach (var stroke in strokes)
            {
                if (stroke.paintHeightFactor <= 0f)
                {
                    continue;
                }
                if (!stroke.HasWearMask)
                {
                    var effectiveWear = LineWeathering.ResolveEffectiveWear(in stroke, globalWear);
                    var h = PaintHeightUnit * strength * Mathf.Clamp01(1f - effectiveWear) * stroke.paintHeightFactor;
                    if (h <= 0f)
                    {
                        continue;
                    }
                    StrokePixelIterator.ForEach(stroke, W, H, (x, y, idx, _, _, _) =>
                    {
                        heightMap[idx] += h;
                    });
                    continue;
                }

                StrokePixelIterator.ForEach(stroke, W, H, (x, y, idx, duFromCenter, _, _) =>
                {
                    var effectiveWear = LineWeathering.ResolveEffectiveWear(in stroke, globalWear, duFromCenter, y, H);
                    var h = PaintHeightUnit * strength * Mathf.Clamp01(1f - effectiveWear) * stroke.paintHeightFactor;
                    if (h > 0f)
                    {
                        heightMap[idx] += h;
                    }
                });
            }

            var globalScale = PaintHeightUnit * strength * Mathf.Clamp01(1f - globalWear);
            if (globalScale <= 0f)
            {
                return;
            }

            for (var li = 0; li < config.lanes.Count; li++)
            {
                var lane = config.lanes[li];
                if (lane.rumbleStripPaintHeightFactor <= 0f)
                {
                    continue;
                }
                if (!RumbleStripResolver.TryResolve(lane, laneRanges[li], in ctx, out var sp))
                {
                    continue;
                }
                var h = globalScale * lane.rumbleStripPaintHeightFactor;
                RumblePixelIterator.ForEach(in sp, W, H, (x, y, idx, _) =>
                {
                    heightMap[idx] += h;
                });
            }
        }
    }
}
