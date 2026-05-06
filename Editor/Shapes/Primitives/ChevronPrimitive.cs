using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// 鋭い頂点を持つ V 字 (シェブロン) 形状。
    /// 外側 V (頂点 (0,0)、足元 (±1, 1-h)) と内側 V (頂点 (0, h)、足元 (±1, 1)) の間のバンド領域。
    /// 外側 V が鋭い頂点を、足元の (u=±1, v∈[1-h, 1]) が垂直エッジ (車線境界と平行) を提供する。
    /// </summary>
    public sealed class ChevronPrimitive : IShapePrimitive
    {
        private readonly float _h;
        private readonly bool _pointAtTop;

        /// <param name="vThicknessNorm">
        /// V 軸方向の足元エッジ長 (正規化, 0..1)。
        /// 線の見た目の太さを制御する。Line Thickness (m) / Mark Height (m) で正規化される。
        /// </param>
        /// <param name="pointAtTop">true: v=0 が頂点 (⌃)、false: v=1 が頂点 (⌄)。</param>
        public ChevronPrimitive(float vThicknessNorm, bool pointAtTop)
        {
            _h = Mathf.Clamp(vThicknessNorm, 0.05f, 0.95f);
            _pointAtTop = pointAtTop;
        }

        public bool Contains(float u, float v, out float duNorm)
        {
            duNorm = u;
            var absU = Mathf.Abs(u);
            if (absU > 1f)
            {
                return false;
            }

            var vAdj = _pointAtTop ? v : 1f - v;
            var oneMinusH = 1f - _h;
            // 外側 V の境界: vAdj = (1-h) * |u|
            // 内側 V の境界: vAdj = h + (1-h) * |u|
            // バンドはこの 2 線の間。v=0 で |u|=0 のみ (鋭い頂点)、|u|=1 で v ∈ [1-h, 1] (垂直足元)。
            var outerV = oneMinusH * absU;
            var innerV = _h + oneMinusH * absU;
            return vAdj >= outerV && vAdj <= innerV;
        }

        public float MaxUExtent => 1f;
        public bool HasDiagonalEdges => true;
    }
}
