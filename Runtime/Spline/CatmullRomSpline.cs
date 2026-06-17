using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// Centripetal Catmull-Rom スプライン。端点は隣接制御点を反射した仮想点で評価する。
    /// フレームの上方向は World Y を基準にし、接線が鉛直に近い場合は World Z、最後に World X を参照方向に使う。
    /// </summary>
    public sealed class CatmullRomSpline
    {
        public const float DefaultAlpha = 0.5f;
        public const float Epsilon = 0.00001f;

        private readonly Vector3[] points;

        public CatmullRomSpline(IReadOnlyList<SplinePoint> controlPoints, bool closed = false, float alpha = DefaultAlpha)
        {
            if (controlPoints == null)
            {
                points = Array.Empty<Vector3>();
                Closed = closed;
                Alpha = Mathf.Max(Epsilon, alpha);
                return;
            }

            points = new Vector3[controlPoints.Count];
            for (var i = 0; i < controlPoints.Count; i++)
            {
                points[i] = controlPoints[i]?.position ?? Vector3.zero;
            }

            Closed = closed;
            Alpha = Mathf.Max(Epsilon, alpha);
        }

        public CatmullRomSpline(IReadOnlyList<Vector3> controlPoints, bool closed = false, float alpha = DefaultAlpha)
        {
            if (controlPoints == null)
            {
                points = Array.Empty<Vector3>();
                Closed = closed;
                Alpha = Mathf.Max(Epsilon, alpha);
                return;
            }

            points = new Vector3[controlPoints.Count];
            for (var i = 0; i < controlPoints.Count; i++)
            {
                points[i] = controlPoints[i];
            }

            Closed = closed;
            Alpha = Mathf.Max(Epsilon, alpha);
        }

        public int ControlPointCount => points.Length;

        public bool Closed { get; }

        public float Alpha { get; }

        public int SegmentCount
        {
            get
            {
                if (points.Length < 2)
                {
                    return 0;
                }
                return Closed ? points.Length : points.Length - 1;
            }
        }

        public bool IsValid => SegmentCount > 0;

        public float MaxParameter => Mathf.Max(0, SegmentCount);

        /// <summary>
        /// セグメント番号 + セグメント内 0..1 のパラメータで位置を評価する。
        /// 有効範囲外の値はスプライン端へクランプされる。
        /// </summary>
        public Vector3 EvaluatePosition(float parameter)
        {
            if (!IsValid)
            {
                return points.Length == 0 ? Vector3.zero : points[0];
            }

            ToSegmentParameter(parameter, out var segmentIndex, out var u);
            GetSegmentPoints(segmentIndex, out var p0, out var p1, out var p2, out var p3);
            return EvaluateSegment(p0, p1, p2, p3, u, Alpha);
        }

        /// <summary>指定パラメータでの接線を正規化して返す。</summary>
        public Vector3 EvaluateTangent(float parameter)
        {
            if (!IsValid)
            {
                return Vector3.forward;
            }

            var step = Mathf.Max(0.0005f, MaxParameter * 0.0005f);
            var before = Mathf.Clamp(parameter - step, 0f, MaxParameter);
            var after = Mathf.Clamp(parameter + step, 0f, MaxParameter);
            if (Mathf.Abs(after - before) <= Epsilon)
            {
                after = Mathf.Clamp(parameter + step, 0f, MaxParameter);
                before = Mathf.Clamp(parameter - step, 0f, MaxParameter);
            }

            var delta = EvaluatePosition(after) - EvaluatePosition(before);
            if (delta.sqrMagnitude <= Epsilon * Epsilon)
            {
                ToSegmentParameter(parameter, out var segmentIndex, out _);
                var start = GetPoint(segmentIndex);
                var end = GetPoint(Closed ? (segmentIndex + 1) % points.Length : Mathf.Min(segmentIndex + 1, points.Length - 1));
                delta = end - start;
            }

            return delta.sqrMagnitude <= Epsilon * Epsilon ? Vector3.forward : delta.normalized;
        }

        /// <summary>指定パラメータでの道路用フレームを評価する。</summary>
        public SplineFrame EvaluateFrame(float parameter)
        {
            return EvaluateFrame(parameter, Vector3.up, Vector3.forward, Vector3.right);
        }

        /// <summary>指定した参照軸を基準に道路用フレームを評価する。</summary>
        public SplineFrame EvaluateFrame(
            float parameter,
            Vector3 referenceUp,
            Vector3 fallbackForward,
            Vector3 fallbackRight)
        {
            var position = EvaluatePosition(parameter);
            var tangent = EvaluateTangent(parameter);
            var upReference = NormalizeOrFallback(referenceUp, Vector3.up);
            var right = Vector3.Cross(upReference, tangent);
            if (right.sqrMagnitude <= 0.0001f)
            {
                upReference = NormalizeOrFallback(fallbackForward, Vector3.forward);
                right = Vector3.Cross(upReference, tangent);
            }
            if (right.sqrMagnitude <= 0.0001f)
            {
                upReference = NormalizeOrFallback(fallbackRight, Vector3.right);
                right = Vector3.Cross(upReference, tangent);
            }
            if (right.sqrMagnitude <= 0.0001f)
            {
                upReference = GetLeastAlignedAxis(tangent);
                right = Vector3.Cross(upReference, tangent);
            }

            right = right.normalized;
            var up = Vector3.Cross(tangent, right).normalized;
            return new SplineFrame(position, tangent, right, up);
        }

        /// <summary>曲線長をサンプリングで近似する。</summary>
        public float EstimateLength(int samplesPerSegment = 24)
        {
            return BuildArcLengthTable(samplesPerSegment).TotalLengthMeters;
        }

        /// <summary>距離サンプリング用の弧長テーブルを作成する。</summary>
        public CatmullRomArcLengthTable BuildArcLengthTable(int samplesPerSegment = 24)
        {
            samplesPerSegment = Mathf.Max(1, samplesPerSegment);
            if (!IsValid)
            {
                return new CatmullRomArcLengthTable(this, Array.Empty<float>(), Array.Empty<float>());
            }

            var sampleCount = SegmentCount * samplesPerSegment + 1;
            var parameters = new float[sampleCount];
            var distances = new float[sampleCount];
            var index = 0;
            var previous = EvaluatePosition(0f);
            parameters[index] = 0f;
            distances[index] = 0f;
            index++;

            var total = 0f;
            for (var segment = 0; segment < SegmentCount; segment++)
            {
                for (var step = 1; step <= samplesPerSegment; step++)
                {
                    var parameter = segment + step / (float)samplesPerSegment;
                    var position = EvaluatePosition(parameter);
                    total += Vector3.Distance(previous, position);
                    parameters[index] = parameter;
                    distances[index] = total;
                    previous = position;
                    index++;
                }
            }

            return new CatmullRomArcLengthTable(this, parameters, distances);
        }

        /// <summary>始点からの距離を指定してスプラインを評価する。</summary>
        public SplineSample SampleByDistance(float distanceMeters, int samplesPerSegment = 24)
        {
            return BuildArcLengthTable(samplesPerSegment).SampleByDistance(distanceMeters);
        }

        /// <summary>指定パラメータで曲率を近似する。戻り値の単位は 1/m。</summary>
        public float EstimateCurvature(float parameter, float parameterStep = 0.01f)
        {
            if (!IsValid)
            {
                return 0f;
            }

            var step = Mathf.Clamp(parameterStep, 0.0005f, 0.25f);
            var before = Mathf.Clamp(parameter - step, 0f, MaxParameter);
            var after = Mathf.Clamp(parameter + step, 0f, MaxParameter);
            if (after - before <= Epsilon)
            {
                return 0f;
            }

            var tangentBefore = EvaluateTangent(before);
            var tangentAfter = EvaluateTangent(after);
            var distance = Vector3.Distance(EvaluatePosition(before), EvaluatePosition(after));
            if (distance <= Epsilon)
            {
                return 0f;
            }

            return (tangentAfter - tangentBefore).magnitude / distance;
        }

        /// <summary>指定パラメータで曲率半径を近似する。</summary>
        public float EstimateCurvatureRadius(float parameter, float parameterStep = 0.01f)
        {
            var curvature = EstimateCurvature(parameter, parameterStep);
            return curvature > Epsilon ? 1f / curvature : float.PositiveInfinity;
        }

        private static Vector3 EvaluateSegment(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u, float alpha)
        {
            if ((p2 - p1).sqrMagnitude <= Epsilon * Epsilon)
            {
                return p1;
            }

            var t0 = 0f;
            var t1 = GetKnot(t0, p0, p1, alpha);
            var t2 = GetKnot(t1, p1, p2, alpha);
            var t3 = GetKnot(t2, p2, p3, alpha);
            if (t2 - t1 <= Epsilon)
            {
                return Vector3.Lerp(p1, p2, u);
            }

            var t = Mathf.Lerp(t1, t2, Mathf.Clamp01(u));
            var a1 = InterpolateByKnot(p0, p1, t0, t1, t);
            var a2 = InterpolateByKnot(p1, p2, t1, t2, t);
            var a3 = InterpolateByKnot(p2, p3, t2, t3, t);
            var b1 = InterpolateByKnot(a1, a2, t0, t2, t);
            var b2 = InterpolateByKnot(a2, a3, t1, t3, t);
            return InterpolateByKnot(b1, b2, t1, t2, t);
        }

        private static float GetKnot(float previous, Vector3 a, Vector3 b, float alpha)
        {
            var distance = Mathf.Max(Vector3.Distance(a, b), Epsilon);
            return previous + Mathf.Pow(distance, alpha);
        }

        private static Vector3 InterpolateByKnot(Vector3 a, Vector3 b, float ta, float tb, float t)
        {
            var denominator = tb - ta;
            if (Mathf.Abs(denominator) <= Epsilon)
            {
                return a;
            }

            var weightA = (tb - t) / denominator;
            var weightB = (t - ta) / denominator;
            return a * weightA + b * weightB;
        }

        private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > Epsilon * Epsilon ? value.normalized : fallback;
        }

        private static Vector3 GetLeastAlignedAxis(Vector3 tangent)
        {
            var xAlignment = Mathf.Abs(Vector3.Dot(tangent, Vector3.right));
            var yAlignment = Mathf.Abs(Vector3.Dot(tangent, Vector3.up));
            var zAlignment = Mathf.Abs(Vector3.Dot(tangent, Vector3.forward));

            if (xAlignment <= yAlignment && xAlignment <= zAlignment)
            {
                return Vector3.right;
            }

            return yAlignment <= zAlignment ? Vector3.up : Vector3.forward;
        }

        private void ToSegmentParameter(float parameter, out int segmentIndex, out float u)
        {
            var clamped = Mathf.Clamp(parameter, 0f, MaxParameter);
            if (Mathf.Approximately(clamped, MaxParameter))
            {
                segmentIndex = SegmentCount - 1;
                u = 1f;
                return;
            }

            segmentIndex = Mathf.Clamp(Mathf.FloorToInt(clamped), 0, SegmentCount - 1);
            u = Mathf.Clamp01(clamped - segmentIndex);
        }

        private void GetSegmentPoints(int segmentIndex, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
        {
            p1 = GetPoint(segmentIndex);
            p2 = GetPoint(segmentIndex + 1);
            p0 = GetPoint(segmentIndex - 1);
            p3 = GetPoint(segmentIndex + 2);
        }

        private Vector3 GetPoint(int index)
        {
            if (Closed)
            {
                var count = points.Length;
                var wrapped = ((index % count) + count) % count;
                return points[wrapped];
            }

            if (index < 0)
            {
                return points[0] * 2f - points[1];
            }
            if (index >= points.Length)
            {
                return points[points.Length - 1] * 2f - points[points.Length - 2];
            }

            return points[index];
        }
    }
}
