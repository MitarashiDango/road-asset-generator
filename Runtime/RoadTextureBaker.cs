using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary><see cref="RoadTextureBaker.Bake"/> が生成するテクスチャ群。</summary>
    public class GeneratedTextures
    {
        public Texture2D albedo;
        public Texture2D normal;
        // RGB = 0(非メタリック)、A = smoothness。
        public Texture2D metallicSmoothness;
        public Texture2D ao;
    }

    /// <summary>1 本の線ストロークをピクセル空間に解決した中間表現。</summary>
    internal struct LineStroke
    {
        public int xCenter;
        public int halfWidthPx;
        public IMarkingShape shape;
        public Color color;
        public float paintHeightFactor;
        public int seed;
    }

    /// <summary>
    /// <see cref="RoadConfig"/> から道路面テクスチャ(albedo / normal / metallic-smoothness / AO)
    /// を手続き的に生成する。Editor に依存しない Runtime API として提供され、ディスクへの保存は
    /// Editor 側のレイヤが担う。
    /// </summary>
    public static class RoadTextureBaker
    {
        // 塗装マークの高さマップ寄与の単位値。strength = 1、factor = 1、lineWear = 0 のときに
        // 法線マップ上で塗装が見えつつアスファルト微細ノイズを覆い尽くさない値を選んでいる。
        private const float PaintHeightUnit = 25f;

        /// <summary><paramref name="config"/> に従ってテクスチャ群をベイクする。</summary>
        public static GeneratedTextures Bake(RoadConfig config)
        {
            var W = (int)config.output.resolution;
            var H = (int)config.output.resolution;
            var seed = config.output.seed;

            var totalWidth = config.TotalWidthMeters;
            if (totalWidth <= 0f)
            {
                throw new InvalidOperationException("Total road width must be > 0.");
            }

            var pxPerMeterX = W / totalWidth;
            var pxPerMeterY = H / config.output.textureLengthMeters;

            var strokes = ResolveStrokes(config, pxPerMeterX, pxPerMeterY, seed);
            AddLaneSpeedReductionDotLineStrokes(strokes, config, pxPerMeterX, pxPerMeterY, seed + 5000);

            var leftShoulderEnd = Mathf.RoundToInt(config.leftShoulder.widthMeters * pxPerMeterX);
            var rightShoulderStart = W - Mathf.RoundToInt(config.rightShoulder.widthMeters * pxPerMeterX);
            var lanePixelRanges = ComputeLanePixelRanges(config, pxPerMeterX);

            var heightMap = RoadNoise.StyleNoise(W, H, config.asphalt.noiseStyle, 1f, seed + 100);

            var albedoPixels = BuildAlbedo(config, W, H, leftShoulderEnd, rightShoulderStart, lanePixelRanges, pxPerMeterX, pxPerMeterY, heightMap, strokes, seed);
            var albedoTex = MakeTextureLinear(albedoPixels, W, H);

            // 塗装の凸を高さマップに加算する処理は Albedo パスのあとに実行する。アスファルト
            // シェーディングへの影響を避け、法線マップだけが塗装の凸を反映するようにするため。
            StampPaintHeight(heightMap, W, H, config, strokes, lanePixelRanges, pxPerMeterX, pxPerMeterY);

            Texture2D normalTex = null;
            if (config.output.generateNormal)
            {
                var normalPixels = BuildNormal(heightMap, W, H);
                normalTex = MakeTextureLinear(normalPixels, W, H);
            }

            Texture2D msTex = null;
            if (config.output.generateMetallicSmoothness)
            {
                var msPixels = BuildMetallicSmoothness(config, W, H, lanePixelRanges, pxPerMeterX, pxPerMeterY, strokes, seed);
                msTex = MakeTextureLinear(msPixels, W, H);
            }

            Texture2D aoTex = null;
            if (config.output.generateAO)
            {
                var aoPixels = BuildAO(W, H, seed);
                aoTex = MakeTextureLinear(aoPixels, W, H);
            }

            return new GeneratedTextures
            {
                albedo = albedoTex,
                normal = normalTex,
                metallicSmoothness = msTex,
                ao = aoTex,
            };
        }

        // -----------------------------------------------------------------
        // レイアウト / stroke 解決
        // -----------------------------------------------------------------

        // 各境界線は隣接レーン間に専用の U 軸スロットを占有する:
        //   leftShoulder | line[0] スロット | lane[0] | line[1] スロット | lane[1] | ... | line[N] スロット | rightShoulder
        // 各境界線の配置軸はスロット左端 + leftHalf に置かれ、最左 stroke の左端がスロット左端と一致する。
        private static List<LineStroke> ResolveStrokes(RoadConfig config, float pxPerMx, float pxPerMy, int seed)
        {
            var strokes = new List<LineStroke>();
            config.EnsureLineCount();

            var n = config.lanes.Count;
            var boundaryMeters = ComputeBoundaryAxes(config);

            var strokeSeed = seed + 1000;
            for (var b = 0; b <= n; b++)
            {
                var line = config.lines[b];
                var styles = line.strokes;
                if (styles == null || styles.Count == 0)
                {
                    continue;
                }

                if (!HasAnyDrawnStroke(styles))
                {
                    continue;
                }

                var gaps = line.spacingsMeters;
                var gapCount = Mathf.Min(styles.Count - 1, gaps?.Count ?? 0);
                var totalSpacing = SumSpacings(gaps, gapCount);

                var cursor = -totalSpacing * 0.5f;
                for (var i = 0; i < styles.Count; i++)
                {
                    AddStrokeAt(strokes, boundaryMeters[b] + cursor, styles[i], pxPerMx, pxPerMy, ref strokeSeed);
                    if (i < gapCount)
                    {
                        cursor += Mathf.Max(0f, gaps[i]);
                    }
                }
            }
            return strokes;
        }

        private static float[] ComputeBoundaryAxes(RoadConfig config)
        {
            var n = config.lanes.Count;
            var boundaryMeters = new float[n + 1];
            var pos = config.leftShoulder.widthMeters;
            for (var b = 0; b <= n; b++)
            {
                config.lines[b].ComputeSlotInfo(out var lh, out _, out var sw);
                boundaryMeters[b] = pos + lh;
                pos += sw;
                if (b < n)
                {
                    pos += config.lanes[b].widthMeters;
                }
            }
            return boundaryMeters;
        }

        private static bool HasAnyDrawnStroke(List<LineStyle> styles)
        {
            for (var i = 0; i < styles.Count; i++)
            {
                if (styles[i].type != LineType.None && styles[i].widthMeters > 0f)
                {
                    return true;
                }
            }
            return false;
        }

        private static float SumSpacings(List<float> gaps, int gapCount)
        {
            var total = 0f;
            for (var i = 0; i < gapCount; i++)
            {
                total += Mathf.Max(0f, gaps[i]);
            }
            return total;
        }

        private static void AddStrokeAt(List<LineStroke> strokes, float xMeters, LineStyle style, float pxPerMx, float pxPerMy, ref int seedCounter)
        {
            if (style.type == LineType.None || style.widthMeters <= 0f)
            {
                return;
            }

            var xCenter = Mathf.RoundToInt(xMeters * pxPerMx);
            var halfW = Mathf.Max(1, Mathf.RoundToInt(style.widthMeters * pxPerMx * 0.5f));

            IMarkingShape shape;
            switch (style.type)
            {
                case LineType.Solid:
                    shape = SolidShape.Instance;
                    break;
                case LineType.Dashed:
                    shape = new MarkingPattern(
                        RectanglePrimitive.Instance,
                        style.dashLengthMeters * pxPerMy,
                        style.dashGapMeters * pxPerMy,
                        style.dashOffsetMeters * pxPerMy);
                    break;
                case LineType.Diamond:
                    var shearNorm = (style.diamondSlantMeters * pxPerMx) / halfW;
                    shape = new MarkingPattern(
                        new ParallelogramPrimitive(shearNorm),
                        style.diamondSizeMeters * pxPerMy,
                        style.diamondSpacingMeters * pxPerMy,
                        style.dashOffsetMeters * pxPerMy);
                    break;
                default:
                    return;
            }
            strokes.Add(new LineStroke
            {
                xCenter = xCenter,
                halfWidthPx = halfW,
                shape = shape,
                color = style.color,
                paintHeightFactor = style.paintHeightFactor,
                seed = seedCounter++,
            });
        }

        // 各レーンの speedReductionDotLine 設定から派生する Y-shear Diamond stroke を生成する。
        // LineStroke パイプラインを共有することで、StampStrokes / BuildMetallicSmoothness は境界線
        // ストロークと同じ扱いで処理できる。
        private static void AddLaneSpeedReductionDotLineStrokes(List<LineStroke> strokes, RoadConfig config, float pxPerMx, float pxPerMy, int seedBase)
        {
            var strokeSeed = seedBase;
            config.EnsureLineCount();
            var pos = config.leftShoulder.widthMeters;
            for (var b = 0; b <= config.lanes.Count; b++)
            {
                config.lines[b].ComputeSlotInfo(out _, out _, out var slotWidth);
                pos += slotWidth;
                if (b >= config.lanes.Count)
                {
                    break;
                }

                var lane = config.lanes[b];
                var laneStart = pos;
                var laneEnd = laneStart + lane.widthMeters;

                if (lane.speedReductionDotLine
                    && lane.speedReductionDotLineWidthMeters > 0f
                    && lane.speedReductionDotLineHeightMeters > 0f)
                {
                    var halfDotWidth = lane.speedReductionDotLineWidthMeters * 0.5f;
                    var placeLeft  = lane.speedReductionDotLineSide == SpeedReductionDotLineSide.Left  || lane.speedReductionDotLineSide == SpeedReductionDotLineSide.Both;
                    var placeRight = lane.speedReductionDotLineSide == SpeedReductionDotLineSide.Right || lane.speedReductionDotLineSide == SpeedReductionDotLineSide.Both;

                    // slant は「車線内側に位置する側が V+ 方向に上がる」と解釈する。LEFT 側 stroke は
                    // 入力 slant をそのまま使い、RIGHT 側 stroke は反転して左右が同じルールに従うように
                    // ミラーする。
                    if (placeLeft)
                    {
                        var xMeters = laneStart + lane.speedReductionDotLineInsetMeters + halfDotWidth;
                        AppendLaneSpeedReductionDotLine(strokes, xMeters, lane, slantSign: +1f, pxPerMx, pxPerMy, ref strokeSeed);
                    }
                    if (placeRight)
                    {
                        var xMeters = laneEnd - lane.speedReductionDotLineInsetMeters - halfDotWidth;
                        AppendLaneSpeedReductionDotLine(strokes, xMeters, lane, slantSign: -1f, pxPerMx, pxPerMy, ref strokeSeed);
                    }
                }
                pos = laneEnd;
            }
        }

        private static void AppendLaneSpeedReductionDotLine(List<LineStroke> strokes, float xMeters, LaneConfig lane, float slantSign, float pxPerMx, float pxPerMy, ref int seedCounter)
        {
            var xCenter = Mathf.RoundToInt(xMeters * pxPerMx);
            var halfW = Mathf.Max(1, Mathf.RoundToInt(lane.speedReductionDotLineWidthMeters * pxPerMx * 0.5f));
            var directionSign = lane.direction == LaneDirection.Backward ? -1f : 1f;
            strokes.Add(new LineStroke
            {
                xCenter = xCenter,
                halfWidthPx = halfW,
                shape = new MarkingPattern(
                    RectanglePrimitive.Instance,
                    lane.speedReductionDotLineHeightMeters * pxPerMy,
                    lane.speedReductionDotLineSpacingMeters * pxPerMy,
                    lane.speedReductionDotLineStartOffsetMeters * pxPerMy,
                    lane.speedReductionDotLineSlantMeters * slantSign * directionSign * pxPerMy),
                color = lane.speedReductionDotLineColor,
                paintHeightFactor = lane.speedReductionDotLinePaintHeightFactor,
                seed = seedCounter++,
            });
        }

        // -----------------------------------------------------------------
        // ピクセル走査ヘルパー(stroke / rumble strip 共通)
        // -----------------------------------------------------------------
        private delegate void StrokePixelAction(int x, int y, int idx, float duFromCenter, int xStart, int xEnd);

        private static void ForEachStrokePixel(in LineStroke s, int W, int H, StrokePixelAction action)
        {
            var slantPad = s.shape.GetSlantPad(s.halfWidthPx);
            var xStart = s.xCenter - s.halfWidthPx - slantPad;
            var xEnd = s.xCenter + s.halfWidthPx + slantPad;
            if (xEnd <= xStart)
            {
                xEnd = xStart + 1;
            }

            for (var y = 0; y < H; y++)
            {
                if (s.shape.CanSkipRow(y))
                {
                    continue;
                }
                for (var x = xStart; x < xEnd; x++)
                {
                    if (x < 0 || x >= W)
                    {
                        continue;
                    }
                    if (!s.shape.TestPixel(x, y, s.xCenter, s.halfWidthPx, out var duFromCenter))
                    {
                        continue;
                    }
                    action(x, y, y * W + x, duFromCenter, xStart, xEnd);
                }
            }
        }

        // 1 本の rumble strip を描画するために必要な、すべてのパスで共通のパラメータ。
        private struct RumbleStripParams
        {
            public float widthPx;
            public float periodPx;
            public float offsetPx;
            public int xStart;
            public int xEnd;
        }

        private static bool TryGetRumbleStripParams(LaneConfig lane, LaneRange laneRange, int W, float pxPerMx, float pxPerMy, out RumbleStripParams parameters)
        {
            parameters = default;
            if (!lane.rumbleStrip)
            {
                return false;
            }

            var widthM = Mathf.Max(0.05f, lane.rumbleStripWidthMeters);
            var spacingM = Mathf.Max(widthM + 0.05f, lane.rumbleStripSpacingMeters);
            var periodM = widthM + spacingM;
            var insetPx = lane.rumbleStripInsetMeters * pxPerMx;

            parameters.widthPx = widthM * pxPerMy;
            parameters.periodPx = periodM * pxPerMy;
            parameters.offsetPx = lane.rumbleStripStartOffsetMeters * pxPerMy;
            parameters.xStart = Mathf.Max(0, laneRange.xStart + Mathf.RoundToInt(insetPx));
            parameters.xEnd = Mathf.Min(W, laneRange.xEnd - Mathf.RoundToInt(insetPx));
            return parameters.xEnd > parameters.xStart;
        }

        // ForEachRumblePixel のコールバック。<paramref name="alpha"/> は 0..1 のフェザリング値で、
        // 帯の内側で 1 に近づき V 軸の縁付近で 0 に向かって減衰する。
        private delegate void RumblePixelAction(int x, int y, int idx, float alpha);

        private static void ForEachRumblePixel(in RumbleStripParams p, int W, int H, RumblePixelAction action)
        {
            for (var y = 0; y < H; y++)
            {
                var t = (y - p.offsetPx) % p.periodPx;
                if (t < 0)
                {
                    t += p.periodPx;
                }
                if (t >= p.widthPx)
                {
                    continue;
                }
                var edgeT = Mathf.Min(t, p.widthPx - t);
                var alpha = Mathf.Clamp01(edgeT);
                for (var x = p.xStart; x < p.xEnd; x++)
                {
                    action(x, y, y * W + x, alpha);
                }
            }
        }

        // -----------------------------------------------------------------
        // レーンのピクセル範囲
        // -----------------------------------------------------------------

        /// <summary>レーンの走行可能領域を表すピクセル範囲 [xStart, xEnd)。</summary>
        internal struct LaneRange
        {
            public int xStart;
            public int xEnd;
        }

        private static LaneRange[] ComputeLanePixelRanges(RoadConfig config, float pxPerMx)
        {
            config.EnsureLineCount();
            var ranges = new LaneRange[config.lanes.Count];
            var pos = config.leftShoulder.widthMeters;
            for (var b = 0; b <= config.lanes.Count; b++)
            {
                config.lines[b].ComputeSlotInfo(out _, out _, out var slotWidth);
                pos += slotWidth;
                if (b < config.lanes.Count)
                {
                    var xs = Mathf.RoundToInt(pos * pxPerMx);
                    var laneEndM = pos + config.lanes[b].widthMeters;
                    var xe = Mathf.RoundToInt(laneEndM * pxPerMx);
                    ranges[b] = new LaneRange { xStart = xs, xEnd = xe };
                    pos = laneEndM;
                }
            }
            return ranges;
        }

        // -----------------------------------------------------------------
        // Albedo
        // -----------------------------------------------------------------
        private static Color32[] BuildAlbedo(RoadConfig config, int W, int H, int leftSh, int rightSh, LaneRange[] laneRanges, float pxPerMx, float pxPerMy, float[] heightMap, List<LineStroke> strokes, int seed)
        {
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

                    pixels[idx] = ToColor32(c);
                }
            }

            // 減速マークと境界線ストロークはアスファルトの上から重ね描きする。
            StampRumbleStrips(pixels, W, H, config, laneRanges, pxPerMx, pxPerMy, seed + 350);
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

        // -----------------------------------------------------------------
        // 各種スタンプ処理
        // -----------------------------------------------------------------
        private static void StampRumbleStrips(Color32[] pixels, int W, int H, RoadConfig config, LaneRange[] laneRanges, float pxPerMx, float pxPerMy, int seedBase)
        {
            for (var li = 0; li < config.lanes.Count; li++)
            {
                var lane = config.lanes[li];
                if (!TryGetRumbleStripParams(lane, laneRanges[li], W, pxPerMx, pxPerMy, out var sp))
                {
                    continue;
                }

                var rng = new System.Random(seedBase + li * 31);
                var baseColor = lane.rumbleStripColor;

                ForEachRumblePixel(in sp, W, H, (x, y, idx, alpha) =>
                {
                    var n = (float)(rng.NextDouble() * 2 - 1) * 0.06f;
                    var c = new Color(
                        Mathf.Clamp01(baseColor.r + n),
                        Mathf.Clamp01(baseColor.g + n),
                        Mathf.Clamp01(baseColor.b + n));
                    var src = pixels[idx];
                    var sourceCol = new Color(src.r / 255f, src.g / 255f, src.b / 255f);
                    pixels[idx] = ToColor32(Color.Lerp(sourceCol, c, alpha));
                });
            }
        }

        // 塗装マーク(境界線ストローク、レーンの山形マーカー、減速マーク)の高さを高さマップに加算し、
        // 法線マップに塗装の凸が現れるようにする。lineWear が 1 に近づくにつれて塗装高さは減衰する。
        private static void StampPaintHeight(float[] heightMap, int W, int H, RoadConfig config, List<LineStroke> strokes, LaneRange[] laneRanges, float pxPerMx, float pxPerMy)
        {
            var wearMul = Mathf.Clamp01(1f - config.weathering.lineWear);
            var strength = Mathf.Max(0f, config.weathering.paintHeightStrength);
            if (wearMul <= 0f || strength <= 0f)
            {
                return;
            }

            var globalScale = PaintHeightUnit * strength * wearMul;

            foreach (var stroke in strokes)
            {
                if (stroke.paintHeightFactor <= 0f)
                {
                    continue;
                }
                var h = globalScale * stroke.paintHeightFactor;
                ForEachStrokePixel(stroke, W, H, (x, y, idx, _, _, _) =>
                {
                    heightMap[idx] += h;
                });
            }

            for (var li = 0; li < config.lanes.Count; li++)
            {
                var lane = config.lanes[li];
                if (lane.rumbleStripPaintHeightFactor <= 0f)
                {
                    continue;
                }
                if (!TryGetRumbleStripParams(lane, laneRanges[li], W, pxPerMx, pxPerMy, out var sp))
                {
                    continue;
                }
                var h = globalScale * lane.rumbleStripPaintHeightFactor;
                ForEachRumblePixel(in sp, W, H, (x, y, idx, _) =>
                {
                    heightMap[idx] += h;
                });
            }
        }

        private static void StampStrokes(Color32[] pixels, int W, int H, List<LineStroke> strokes, float wear, float fade, int seedBase)
        {
            foreach (var stroke in strokes)
            {
                var rng = new System.Random(stroke.seed + seedBase);
                var s = stroke;
                ForEachStrokePixel(s, W, H, (x, y, idx, duFromCenter, xStart, xEnd) =>
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
                    pixels[idx] = ToColor32(c);
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
                        pixels[yw * W + x] = ToColor32(blended);
                    }
                }
            }
        }

        // -----------------------------------------------------------------
        // 法線マップ(高さマップから生成)
        // -----------------------------------------------------------------
        private static Color32[] BuildNormal(float[] heightMap, int W, int H)
        {
            var pixels = new Color32[W * H];
            // 高さマップは概ね [-30, 30] の範囲。法線の見た目が自然になるようチューニング済みの強度。
            const float strength = 4.5f / 255f;
            for (var y = 0; y < H; y++)
            {
                var yp = (y + 1) % H;
                var yn = (y - 1 + H) % H;
                for (var x = 0; x < W; x++)
                {
                    var xp = (x + 1) % W;
                    var xn = (x - 1 + W) % W;
                    var dx = (heightMap[y * W + xp] - heightMap[y * W + xn]) * 0.5f * strength;
                    var dy = (heightMap[yp * W + x] - heightMap[yn * W + x]) * 0.5f * strength;
                    const float nz = 1f;
                    var len = Mathf.Sqrt(dx * dx + dy * dy + nz * nz);
                    var nx = -dx / len;
                    var ny = -dy / len;
                    var nzn = nz / len;
                    var rByte = (byte)Mathf.Clamp((nx + 1f) * 0.5f * 255f, 0, 255);
                    var gByte = (byte)Mathf.Clamp((-ny + 1f) * 0.5f * 255f, 0, 255);
                    var bByte = (byte)Mathf.Clamp((nzn + 1f) * 0.5f * 255f, 0, 255);
                    pixels[y * W + x] = new Color32(rByte, gByte, bByte, 255);
                }
            }
            return pixels;
        }

        // -----------------------------------------------------------------
        // Metallic / Smoothness パック:RGB = 0(非メタリック)、A = smoothness
        // -----------------------------------------------------------------
        private static Color32[] BuildMetallicSmoothness(RoadConfig config, int W, int H, LaneRange[] laneRanges, float pxPerMx, float pxPerMy, List<LineStroke> strokes, int seed)
        {
            var pixels = new Color32[W * H];
            // アスファルトは粗い (~0.16)。濡れ路面では全体的に smoothness を上げる。
            var asphaltSmooth = config.weathering.wetSurface ? (byte)180 : (byte)40;
            var noise = RoadNoise.GaussianBlurWrap(RoadNoise.WhiteNoise(W, H, seed + 601), W, H, 2f);

            for (var i = 0; i < pixels.Length; i++)
            {
                var v = asphaltSmooth + (int)(noise[i] * 14f);
                v = Mathf.Clamp(v, 0, 255);
                pixels[i] = new Color32(0, 0, 0, (byte)v);
            }

            // 塗装マーク(減速マークと線 stroke)はアスファルトより滑らか。
            var lineSmooth = config.weathering.wetSurface ? (byte)220 : (byte)110;

            for (var li = 0; li < config.lanes.Count; li++)
            {
                var lane = config.lanes[li];
                if (!TryGetRumbleStripParams(lane, laneRanges[li], W, pxPerMx, pxPerMy, out var sp))
                {
                    continue;
                }
                ForEachRumblePixel(in sp, W, H, (x, y, idx, _) =>
                {
                    pixels[idx].a = lineSmooth;
                });
            }

            foreach (var stroke in strokes)
            {
                ForEachStrokePixel(stroke, W, H, (x, y, idx, _, _, _) =>
                {
                    pixels[idx].a = lineSmooth;
                });
            }
            return pixels;
        }

        // -----------------------------------------------------------------
        // AO
        // -----------------------------------------------------------------
        private static Color32[] BuildAO(int W, int H, int seed)
        {
            var pixels = new Color32[W * H];
            var noise = RoadNoise.GaussianBlurWrap(RoadNoise.WhiteNoise(W, H, seed + 700), W, H, 4f);
            for (var i = 0; i < pixels.Length; i++)
            {
                var v = 248 + (int)(noise[i] * 5f);
                v = Mathf.Clamp(v, 0, 255);
                pixels[i] = new Color32((byte)v, (byte)v, (byte)v, 255);
            }
            return pixels;
        }

        // -----------------------------------------------------------------
        // テクスチャ生成
        // -----------------------------------------------------------------
        private static Texture2D MakeTextureLinear(Color32[] pixels, int W, int H)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true, false);
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        private static Color32 ToColor32(Color c)
        {
            return new Color32(
                (byte)Mathf.Clamp(c.r * 255f, 0, 255),
                (byte)Mathf.Clamp(c.g * 255f, 0, 255),
                (byte)Mathf.Clamp(c.b * 255f, 0, 255),
                (byte)Mathf.Clamp(c.a * 255f, 0, 255));
        }
    }
}
