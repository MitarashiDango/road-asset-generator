using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>スプライン上の位置と、その地点の進行方向・横方向・上方向。</summary>
    public readonly struct SplineFrame
    {
        public readonly Vector3 position;
        public readonly Vector3 tangent;
        public readonly Vector3 right;
        public readonly Vector3 up;

        public SplineFrame(Vector3 position, Vector3 tangent, Vector3 right, Vector3 up)
        {
            this.position = position;
            this.tangent = tangent;
            this.right = right;
            this.up = up;
        }
    }
}
