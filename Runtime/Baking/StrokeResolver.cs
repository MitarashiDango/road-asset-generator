using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// <see cref="RoadConfig"/> から描画用の <see cref="LineStroke"/> リストを解決する。
    /// 境界線ストロークと、レーンの減速ドットラインの両方を統一的に扱う。
    /// </summary>
    internal static class StrokeResolver
    {
        public static List<LineStroke> Resolve(in BakeContext ctx)
        {
            var strokes = new List<LineStroke>();
            ResolveBoundaryStrokes(strokes, in ctx);
            ResolveSpeedReductionDotLines(strokes, in ctx);
            return strokes;
        }

        // 各境界線は隣接レーン間に専用の U 軸スロットを占有する:
        //   leftShoulder | line[0] スロット | lane[0] | line[1] スロット | lane[1] | ... | line[N] スロット | rightShoulder
        // 各境界線の配置軸はスロット左端 + leftHalf に置かれ、最左 stroke の左端がスロット左端と一致する。
        private static void ResolveBoundaryStrokes(List<LineStroke> strokes, in BakeContext ctx)
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
                    AddStrokeAt(strokes, boundaryMeters[b] + cursor, styles[i], ctx.pxPerMx, ctx.pxPerMy, ref strokeSeed);
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

                    // slant は「車線内側に位置する側が V+ 方向に上がる」と解釈する。LEFT 側 stroke は
                    // 入力 slant をそのまま使い、RIGHT 側 stroke は反転して左右が同じルールに従うように
                    // ミラーする。
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
                seed = seedCounter++,
            });
        }
    }
}
