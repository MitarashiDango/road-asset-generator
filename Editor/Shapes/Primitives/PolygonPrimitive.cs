using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// 多角形ポリゴンで定義される図形プリミティブ。
    /// Winding Number アルゴリズムで内外判定を行い、複数リング（外周 + 穴）をサポートする。
    /// </summary>
    public sealed class PolygonPrimitive : IShapePrimitive
    {
        private readonly ResolvedPolygon _polygon;
        private readonly float _maxUExtent;

        public PolygonPrimitive(ResolvedPolygon polygon)
        {
            _polygon = polygon;
            _maxUExtent = Mathf.Max(Mathf.Abs(polygon.minU), Mathf.Abs(polygon.maxU));
            if (_maxUExtent < 0.001f)
            {
                _maxUExtent = 1f;
            }
        }

        /// <summary>
        /// <see cref="PolygonData"/> から直接生成するファクトリメソッド。
        /// weights で頂点グループの適用量を指定する。null の場合は基本位置のみ使用。
        /// </summary>
        public static PolygonPrimitive FromData(PolygonData data, Dictionary<string, float> weights = null)
        {
            return new PolygonPrimitive(data.Resolve(weights));
        }

        public bool Contains(float u, float v, out float duNorm)
        {
            duNorm = u;

            if (u < _polygon.minU || u > _polygon.maxU ||
                v < _polygon.minV || v > _polygon.maxV)
            {
                return false;
            }

            return PolygonMath.WindingNumber(_polygon.rings, u, v) != 0;
        }

        public float MaxUExtent => _maxUExtent;
        public bool HasDiagonalEdges => true;
    }
}
