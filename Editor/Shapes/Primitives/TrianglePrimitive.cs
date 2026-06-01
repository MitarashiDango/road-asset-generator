using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>|u| &lt;= 1 - v の二等辺三角形。状態を持たないためシングルトンとして共有する。</summary>
    public sealed class TrianglePrimitive : IShapePrimitive
    {
        public static readonly TrianglePrimitive Instance = new TrianglePrimitive();

        public bool Contains(float u, float v, out float duNorm)
        {
            duNorm = u;
            return Mathf.Abs(u) <= 1f - v;
        }

        public float MaxUExtent => 1f;
        public bool HasDiagonalEdges => true;
    }
}
