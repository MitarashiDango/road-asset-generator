using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// 路面の Albedo マップを構築する。アスファルトのベース色、肩部のティント、レーン色、
    /// 微細ノイズ、タイヤ跡、路面凹凸舗装・境界線ストローク・補修パッチのスタンプを統合する。
    /// </summary>
    internal static class AlbedoBuilder
    {
        public static Color32[] Build(in BakeContext ctx, List<LineStroke> strokes, LaneRange[] laneRanges, float[] heightMap)
        {
            var W = ctx.W;
            var H = ctx.H;
            var config = ctx.config;
            var seed = ctx.seed;

            var leftSh = Mathf.RoundToInt(config.leftShoulder.widthMeters * ctx.pxPerMx);
            var rightSh = W - Mathf.RoundToInt(config.rightShoulder.widthMeters * ctx.pxPerMx);

            var pixels = new Color32[W * H];
            var baseCol = config.asphalt.baseColor;
            var shoulderTintL = ShoulderTintColor(config.leftShoulder.colorTint);
            var shoulderTintR = ShoulderTintColor(config.rightShoulder.colorTint);

            var rng = new System.Random(seed + 200);
            var bright = config.asphalt.brightSpeckAmount;
            var dark = config.asphalt.darkSpeckAmount;

            float[] wearMap = null;
            if (config.weathering.tireTrackWear > 0f)
            {
                wearMap = BuildTireTrackWear(config, W, H);
            }

            var laneIndexAt = BuildLaneIndexLUT(W, laneRanges);

            for (var y = 0; y < H; y++)
            {
                for (var x = 0; x < W; x++)
                {
                    var idx = y * W + x;
                    var h = heightMap[idx];
                    var c = baseCol;
                    c.r += h / 255f;
                    c.g += h * 0.95f / 255f;
                    c.b += h * 0.88f / 255f;

                    if (x < leftSh)
                    {
                        c.r += shoulderTintL.r;
                        c.g += shoulderTintL.g;
                        c.b += shoulderTintL.b;
                    }
                    else if (x >= rightSh)
                    {
                        c.r += shoulderTintR.r;
                        c.g += shoulderTintR.g;
                        c.b += shoulderTintR.b;
                    }

                    var laneIdx = laneIndexAt[x];
                    if (laneIdx >= 0)
                    {
                        var lane = config.lanes[laneIdx];
                        if (lane.surfaceTint && lane.surfaceTintStrength > 0f)
                        {
                            c = Color.Lerp(c, lane.surfaceTintColor, lane.surfaceTintStrength);
                        }
                    }

                    var r = rng.NextDouble();
                    if (r > 1.0 - bright)
                    {
                        c.r += 0.12f;
                        c.g += 0.12f;
                        c.b += 0.12f;
                    }
                    else if (r < dark)
                    {
                        c.r -= 0.09f;
                        c.g -= 0.09f;
                        c.b -= 0.09f;
                    }

                    if (wearMap != null)
                    {
                        var w = wearMap[idx];
                        c.r -= w * 0.07f;
                        c.g -= w * 0.07f;
                        c.b -= w * 0.07f;
                    }

                    pixels[idx] = TextureUtils.ToColor32(c);
                }
            }

            // 路面凹凸舗装と境界線ストロークはアスファルトの上から重ね描きする。
            StampRumbleStrips(pixels, in ctx, laneRanges, seed + 350);
            StampStrokes(pixels, W, H, strokes, config.weathering.lineWear, config.weathering.lineFade, seed + 400);

            if (config.weathering.repairPatches && config.weathering.repairPatchCount > 0)
            {
                StampRepairPatches(pixels, W, H, config.weathering.repairPatchCount, leftSh, rightSh, seed + 500);
            }

            return pixels;
        }

        private static Color ShoulderTintColor(float tint)
        {
            return new Color(tint, tint, tint * 0.7f);
        }

        // 各ピクセル列 x を所属レーンの index に対応させる LUT(どのレーンにも属さなければ -1)。
        // レーン範囲は走行可能領域(drivable bounds)であり、境界線は専用の slot を持つため、ここで -1
        // を返した位置にはレーン色のティントが乗らない。
        private static int[] BuildLaneIndexLUT(int W, LaneRange[] laneRanges)
        {
            var lut = new int[W];
            for (var i = 0; i < W; i++)
            {
                lut[i] = -1;
            }
            for (var li = 0; li < laneRanges.Length; li++)
            {
                var xs = Mathf.Max(0, laneRanges[li].xStart);
                var xe = Mathf.Min(W, laneRanges[li].xEnd);
                for (var x = xs; x < xe; x++)
                {
                    lut[x] = li;
                }
            }
            return lut;
        }

        private static float[] BuildTireTrackWear(RoadConfig config, int W, int H)
        {
            var map = new float[W * H];
            var pxPerMx = W / config.TotalWidthMeters;
            var intensity = config.weathering.tireTrackWear;
            var pos = config.leftShoulder.widthMeters;
            for (var li = 0; li < config.lanes.Count; li++)
            {
                var laneCenter = pos + config.lanes[li].widthMeters * 0.5f;
                pos += config.lanes[li].widthMeters;
                // 1 レーンに 2 本のタイヤ跡(中央から ±0.85 m)。
                for (var side = -1; side <= 1; side += 2)
                {
                    var trackX = laneCenter + side * 0.85f;
                    var trackPx = Mathf.RoundToInt(trackX * pxPerMx);
                    var sigmaPx = Mathf.RoundToInt(0.18f * pxPerMx);
                    for (var x = trackPx - sigmaPx * 3; x <= trackPx + sigmaPx * 3; x++)
                    {
                        if (x < 0 || x >= W)
                        {
                            continue;
                        }
                        var dx = (x - trackPx) / (float)sigmaPx;
                        var falloff = Mathf.Exp(-dx * dx * 0.5f);
                        for (var y = 0; y < H; y++)
                        {
                            map[y * W + x] += falloff * intensity;
                        }
                    }
                }
            }
            return map;
        }

        private static void StampRumbleStrips(Color32[] pixels, in BakeContext ctx, LaneRange[] laneRanges, int seedBase)
        {
            var W = ctx.W;
            var H = ctx.H;
            var config = ctx.config;
            for (var li = 0; li < config.lanes.Count; li++)
            {
                var lane = config.lanes[li];
                if (!RumbleStripResolver.TryResolve(lane, laneRanges[li], in ctx, out var sp))
                {
                    continue;
                }

                var rng = new System.Random(seedBase + li * 31);
                var baseColor = lane.rumbleStripColor;

                RumblePixelIterator.ForEach(in sp, W, H, (x, y, idx, alpha) =>
                {
                    var n = (float)(rng.NextDouble() * 2 - 1) * 0.06f;
                    var c = new Color(
                        Mathf.Clamp01(baseColor.r + n),
                        Mathf.Clamp01(baseColor.g + n),
                        Mathf.Clamp01(baseColor.b + n));
                    var src = pixels[idx];
                    var sourceCol = new Color(src.r / 255f, src.g / 255f, src.b / 255f);
                    pixels[idx] = TextureUtils.ToColor32(Color.Lerp(sourceCol, c, alpha));
                });
            }
        }

        private static void StampStrokes(Color32[] pixels, int W, int H, List<LineStroke> strokes, float wear, float fade, int seedBase)
        {
            foreach (var stroke in strokes)
            {
                var rng = new System.Random(stroke.seed + seedBase);
                var s = stroke;
                StrokePixelIterator.ForEach(s, W, H, (x, y, idx, duFromCenter, xStart, xEnd) =>
                {
                    var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    var wearFactor = 1f - wear * 0.5f * Mathf.Abs(noise);
                    var c = s.color * wearFactor;

                    var isEdge = s.shape.HasDiagonalEdges
                        ? Mathf.Abs(duFromCenter) >= s.halfWidthPx - 1f
                        : (x == xStart || x == xEnd - 1);

                    if (isEdge)
                    {
                        var src = pixels[idx];
                        var t = 0.45f - fade * 0.3f;
                        c = Color.Lerp(new Color(src.r / 255f, src.g / 255f, src.b / 255f), c, t);
                    }
                    else
                    {
                        c = Color.Lerp(c, Color.gray, fade * 0.4f);
                    }
                    pixels[idx] = TextureUtils.ToColor32(c);
                });
            }
        }

        private static void StampRepairPatches(Color32[] pixels, int W, int H, int count, int leftSh, int rightSh, int seed)
        {
            var rng = new System.Random(seed);
            var patchCol = new Color(45f / 255f, 43f / 255f, 41f / 255f);
            for (var i = 0; i < count; i++)
            {
                var cx = rng.Next(leftSh + 20, Mathf.Max(leftSh + 21, rightSh - 20));
                var cy = rng.Next(0, H);
                var rw = rng.Next(W / 12, W / 5);
                var rh = rng.Next(H / 12, H / 5);
                for (var y = cy - rh / 2; y <= cy + rh / 2; y++)
                {
                    var yw = ((y % H) + H) % H;
                    for (var x = cx - rw / 2; x <= cx + rw / 2; x++)
                    {
                        if (x < 0 || x >= W)
                        {
                            continue;
                        }
                        var edgeX = 1f - Mathf.Abs((x - cx) / (rw * 0.5f));
                        var edgeY = 1f - Mathf.Abs((y - cy) / (rh * 0.5f));
                        var a = Mathf.Clamp01(Mathf.Min(edgeX, edgeY) * 2f);
                        var src = pixels[yw * W + x];
                        var blended = Color.Lerp(new Color(src.r / 255f, src.g / 255f, src.b / 255f), patchCol, a * 0.7f);
                        pixels[yw * W + x] = TextureUtils.ToColor32(blended);
                    }
                }
            }
        }
    }
}
