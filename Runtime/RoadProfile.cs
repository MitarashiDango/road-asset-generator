using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    public enum RoadLaneDirection { Forward, Backward }
    public enum RoadLineKind { None, Solid, Dashed }
    public enum RoadProfileTransitionSide { Left, Right }

    /// <summary>道路区間へ埋め込まれる幅員・区画線定義。</summary>
    [Serializable]
    public class RoadProfile
    {
        [Min(0f)] public float leftShoulderWidthMeters = 0.75f;
        [Min(0f)] public float rightShoulderWidthMeters = 0.75f;
        public List<RoadLane> lanes = new List<RoadLane>();
        public List<RoadBoundaryLine> boundaryLines = new List<RoadBoundaryLine>();

        /// <summary>道路全幅。区画線の幅は幅員計算に含めない。</summary>
        public float TotalWidthMeters
        {
            get
            {
                var width = Mathf.Max(0f, leftShoulderWidthMeters) + Mathf.Max(0f, rightShoulderWidthMeters);
                if (lanes != null)
                {
                    foreach (var lane in lanes)
                    {
                        if (lane != null)
                        {
                            width += Mathf.Max(0f, lane.widthMeters);
                        }
                    }
                }
                return width;
            }
        }

        public float HalfWidthMeters => TotalWidthMeters * 0.5f;

        /// <summary><c>boundaryLines.Count == lanes.Count + 1</c> を満たすよう不足分を補完する。</summary>
        public void EnsureBoundaryLineCount()
        {
            if (lanes == null)
            {
                lanes = new List<RoadLane>();
            }
            if (boundaryLines == null)
            {
                boundaryLines = new List<RoadBoundaryLine>();
            }

            var desired = lanes.Count + 1;
            while (boundaryLines.Count < desired)
            {
                boundaryLines.Add(RoadBoundaryLine.CreateDefault(boundaryLines.Count, desired));
            }
            while (boundaryLines.Count > desired)
            {
                boundaryLines.RemoveAt(boundaryLines.Count - 1);
            }
        }

        public RoadProfile Clone()
        {
            var copy = new RoadProfile
            {
                leftShoulderWidthMeters = leftShoulderWidthMeters,
                rightShoulderWidthMeters = rightShoulderWidthMeters,
                lanes = new List<RoadLane>(),
                boundaryLines = new List<RoadBoundaryLine>(),
            };

            if (lanes != null)
            {
                foreach (var lane in lanes)
                {
                    copy.lanes.Add(lane?.Clone());
                }
            }
            if (boundaryLines != null)
            {
                foreach (var line in boundaryLines)
                {
                    copy.boundaryLines.Add(line?.Clone());
                }
            }

            return copy;
        }

        public static RoadProfile CreateDefaultTwoLane()
        {
            var profile = new RoadProfile();
            profile.lanes.Add(new RoadLane { label = "Lane (forward)", widthMeters = 3f, direction = RoadLaneDirection.Forward });
            profile.lanes.Add(new RoadLane { label = "Lane (oncoming)", widthMeters = 3f, direction = RoadLaneDirection.Backward });
            profile.boundaryLines.Add(RoadBoundaryLine.Single("Left Edge", RoadLineStroke.White(RoadLineKind.Solid)));
            profile.boundaryLines.Add(RoadBoundaryLine.Single("Center", RoadLineStroke.Yellow(RoadLineKind.Solid)));
            profile.boundaryLines.Add(RoadBoundaryLine.Single("Right Edge", RoadLineStroke.White(RoadLineKind.Solid)));
            return profile;
        }
    }

    /// <summary>1 つの車線定義。</summary>
    [Serializable]
    public class RoadLane
    {
        public string label = "Lane";
        [Min(0.5f)] public float widthMeters = 3f;
        public RoadLaneDirection direction = RoadLaneDirection.Forward;

        public RoadLane Clone()
        {
            return new RoadLane
            {
                label = label,
                widthMeters = widthMeters,
                direction = direction,
            };
        }
    }

    /// <summary>車線境界または路肩内側に描画する区画線定義。</summary>
    [Serializable]
    public class RoadBoundaryLine
    {
        public string label = "Boundary";
        public List<RoadLineStroke> strokes = new List<RoadLineStroke>();
        [Min(0f)] public float strokeSpacingMeters = 0.15f;

        public RoadBoundaryLine Clone()
        {
            var copy = new RoadBoundaryLine
            {
                label = label,
                strokeSpacingMeters = strokeSpacingMeters,
                strokes = new List<RoadLineStroke>(),
            };

            if (strokes != null)
            {
                foreach (var stroke in strokes)
                {
                    copy.strokes.Add(stroke?.Clone());
                }
            }

            return copy;
        }

        public static RoadBoundaryLine Single(string label, RoadLineStroke stroke)
        {
            return new RoadBoundaryLine
            {
                label = label,
                strokes = new List<RoadLineStroke> { stroke },
            };
        }

        public static RoadBoundaryLine Double(string label, RoadLineStroke left, RoadLineStroke right, float spacingMeters)
        {
            return new RoadBoundaryLine
            {
                label = label,
                strokes = new List<RoadLineStroke> { left, right },
                strokeSpacingMeters = spacingMeters,
            };
        }

        public static RoadBoundaryLine CreateDefault(int index, int totalCount)
        {
            var isOuter = index == 0 || index == totalCount - 1;
            return Single(isOuter ? "Outer Edge" : "Lane Divider", RoadLineStroke.White(isOuter ? RoadLineKind.Solid : RoadLineKind.Dashed));
        }
    }

    /// <summary>1 本の区画線ストローク。</summary>
    [Serializable]
    public class RoadLineStroke
    {
        public RoadLineKind kind = RoadLineKind.Solid;
        [Min(0f)] public float widthMeters = 0.15f;
        public Color color = Color.white;
        [Min(0.05f)] public float dashLengthMeters = 5f;
        [Min(0.05f)] public float dashGapMeters = 5f;

        public RoadLineStroke Clone()
        {
            return new RoadLineStroke
            {
                kind = kind,
                widthMeters = widthMeters,
                color = color,
                dashLengthMeters = dashLengthMeters,
                dashGapMeters = dashGapMeters,
            };
        }

        public static RoadLineStroke White(RoadLineKind kind)
        {
            return new RoadLineStroke
            {
                kind = kind,
                color = Color.white,
                widthMeters = 0.15f,
            };
        }

        public static RoadLineStroke Yellow(RoadLineKind kind)
        {
            return new RoadLineStroke
            {
                kind = kind,
                color = new Color(232f / 255f, 168f / 255f, 32f / 255f),
                widthMeters = 0.15f,
            };
        }
    }

    /// <summary>区間始点からの距離に紐づく道路プロファイルキー。</summary>
    [Serializable]
    public class RoadProfileKey
    {
        [Min(0f)] public float positionMeters = 0f;
        public RoadProfile profile = RoadProfile.CreateDefaultTwoLane();
        [Min(0f)] public float transitionLengthMeters = 0f;
        public RoadProfileTransitionSide laneTransitionSide = RoadProfileTransitionSide.Right;

        public RoadProfileKey Clone()
        {
            return new RoadProfileKey
            {
                positionMeters = positionMeters,
                profile = profile?.Clone(),
                transitionLengthMeters = transitionLengthMeters,
                laneTransitionSide = laneTransitionSide,
            };
        }
    }
}
