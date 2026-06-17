using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>スプラインの距離サンプリング結果。</summary>
    public readonly struct SplineSample
    {
        public readonly float distanceMeters;
        public readonly float parameter;
        public readonly Vector3 position;
        public readonly Vector3 tangent;
        public readonly SplineFrame frame;
        public readonly float curvature;

        public SplineSample(
            float distanceMeters,
            float parameter,
            Vector3 position,
            Vector3 tangent,
            SplineFrame frame,
            float curvature)
        {
            this.distanceMeters = distanceMeters;
            this.parameter = parameter;
            this.position = position;
            this.tangent = tangent;
            this.frame = frame;
            this.curvature = curvature;
        }

        public float CurvatureRadiusMeters => curvature > CatmullRomSpline.Epsilon ? 1f / curvature : float.PositiveInfinity;
    }
}
