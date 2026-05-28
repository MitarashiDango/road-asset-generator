using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// Metallic / Smoothness パックマップを構築する。RGB = 0(非メタリック)、A = smoothness。
    /// 塗装マーク(減速帯と線 stroke)はアスファルトより滑らかにスタンプする。
    /// </summary>
    internal static class MetallicSmoothnessBuilder
    {
        public static Color32[] Build(in BakeContext ctx, LaneRange[] laneRanges, List<LineStroke> strokes)
        {
            var W = ctx.W;
            var H = ctx.H;
            var config = ctx.config;
            var pixels = new Color32[W * H];

            // アスファルトは粗い (~0.16)。濡れ路面では全体的に smoothness を上げる。
            var asphaltSmooth = config.weathering.wetSurface ? (byte)180 : (byte)40;
            var noise = RoadNoise.GaussianBlurWrap(RoadNoise.WhiteNoise(W, H, ctx.seed + 601), W, H, 2f);

            for (var i = 0; i < pixels.Length; i++)
            {
                var v = asphaltSmooth + (int)(noise[i] * 14f);
                v = Mathf.Clamp(v, 0, 255);
                pixels[i] = new Color32(0, 0, 0, (byte)v);
            }

            // 塗装マーク(減速帯と線 stroke)はアスファルトより滑らか。
            var lineSmooth = config.weathering.wetSurface ? (byte)220 : (byte)110;

            for (var li = 0; li < config.lanes.Count; li++)
            {
                var lane = config.lanes[li];
                if (!RumbleStripResolver.TryResolve(lane, laneRanges[li], in ctx, out var sp))
                {
                    continue;
                }
                RumblePixelIterator.ForEach(in sp, W, H, (x, y, idx, _) =>
                {
                    pixels[idx].a = lineSmooth;
                });
            }

            var preStrokeSmoothness = new byte[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                preStrokeSmoothness[i] = pixels[i].a;
            }

            foreach (var stroke in strokes)
            {
                if (!stroke.HasWearMask)
                {
                    var effectiveWear = LineWeathering.ResolveEffectiveWear(in stroke, config.weathering.lineWear);
                    StrokePixelIterator.ForEach(stroke, W, H, (x, y, idx, _, _, _) =>
                    {
                        pixels[idx].a = (byte)Mathf.RoundToInt(Mathf.Lerp(lineSmooth, preStrokeSmoothness[idx], effectiveWear));
                    });
                    continue;
                }

                StrokePixelIterator.ForEach(stroke, W, H, (x, y, idx, duFromCenter, _, _) =>
                {
                    var effectiveWear = LineWeathering.ResolveEffectiveWear(in stroke, config.weathering.lineWear, duFromCenter, y, H);
                    pixels[idx].a = (byte)Mathf.RoundToInt(Mathf.Lerp(lineSmooth, preStrokeSmoothness[idx], effectiveWear));
                });
            }
            return pixels;
        }
    }
}
