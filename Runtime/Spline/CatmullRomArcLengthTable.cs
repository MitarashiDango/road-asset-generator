using System;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>Catmull-Rom スプラインの距離サンプリング用テーブル。</summary>
    public sealed class CatmullRomArcLengthTable
    {
        private readonly CatmullRomSpline spline;
        private readonly float[] parameters;
        private readonly float[] distances;

        internal CatmullRomArcLengthTable(CatmullRomSpline spline, float[] parameters, float[] distances)
        {
            this.spline = spline;
            this.parameters = parameters;
            this.distances = distances;
        }

        public float TotalLengthMeters => distances.Length == 0 ? 0f : distances[distances.Length - 1];

        public int Count => distances.Length;

        /// <summary>始点からの距離を指定してスプラインを評価する。</summary>
        public SplineSample SampleByDistance(float distanceMeters)
        {
            if (!spline.IsValid)
            {
                return default;
            }

            if (distances.Length == 0)
            {
                var frame = spline.EvaluateFrame(0f);
                return new SplineSample(0f, 0f, frame.position, frame.tangent, frame, 0f);
            }

            var clampedDistance = Mathf.Clamp(distanceMeters, 0f, TotalLengthMeters);
            var parameter = ParameterAtDistance(clampedDistance);
            var position = spline.EvaluatePosition(parameter);
            var tangent = spline.EvaluateTangent(parameter);
            var frameAtDistance = spline.EvaluateFrame(parameter);
            var curvature = spline.EstimateCurvature(parameter);
            return new SplineSample(clampedDistance, parameter, position, tangent, frameAtDistance, curvature);
        }

        /// <summary>距離に対応するスプラインパラメータを線形補間で求める。</summary>
        public float ParameterAtDistance(float distanceMeters)
        {
            if (distances.Length == 0)
            {
                return 0f;
            }

            var clampedDistance = Mathf.Clamp(distanceMeters, 0f, TotalLengthMeters);
            var index = Array.BinarySearch(distances, clampedDistance);
            if (index >= 0)
            {
                return parameters[index];
            }

            var next = ~index;
            if (next <= 0)
            {
                return parameters[0];
            }
            if (next >= distances.Length)
            {
                return parameters[parameters.Length - 1];
            }

            var prev = next - 1;
            var span = distances[next] - distances[prev];
            if (span <= CatmullRomSpline.Epsilon)
            {
                return parameters[prev];
            }

            var t = (clampedDistance - distances[prev]) / span;
            return Mathf.Lerp(parameters[prev], parameters[next], t);
        }
    }
}
