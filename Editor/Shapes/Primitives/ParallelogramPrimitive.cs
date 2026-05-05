using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// |u - shear*(v-0.5)| &lt;= 1 の平行四辺形。
    /// v 位置に応じて u 中心がシフトし、菱形ストロークを表現する。
    /// </summary>
    public sealed class ParallelogramPrimitive : IShapePrimitive
    {
        private readonly float _shearNorm;

        /// <param name="shearNorm">正規化シアー量。slantPx / halfWidthPx に相当。</param>
        public ParallelogramPrimitive(float shearNorm)
        {
            _shearNorm = shearNorm;
        }

        public bool Contains(float u, float v, out float duNorm)
        {
            duNorm = u - _shearNorm * (v - 0.5f);
            return Mathf.Abs(duNorm) <= 1f;
        }

        public float MaxUExtent => 1f + Mathf.Abs(_shearNorm) * 0.5f;
        public bool HasDiagonalEdges => true;
    }
}
