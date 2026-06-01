using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>u^2 + (2(v-0.5))^2 &lt;= 1 の楕円。状態を持たないためシングルトンとして共有する。</summary>
    public sealed class EllipsePrimitive : IShapePrimitive
    {
        public static readonly EllipsePrimitive Instance = new EllipsePrimitive();

        public bool Contains(float u, float v, out float duNorm)
        {
            duNorm = u;
            var vCentered = 2f * (v - 0.5f);
            return u * u + vCentered * vCentered <= 1f;
        }

        public float MaxUExtent => 1f;
        public bool HasDiagonalEdges => true;
    }
}
