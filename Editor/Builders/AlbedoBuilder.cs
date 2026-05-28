using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// 路面の Albedo マップを構築する。アスファルトのベース色、肩部のティント、レーン色、
    /// 微細ノイズ、タイヤ跡、減速帯・境界線ストローク・補修パッチのスタンプを統合する。
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

            TireTrackWearData tireTrackWear = null;
            if (HasAnyTireWear(config))
            {
                tireTrackWear = BuildTireTrackWear(config, laneRanges, W, H, ctx.pxPerMx);
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

                    if (tireTrackWear != null)
                    {
                        var blend = Mathf.Clamp01(tireTrackWear.blendMap[idx]);
                        if (blend > 0f)
                        {
                            var target = tireTrackWear.ResolveColor(idx);
                            c = Color.Lerp(c, target, blend);
                        }
                    }

                    pixels[idx] = TextureUtils.ToColor32(c);
                }
            }

            // 減速帯と境界線ストロークはアスファルトの上から重ね描きする。
            StampRumbleStrips(pixels, in ctx, laneRanges, seed + 350);
            StampStrokes(pixels, W, H, strokes, config.weathering.lineWear, config.weathering.lineFade, tireTrackWear?.wearMap, config.weathering.tireTrackMarkingWearStrength, seed + 400);

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

        private sealed class TireTrackWearData
        {
            public readonly float[] wearMap;
            public readonly float[] blendMap;
            public readonly Color32[] colorMap;

            public TireTrackWearData(int length)
            {
                wearMap = new float[length];
                blendMap = new float[length];
                colorMap = new Color32[length];
            }

            public void AddTrack(int idx, float wear, float blend, Color color)
            {
                wearMap[idx] += wear;
                if (blend <= 0f)
                {
                    return;
                }

                var oldBlend = blendMap[idx];
                var newBlend = oldBlend + blend;
                if (oldBlend > 0f)
                {
                    var current = ToColor(colorMap[idx]);
                    colorMap[idx] = TextureUtils.ToColor32(Color.Lerp(current, color, blend / newBlend));
                }
                else
                {
                    colorMap[idx] = TextureUtils.ToColor32(color);
                }
                blendMap[idx] = newBlend;
            }

            public Color ResolveColor(int idx)
            {
                Debug.Assert(blendMap[idx] > 0f, "Tire track color should only be resolved for pixels with a positive blend weight.");
                return ToColor(colorMap[idx]);
            }

            private static Color ToColor(Color32 c)
            {
                return new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f);
            }
        }

        private static TireTrackWearData BuildTireTrackWear(RoadConfig config, LaneRange[] laneRanges, int W, int H, float pxPerMx)
        {
            var data = new TireTrackWearData(W * H);
            var globalIntensity = config.weathering.tireTrackWear;

            for (var li = 0; li < config.lanes.Count; li++)
            {
                var lane = config.lanes[li];
                var laneIntensity = Mathf.Clamp01(globalIntensity + lane.tireTrackWearBoost);
                if (laneIntensity <= 0f)
                {
                    continue;
                }

                ResolveTireTrackSettings(config, lane, out var widthMeters, out var spacingMeters, out var color, out var opacity);
                var trackOffsetPx = spacingMeters * 0.5f * pxPerMx;
                var sigmaPx = Mathf.Max(0.5f, widthMeters * pxPerMx / 6f);
                var radiusPx = Mathf.CeilToInt(sigmaPx * 3f);

                // 境界線スロット幅を含めた正しいレーン中央を LaneRange から取得する。
                var range = laneRanges[li];
                var laneCenterPx = (range.xStart + range.xEnd) * 0.5f;

                for (var side = -1; side <= 1; side += 2)
                {
                    var trackPx = laneCenterPx + side * trackOffsetPx;
                    for (var x = Mathf.FloorToInt(trackPx) - radiusPx; x <= Mathf.CeilToInt(trackPx) + radiusPx; x++)
                    {
                        if (x < 0 || x >= W)
                        {
                            continue;
                        }
                        var dx = (x - trackPx) / sigmaPx;
                        var falloff = Mathf.Exp(-dx * dx * 0.5f);
                        var wear = falloff * laneIntensity;
                        var blend = wear * opacity;
                        for (var y = 0; y < H; y++)
                        {
                            var idx = y * W + x;
                            data.AddTrack(idx, wear, blend, color);
                        }
                    }
                }
            }
            return data;
        }

        private static void ResolveTireTrackSettings(RoadConfig config, LaneConfig lane, out float widthMeters, out float spacingMeters, out Color color, out float opacity)
        {
            var weathering = config.weathering;
            if (lane.tireTrackOverride)
            {
                widthMeters = lane.tireTrackWidthMeters;
                spacingMeters = lane.tireTrackSpacingMeters;
                color = lane.tireTrackColor;
                opacity = lane.tireTrackOpacity;
            }
            else
            {
                widthMeters = weathering.defaultTireTrackWidthMeters;
                spacingMeters = weathering.defaultTireTrackSpacingMeters;
                color = weathering.defaultTireTrackColor;
                opacity = weathering.defaultTireTrackOpacity;
            }

            widthMeters = Mathf.Max(0.05f, widthMeters);
            spacingMeters = Mathf.Max(0f, spacingMeters);
            opacity = Mathf.Clamp01(opacity);
            // Albedo output is opaque; ignore the UI color alpha so tire tracks only affect RGB.
            color.a = 1f;
        }

        private static bool HasAnyTireWear(RoadConfig config)
        {
            if (config.weathering.tireTrackWear > 0f)
            {
                return true;
            }
            for (var i = 0; i < config.lanes.Count; i++)
            {
                if (config.lanes[i].tireTrackWearBoost > 0f)
                {
                    return true;
                }
            }
            return false;
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

        private static void StampStrokes(Color32[] pixels, int W, int H, List<LineStroke> strokes, float wear, float fade, float[] wearMap, float markingWearStrength, int seedBase)
        {
            var applyTireWear = wearMap != null && markingWearStrength > 0f;
            foreach (var stroke in strokes)
            {
                var rng = new System.Random(stroke.seed + seedBase);
                var s = stroke;
                var localWear = LineWeathering.ResolveWear(in s, wear);
                var localFade = LineWeathering.ResolveFade(in s, fade);
                StrokePixelIterator.ForEach(s, W, H, (x, y, idx, duFromCenter, xStart, xEnd) =>
                {
                    var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    var wearFactor = 1f - localWear * 0.5f * Mathf.Abs(noise);
                    var c = s.color * wearFactor;
                    var src = pixels[idx];
                    var bg = new Color(src.r / 255f, src.g / 255f, src.b / 255f);

                    var isEdge = s.shape.HasDiagonalEdges
                        ? Mathf.Abs(duFromCenter) >= s.halfWidthPx - 1f
                        : (x == xStart || x == xEnd - 1);

                    if (isEdge)
                    {
                        var t = 0.45f - localFade * 0.3f;
                        c = Color.Lerp(bg, c, t);
                    }
                    else
                    {
                        c = Color.Lerp(c, Color.gray, localFade * 0.4f);
                    }

                    // タイヤ跡が標示の上を通る位置では、下地色に寄せて摩耗表現を加える。
                    if (applyTireWear)
                    {
                        var localTireWear = Mathf.Clamp01(wearMap[idx]);
                        if (localTireWear > 0f)
                        {
                            var fadeT = localTireWear * markingWearStrength;
                            c = Color.Lerp(c, bg, fadeT);
                        }
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
