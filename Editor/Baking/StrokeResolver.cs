using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// <see cref="RoadConfig"/> から描画用の <see cref="LineStroke"/> リストを解決する。
    /// 境界線ストローク、減速ドットライン、減速マーク (山形) を統一的に扱う。
    /// </summary>
    internal static class StrokeResolver
    {
        public static List<LineStroke> Resolve(in BakeContext ctx)
        {
            var strokes = new List<LineStroke>();
            var maskCache = new Dictionary<Texture2D, (float[] pixels, int width, int height)>();
            ResolveBoundaryStrokes(strokes, in ctx, maskCache);
            ResolveSpeedReductionDotLines(strokes, in ctx);
            ResolveDecelerationMarks(strokes, in ctx);
            return strokes;
        }

        // 各境界線は隣接レーン間に専用の U 軸スロットを占有する。
        //   leftShoulder | line[0] スロット | lane[0] | line[1] スロット | lane[1] | ... | line[N] スロット | rightShoulder
        // 各境界線の配置軸はスロット左端 + leftHalf に置かれ、最左ストロークの左端がスロット左端と一致する。
        private static void ResolveBoundaryStrokes(List<LineStroke> strokes, in BakeContext ctx, Dictionary<Texture2D, (float[] pixels, int width, int height)> maskCache)
        {
            var config = ctx.config;
            config.EnsureLineCount();

            var n = config.lanes.Count;
            var boundaryMeters = ComputeBoundaryAxes(config);

            var strokeSeed = ctx.seed + 1000;
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
                    AddStrokeAt(strokes, boundaryMeters[b] + cursor, styles[i], ctx.pxPerMx, ctx.pxPerMy, ref strokeSeed, maskCache);
                    if (i < gapCount)
                    {
                        cursor += Mathf.Max(0f, gaps[i]);
                    }
                }
            }
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

        private static void AddStrokeAt(List<LineStroke> strokes, float xMeters, LineStyle style, float pxPerMx, float pxPerMy, ref int seedCounter, Dictionary<Texture2D, (float[] pixels, int width, int height)> maskCache)
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
            ResolveWearMask(style, pxPerMy, maskCache, out var maskPixels, out var maskW, out var maskH, out var maskStrength, out var maskTileLengthPx);

            strokes.Add(new LineStroke
            {
                xCenter = xCenter,
                halfWidthPx = halfW,
                shape = shape,
                color = style.color,
                paintHeightFactor = style.paintHeightFactor,
                wearOverride = style.lineWeatheringOverride ? style.wearOverrideValue : LineWeathering.UseGlobal,
                fadeOverride = style.lineWeatheringOverride ? style.fadeOverrideValue : LineWeathering.UseGlobal,
                wearMaskPixels = maskPixels,
                wearMaskW = maskW,
                wearMaskH = maskH,
                wearMaskStrength = maskStrength,
                wearMaskTiling = style.wearMaskTiling,
                wearMaskTileLengthPx = maskTileLengthPx,
                seed = seedCounter++,
            });
        }

        private static void ResolveWearMask(
            LineStyle style,
            float pxPerMy,
            Dictionary<Texture2D, (float[] pixels, int width, int height)> maskCache,
            out float[] maskPixels,
            out int maskW,
            out int maskH,
            out float maskStrength,
            out float maskTileLengthPx)
        {
            maskPixels = null;
            maskW = 0;
            maskH = 0;
            maskStrength = 0f;
            maskTileLengthPx = Mathf.Max(0.1f, style.wearMaskTileLengthMeters) * pxPerMy;

            if (style.wearMask == null || style.wearMaskStrength <= 0f)
            {
                return;
            }

            if (!maskCache.TryGetValue(style.wearMask, out var loaded))
            {
                loaded = MaskTextureLoader.LoadGrayscale(style.wearMask);
                maskCache[style.wearMask] = loaded;
            }

            if (loaded.pixels == null || loaded.width <= 0 || loaded.height <= 0)
            {
                return;
            }

            maskPixels = loaded.pixels;
            maskW = loaded.width;
            maskH = loaded.height;
            maskStrength = Mathf.Clamp01(style.wearMaskStrength);
        }

        // 各レーンの speedReductionDotLine 設定から、Y 方向に斜行した Diamond ストロークを生成する。
        // LineStroke の処理経路を共有することで、アルベドやメタリック/スムースネスの生成では
        // 境界線ストロークと同じ扱いが可能。
        private static void ResolveSpeedReductionDotLines(List<LineStroke> strokes, in BakeContext ctx)
        {
            var config = ctx.config;
            var strokeSeed = ctx.seed + 5000;
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

                    // Slant は「車線内側に位置する側が V+ 方向に上がる」と解釈する。
                    // 左側ストロークは入力値をそのまま使い、右側ストロークは符号を反転して同じ規則にそろえる。
                    if (placeLeft)
                    {
                        var xMeters = laneStart + lane.speedReductionDotLineInsetMeters + halfDotWidth;
                        AppendLaneSpeedReductionDotLine(strokes, xMeters, lane, slantSign: +1f, ctx.pxPerMx, ctx.pxPerMy, ref strokeSeed);
                    }
                    if (placeRight)
                    {
                        var xMeters = laneEnd - lane.speedReductionDotLineInsetMeters - halfDotWidth;
                        AppendLaneSpeedReductionDotLine(strokes, xMeters, lane, slantSign: -1f, ctx.pxPerMx, ctx.pxPerMy, ref strokeSeed);
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
                wearOverride = LineWeathering.UseGlobal,
                fadeOverride = LineWeathering.UseGlobal,
                seed = seedCounter++,
            });
        }

        // 各レーンの decelerationMark 設定から V 字型ストロークを生成する。
        // レーン中央に V 字を周期配置し、進行方向 (Forward/Backward) に応じて頂点の向きを反転する。
        private static void ResolveDecelerationMarks(List<LineStroke> strokes, in BakeContext ctx)
        {
            var config = ctx.config;
            var strokeSeed = ctx.seed + 7000;
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

                if (lane.decelerationMark
                    && lane.decelerationMarkWidthMeters > 0f
                    && lane.decelerationMarkHeightMeters > 0f)
                {
                    AppendLaneDecelerationMark(strokes, lane, laneStart, laneEnd, ctx.pxPerMx, ctx.pxPerMy, ref strokeSeed);
                }

                pos = laneEnd;
            }
        }

        private static void AppendLaneDecelerationMark(List<LineStroke> strokes, LaneConfig lane, float laneStart, float laneEnd, float pxPerMx, float pxPerMy, ref int seedCounter)
        {
            // レーン中央に配置する。
            var xCenterMeters = (laneStart + laneEnd) * 0.5f;
            var xCenter = Mathf.RoundToInt(xCenterMeters * pxPerMx);

            // 半幅 = min(指定幅 / 2, レーン半幅 - inset)。指定幅がレーン幅を超える場合はクリップする。
            var availableHalfWidth = Mathf.Max(0.05f, lane.widthMeters * 0.5f - lane.decelerationMarkInsetMeters);
            var halfWidthMeters = Mathf.Min(lane.decelerationMarkWidthMeters * 0.5f, availableHalfWidth);
            var halfW = Mathf.Max(1, Mathf.RoundToInt(halfWidthMeters * pxPerMx));

            // 進行方向で V 字の頂点を決定する。
            // 内部 v 軸 [0..1] は MarkingPattern が周期計算後に渡すローカル座標で、
            // v=0 が周期の先頭 (テクスチャ y が小さい側)、v=1 が末尾 (テクスチャ y が大きい側)。
            // 通常 Unity の道路メッシュでは V+ (Forward) がテクスチャ y 大の方向に対応するため、
            //   Forward → 頂点を v=1 側 (テクスチャ y 大 = 進行方向の前方) に置くため pointAtTop=false
            //   Backward → 頂点を v=0 側 (進行方向の前方) に置くため pointAtTop=true
            var pointAtTop = lane.direction == LaneDirection.Backward;

            // 厚みは「足元の V 軸方向の長さ」として正規化する。
            // ChevronPrimitive の h パラメータは Mark Height に対する足元 V 長の比率。
            var thicknessNormV = lane.decelerationMarkHeightMeters > 0f
                ? lane.decelerationMarkThicknessMeters / lane.decelerationMarkHeightMeters
                : 0.2f;

            var shape = new MarkingPattern(
                new ChevronPrimitive(thicknessNormV, pointAtTop),
                lane.decelerationMarkHeightMeters * pxPerMy,
                lane.decelerationMarkSpacingMeters * pxPerMy,
                lane.decelerationMarkStartOffsetMeters * pxPerMy);

            strokes.Add(new LineStroke
            {
                xCenter = xCenter,
                halfWidthPx = halfW,
                shape = shape,
                color = lane.decelerationMarkColor,
                paintHeightFactor = lane.decelerationMarkPaintHeightFactor,
                wearOverride = LineWeathering.UseGlobal,
                fadeOverride = LineWeathering.UseGlobal,
                seed = seedCounter++,
            });
        }
    }
}
