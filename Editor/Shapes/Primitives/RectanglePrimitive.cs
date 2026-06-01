using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>|u| &lt;= 1 の矩形。状態を持たないためシングルトンとして共有する。</summary>
    public sealed class RectanglePrimitive : IShapePrimitive
    {
        public static readonly RectanglePrimitive Instance = new RectanglePrimitive();

        public bool Contains(float u, float v, out float duNorm)
        {
            duNorm = u;
            return Mathf.Abs(u) <= 1f;
        }

        public float MaxUExtent => 1f;
        public bool HasDiagonalEdges => false;
    }
}
