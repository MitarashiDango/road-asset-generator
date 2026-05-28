using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    public enum LineType { None, Solid, Dashed, Diamond }
    public enum NoiseStyle { Smooth, Standard, Aggregate, Coarse, Worn, Concrete }
    public enum PipelineTarget { AutoDetect, BuiltIn, URP }
    public enum TextureResolution { _512 = 512, _1024 = 1024, _2048 = 2048, _4096 = 4096 }
    public enum SpeedReductionDotLineSide { Left, Right, Both }
    public enum WearMaskTiling { StretchAlongV, RepeatAlongV }

    /// <summary>
    /// レーンごとの進行方向(V 軸方向)。Forward = V+(視点 V=0 から遠ざかる方向)、
    /// Backward = V-(対向)。山形マーカーの slant 自動反転に利用される。
    /// </summary>
    public enum LaneDirection { Forward, Backward }

    /// <summary>1 本の線ストロークを描画するための見た目設定。</summary>
    [Serializable]
    public class LineStyle
    {
        public LineType type = LineType.Solid;
        public Color color = Color.white;
        [Min(0f)] public float widthMeters = 0.15f;

        // Dashed 専用パラメータ
        [Min(0.05f)] public float dashLengthMeters = 5f;
        [Min(0.05f)] public float dashGapMeters = 5f;

        // V 軸方向の位相オフセット(Dashed と Diamond で共有)。テクスチャ上端 (V=0) からの距離。
        public float dashOffsetMeters = 2.5f;

        // Diamond 専用パラメータ。V 軸方向に繰り返されるシアー長方形(平行四辺形)。
        // 既定: size 1.0m + spacing 1.0m = 2.0m 周期。タイル長 10m を整数等分 (5 マーク/タイル)。
        [Min(0.05f)] public float diamondSizeMeters = 1.0f;
        [Min(0.05f)] public float diamondSpacingMeters = 1.0f;
        public float diamondSlantMeters = 0.3f;

        /// <summary>
        /// この stroke が高さマップ(およびそれを介して法線マップ)に与えるペイント厚みの倍率。
        /// 0 = 平坦、1 = 標準、1 超 = より厚塗り。<see cref="WeatheringConfig.paintHeightStrength"/>
        /// と <see cref="WeatheringConfig.lineWear"/> で全体スケールが調整される。
        /// </summary>
        [Min(0f)] public float paintHeightFactor = 1.0f;

        // ---- Stroke weathering override ----
        // OFF の場合は WeatheringConfig.lineWear / lineFade を使用する。
        public bool lineWeatheringOverride = false;
        [Range(0f, 1f)] public float wearOverrideValue = 0.15f;
        [Range(0f, 1f)] public float fadeOverrideValue = 0.08f;
        public Texture2D wearMask;
        [Range(0f, 1f)] public float wearMaskStrength = 1.0f;
        public WearMaskTiling wearMaskTiling = WearMaskTiling.StretchAlongV;
        [Min(0.1f)] public float wearMaskTileLengthMeters = 10f;

        public LineStyle Clone()
        {
            return new LineStyle
            {
                type = type,
                color = color,
                widthMeters = widthMeters,
                dashLengthMeters = dashLengthMeters,
                dashGapMeters = dashGapMeters,
                dashOffsetMeters = dashOffsetMeters,
                diamondSizeMeters = diamondSizeMeters,
                diamondSpacingMeters = diamondSpacingMeters,
                diamondSlantMeters = diamondSlantMeters,
                paintHeightFactor = paintHeightFactor,
                lineWeatheringOverride = lineWeatheringOverride,
                wearOverrideValue = wearOverrideValue,
                fadeOverrideValue = fadeOverrideValue,
                wearMask = wearMask,
                wearMaskStrength = wearMaskStrength,
                wearMaskTiling = wearMaskTiling,
                wearMaskTileLengthMeters = wearMaskTileLengthMeters,
            };
        }
    }

    /// <summary>
    /// レーン間(またはレーンと路側帯の間)の境界線。任意本数の stroke レイヤーを保持し、
    /// 隣接 stroke 間の中心-中心間隔(spacingsMeters)を明示的に持つ。
    /// </summary>
    [Serializable]
    public class LineConfig
    {
        public string label = "Line";

        // この境界に描かれる stroke 群。U 軸方向に左から右の物理順で並ぶ。
        public List<LineStyle> strokes = new List<LineStyle>();

        // 隣接 stroke 間の中心-中心間隔。要素数は strokes.Count - 1 となる。
        public List<float> spacingsMeters = new List<float>();

        public static LineConfig Single(string label, LineStyle s)
            => new LineConfig
            {
                label = label,
                strokes = new List<LineStyle> { s },
            };

        public static LineConfig Double(string label, LineStyle a, LineStyle b, float spacing)
            => new LineConfig
            {
                label = label,
                strokes = new List<LineStyle> { a, b },
                spacingsMeters = new List<float> { spacing },
            };

        /// <summary>
        /// この境界線が U 軸方向に占める範囲(slot)を計算する。配置軸(placement axis)
        /// は stroke が配置される位置(= ベイカが利用する境界座標)。stroke 幅が左右対称なら
        /// <paramref name="leftHalf"/> と <paramref name="rightHalf"/> は等しい。
        /// </summary>
        /// <param name="leftHalf">slot 左端から配置軸までの距離。</param>
        /// <param name="rightHalf">配置軸から slot 右端までの距離。</param>
        /// <param name="slotWidth">slot が U 軸方向に占める総幅。</param>
        public void ComputeSlotInfo(out float leftHalf, out float rightHalf, out float slotWidth)
        {
            leftHalf = 0f;
            rightHalf = 0f;
            slotWidth = 0f;
            if (strokes == null || strokes.Count == 0)
            {
                return;
            }

            var gapCount = Mathf.Min(strokes.Count - 1, spacingsMeters?.Count ?? 0);
            var totalSpacing = 0f;
            for (var i = 0; i < gapCount; i++)
            {
                totalSpacing += Mathf.Max(0f, spacingsMeters[i]);
            }

            var cursor = -totalSpacing * 0.5f;
            var minU = 0f;
            var maxU = 0f;
            var any = false;
            for (var i = 0; i < strokes.Count; i++)
            {
                var s = strokes[i];
                if (s != null && s.type != LineType.None && s.widthMeters > 0f)
                {
                    var halfW = s.widthMeters * 0.5f;
                    var l = cursor - halfW;
                    var r = cursor + halfW;
                    if (!any)
                    {
                        minU = l;
                        maxU = r;
                        any = true;
                    }
                    else
                    {
                        if (l < minU) { minU = l; }
                        if (r > maxU) { maxU = r; }
                    }
                }
                if (i < gapCount)
                {
                    cursor += Mathf.Max(0f, spacingsMeters[i]);
                }
            }
            if (any)
            {
                leftHalf = -minU;
                rightHalf = maxU;
                slotWidth = leftHalf + rightHalf;
            }
        }
    }

    /// <summary>1 つの走行レーン。</summary>
    [Serializable]
    public class LaneConfig
    {
        public string label = "Lane";
        [Min(0.5f)] public float widthMeters = 3.0f;

        /// <summary>
        /// V 軸方向の進行方向。山形マーカーに影響し、Backward レーンでは slant が自動反転されるため、
        /// シェブロンは常に進行方向を指す。
        /// </summary>
        public LaneDirection direction = LaneDirection.Forward;

        // ---- タイヤ跡摩耗 (Per-lane tire track wear boost) ----
        // 全体設定 (Weathering.tireTrackWear) に加算されるレーン固有の追加摩耗。
        // 例: 全体 0.2 + レーン 0.3 → このレーンの実効摩耗強度は 0.5 (clamp 1.0)。
        [Range(0f, 1f)] public float tireTrackWearBoost = 0f;
        public bool tireTrackOverride = false;
        [Min(0.05f)] public float tireTrackWidthMeters = 1.08f;
        [Min(0f)] public float tireTrackSpacingMeters = 1.70f;
        public Color tireTrackColor = new Color(0.05f, 0.05f, 0.06f);
        [Range(0f, 1f)] public float tireTrackOpacity = 0.30f;

        // ---- 路面色(カーブ警戒の赤路面など) ----
        public bool surfaceTint = false;
        public Color surfaceTintColor = new Color(0.55f, 0.20f, 0.18f);
        [Range(0f, 1f)] public float surfaceTintStrength = 0.7f;

        // ---- 減速帯 (Rumble strips) ----
        // 既定: width 0.30m + spacing 0.70m = 1.0m 周期。タイル長 10m を整数等分 (10 帯/タイル)。
        // 路面凹凸構造指針の標準寸法に準拠。
        public bool rumbleStrip = false;
        [Min(0.05f)] public float rumbleStripWidthMeters = 0.30f;
        [Min(0.1f)]  public float rumbleStripSpacingMeters = 0.70f;
        public Color rumbleStripColor = Color.white;
        // V 軸の位相オフセット (0..tile 長)。spacing/2 を既定とすることでパターンがタイル内中央配置になる
        // (LineStyle.dashOffsetMeters の流儀に揃えている)。
        public float rumbleStripStartOffsetMeters = 0.35f;
        // 区画線と帯が重ならないようにするための、レーン端からの U 軸方向のインセット。
        [Min(0f)] public float rumbleStripInsetMeters = 0.20f;
        // 減速帯は細い区画線より厚塗りされる傾向にあるため、既定 1.5 倍。
        [Min(0f)] public float rumbleStripPaintHeightFactor = 1.5f;

        // ---- 減速ドットライン (Speed Reduction Dot Line) ----
        // 国土交通省関東地方整備局による正式名称。レーン端に沿って斜行した平行四辺形 (ドット) が
        // 一定間隔で並び、視覚的に走行速度を抑制する効果を狙った路面標示。
        // 既定: height 1.0m + spacing 1.0m = 2.0m 周期。タイル長 10m を整数等分 (5 ドット/タイル)。
        public bool speedReductionDotLine = false;
        [Min(0.05f)] public float speedReductionDotLineWidthMeters = 0.30f;
        [Min(0.05f)] public float speedReductionDotLineHeightMeters = 1.0f;
        [Min(0.1f)]  public float speedReductionDotLineSpacingMeters = 1.0f;
        public Color speedReductionDotLineColor = Color.white;
        public float speedReductionDotLineStartOffsetMeters = 0.5f;
        public float speedReductionDotLineSlantMeters = 0.3f;
        // 選択したレーン端からドットの最も近い縁までの距離(0 以上)。
        [Min(0f)] public float speedReductionDotLineInsetMeters = 0.20f;
        public SpeedReductionDotLineSide speedReductionDotLineSide = SpeedReductionDotLineSide.Right;
        [Min(0f)] public float speedReductionDotLinePaintHeightFactor = 1.0f;

        // ---- 減速マーク (山形マーク / Deceleration Chevron) ----
        // レーン中央に進行方向を指す V 字型シェブロンを V 軸方向へ周期的に配置する。
        // 急カーブや追突事故多発区間など、減速を要する区間およびその手前に設置される塗装路面標示。
        // 既定: height 1.0m + spacing 4.0m = 5.0m 周期。タイル長 10m を整数等分 (2 マーク/タイル)。
        // 線太さ 0.20m は政令の標準型に準拠。
        public bool decelerationMark = false;
        public Color decelerationMarkColor = Color.white;
        // V字の開口幅 (U軸方向)。
        [Min(0.3f)] public float decelerationMarkWidthMeters = 2.0f;
        // V字の深さ (V軸方向のマーク 1 つ分の高さ)。
        [Min(0.2f)] public float decelerationMarkHeightMeters = 1.0f;
        // V字マーク間のギャップ (V軸方向)。
        [Min(0.5f)] public float decelerationMarkSpacingMeters = 4.0f;
        // V字を構成する線の太さ。
        [Min(0.05f)] public float decelerationMarkThicknessMeters = 0.2f;
        // V 軸の位相オフセット。spacing/2 を既定とすることでパターンがタイル内中央配置になる。
        public float decelerationMarkStartOffsetMeters = 2.0f;
        // レーン端からの U 軸インセット (区画線とマークが重ならないようにする)。
        [Min(0f)] public float decelerationMarkInsetMeters = 0.3f;
        [Min(0f)] public float decelerationMarkPaintHeightFactor = 1.0f;
    }

    [Serializable]
    public class ShoulderConfig
    {
        [Min(0f)] public float widthMeters = 0.75f;
        // 加算的な明度オフセット(-0.5 〜 +0.5)。
        [Range(-0.5f, 0.5f)] public float colorTint = 0.04f;
    }

    [Serializable]
    public class AsphaltConfig
    {
        public Color baseColor = new Color(74f / 255f, 72f / 255f, 68f / 255f);
        public NoiseStyle noiseStyle = NoiseStyle.Standard;
        [Range(0f, 2f)] public float noiseIntensity = 1f;
        [Range(0f, 0.05f)] public float brightSpeckAmount = 0.015f;
        [Range(0f, 0.05f)] public float darkSpeckAmount = 0.008f;
    }

    [Serializable]
    public class WeatheringConfig
    {
        // 線の縁の摩耗。塗装高さの寄与も同時に減衰させる。
        [Range(0f, 1f)] public float lineWear = 0.15f;
        [Range(0f, 1f)] public float lineFade = 0.08f;
        // タイヤ跡によるレーン中央付近の劣化強度。レーン毎の追加調整は LaneConfig.tireTrackWearBoost で行う。
        [Range(0f, 1f)] public float tireTrackWear = 0.0f;
        // タイヤ跡が路面標示 (境界線・減速マーク等) に与える摩耗の強度。
        // 0 = 標示はタイヤ跡の影響を受けない、1 = タイヤ跡上で標示が完全に下地色にフェード。
        [Range(0f, 1f)] public float tireTrackMarkingWearStrength = 0.5f;
        [Min(0.05f)] public float defaultTireTrackWidthMeters = 1.08f;
        [Min(0f)] public float defaultTireTrackSpacingMeters = 1.70f;
        public Color defaultTireTrackColor = new Color(0.05f, 0.05f, 0.06f);
        [Range(0f, 1f)] public float defaultTireTrackOpacity = 0.30f;
        public bool repairPatches = false;
        [Range(0, 8)] public int repairPatchCount = 2;
        public bool wetSurface = false;
        // 法線マップへの塗装高さ寄与の全体倍率。0 = 塗装の凸なし。
        [Range(0f, 3f)] public float paintHeightStrength = 1.0f;
    }

    [Serializable]
    public class OutputConfig
    {
        public TextureResolution resolution = TextureResolution._1024;
        // V 軸方向(進行方向)に 1 タイルがカバーするメートル数。
        [Min(1f)] public float textureLengthMeters = 10f;
        public string outputFolder = "Assets/RoadTextures";
        public string namePrefix = "road";
        public bool generateNormal = true;
        public bool generateMetallicSmoothness = true;
        public bool generateAO = true;
        public bool generateMaterial = true;
        public PipelineTarget pipelineTarget = PipelineTarget.AutoDetect;
        public int seed = 42;
    }

    /// <summary>
    /// 道路定義のトップレベルクラス。道路全体の幅は (路側帯幅 + 各レーン幅 + 各境界線スロット幅) の総和となり、
    /// 境界線の太さや本数に関係なく、各レーンは生成テクスチャ上で正確にその走行可能幅を占める。
    /// </summary>
    [Serializable]
    public class RoadConfig
    {
        public ShoulderConfig leftShoulder = new ShoulderConfig();
        public ShoulderConfig rightShoulder = new ShoulderConfig();
        public List<LaneConfig> lanes = new List<LaneConfig>();
        // 境界線。lines.Count == lanes.Count + 1 を維持する必要がある。
        // lines[0] = 左外側線、lines[N] = 右外側線、lines[i] = lane[i-1] と lane[i] の間。
        public List<LineConfig> lines = new List<LineConfig>();
        public AsphaltConfig asphalt = new AsphaltConfig();
        public WeatheringConfig weathering = new WeatheringConfig();
        public OutputConfig output = new OutputConfig();

        public float TotalWidthMeters
        {
            get
            {
                var w = leftShoulder.widthMeters + rightShoulder.widthMeters;
                foreach (var lane in lanes)
                {
                    w += lane.widthMeters;
                }
                if (lines != null)
                {
                    foreach (var line in lines)
                    {
                        if (line == null)
                        {
                            continue;
                        }
                        line.ComputeSlotInfo(out _, out _, out var slot);
                        w += slot;
                    }
                }
                return w;
            }
        }

        /// <summary><c>lines.Count == lanes.Count + 1</c> を保つ。不足分は既定値で補完する。</summary>
        public void EnsureLineCount()
        {
            var desired = lanes.Count + 1;
            while (lines.Count < desired)
            {
                lines.Add(MakeDefaultBoundaryLine(lines.Count));
            }
            while (lines.Count > desired)
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        private LineConfig MakeDefaultBoundaryLine(int index)
        {
            var total = lanes.Count + 1;
            var isOuter = index == 0 || index == total - 1;
            return LineConfig.Single(
                isOuter ? "Outer Edge" : "Lane Divider",
                MakeWhiteLine(isOuter ? LineType.Solid : LineType.Dashed));
        }

        // ---------------------------------------------------------------------------------
        // 組み込みプリセット(日本仕様の代表的な構成)
        // ---------------------------------------------------------------------------------

        public static RoadConfig PresetMountainRoad_NoOvertaking()
        {
            // 山道・追越し禁止：片側1車線、中央 黄実線
            var c = NewTwoLaneRoad(shoulder: 0.75f);
            c.lines = new List<LineConfig>
            {
                LineConfig.Single("Left Edge",  MakeWhiteLine(LineType.Solid)),
                LineConfig.Single("Center (Yellow Solid)", MakeYellowLine(LineType.Solid)),
                LineConfig.Single("Right Edge", MakeWhiteLine(LineType.Solid)),
            };
            return c;
        }

        public static RoadConfig PresetMountainRoad_PassingOK()
        {
            // 山道・追越し可：片側1車線、中央 白破線
            var c = NewTwoLaneRoad(shoulder: 0.75f);
            c.lines = new List<LineConfig>
            {
                LineConfig.Single("Left Edge",  MakeWhiteLine(LineType.Solid)),
                LineConfig.Single("Center (White Dashed)", MakeWhiteLine(LineType.Dashed)),
                LineConfig.Single("Right Edge", MakeWhiteLine(LineType.Solid)),
            };
            return c;
        }

        public static RoadConfig PresetFourLane()
        {
            // 4車線・中央 二重黄実線、車線間 白破線。日本の左側通行を想定して左側 2 車線が forward。
            var c = new RoadConfig();
            c.leftShoulder.widthMeters = 1.0f;
            c.rightShoulder.widthMeters = 1.0f;
            for (var i = 0; i < 4; i++)
            {
                c.lanes.Add(new LaneConfig
                {
                    label = $"Lane {i + 1}",
                    widthMeters = 3.25f,
                    direction = i < 2 ? LaneDirection.Forward : LaneDirection.Backward,
                });
            }
            c.lines = new List<LineConfig>
            {
                LineConfig.Single("Left Edge",   MakeWhiteLine(LineType.Solid)),
                LineConfig.Single("Lane Divider", MakeWhiteLine(LineType.Dashed)),
                LineConfig.Double("Center (Double Yellow)",
                    MakeYellowLine(LineType.Solid),
                    MakeYellowLine(LineType.Solid),
                    spacing: 0.15f),
                LineConfig.Single("Lane Divider", MakeWhiteLine(LineType.Dashed)),
                LineConfig.Single("Right Edge",  MakeWhiteLine(LineType.Solid)),
            };
            return c;
        }

        public static RoadConfig PresetNarrowLane15()
        {
            // 1.5車線山道：中央線なし、外側線のみ
            var c = NewSingleLaneRoad(shoulder: 0.25f, laneWidth: 4.0f, laneLabel: "Single Lane");
            c.lines = new List<LineConfig>
            {
                LineConfig.Single("Left Edge",  MakeWhiteLine(LineType.Solid)),
                LineConfig.Single("Right Edge", MakeWhiteLine(LineType.Solid)),
            };
            return c;
        }

        public static RoadConfig PresetSingleLane()
        {
            // 1車線道路：細めの一方通行路。両側に白実線(路側帯線)。
            var c = NewSingleLaneRoad(shoulder: 0.5f, laneWidth: 3.0f, laneLabel: "Lane");
            c.lines = new List<LineConfig>
            {
                LineConfig.Single("Left Edge",  MakeWhiteLine(LineType.Solid)),
                LineConfig.Single("Right Edge", MakeWhiteLine(LineType.Solid)),
            };
            return c;
        }

        public static RoadConfig PresetNoLaneMarkings()
        {
            // 車線なし道路：区画線なし(住宅地の生活道路や農道など)。drivable area のみ。
            var c = NewSingleLaneRoad(shoulder: 0.25f, laneWidth: 4.0f, laneLabel: "Lane");
            c.lines = new List<LineConfig>
            {
                LineConfig.Single("Left Edge",  MakeWhiteLine(LineType.None)),
                LineConfig.Single("Right Edge", MakeWhiteLine(LineType.None)),
            };
            return c;
        }

        // ---------------------------------------------------------------------------------
        // プリセット構築用ヘルパー
        // ---------------------------------------------------------------------------------

        private static readonly Color YellowLineColor = new Color(232f / 255f, 168f / 255f, 32f / 255f);

        private static RoadConfig NewSingleLaneRoad(float shoulder, float laneWidth, string laneLabel)
        {
            var c = new RoadConfig();
            c.leftShoulder.widthMeters = shoulder;
            c.rightShoulder.widthMeters = shoulder;
            c.lanes.Add(new LaneConfig
            {
                label = laneLabel,
                widthMeters = laneWidth,
                direction = LaneDirection.Forward,
            });
            return c;
        }

        private static RoadConfig NewTwoLaneRoad(float shoulder)
        {
            var c = new RoadConfig();
            c.leftShoulder.widthMeters = shoulder;
            c.rightShoulder.widthMeters = shoulder;
            c.lanes.Add(new LaneConfig { label = "Lane (forward)",  widthMeters = 3.0f, direction = LaneDirection.Forward });
            c.lanes.Add(new LaneConfig { label = "Lane (oncoming)", widthMeters = 3.0f, direction = LaneDirection.Backward });
            return c;
        }

        private static LineStyle MakeWhiteLine(LineType type)
        {
            return new LineStyle
            {
                type = type,
                color = Color.white,
                widthMeters = 0.15f,
                dashLengthMeters = 5f,
                dashGapMeters = 5f,
                dashOffsetMeters = 2.5f,
            };
        }

        private static LineStyle MakeYellowLine(LineType type)
        {
            return new LineStyle
            {
                type = type,
                color = YellowLineColor,
                widthMeters = 0.15f,
            };
        }
    }
}
