using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// <see cref="IShapePrimitive"/> を V 軸方向に繰り返し配置する路面標示パターン。
    /// 破線・菱形ストローク・減速ドットラインなど、周期的な標示を統一的に扱う。
    /// </summary>
    public sealed class MarkingPattern : IMarkingShape
    {
        private readonly IShapePrimitive _primitive;
        private readonly float _sizePx;
        private readonly float _periodPx;
        private readonly float _offsetPx;
        private readonly float _yShearPx;

        /// <param name="primitive">配置する図形プリミティブ。</param>
        /// <param name="sizePx">1 マークの V 方向サイズ (px)。</param>
        /// <param name="gapPx">マーク間の V 方向の間隔 (px)。</param>
        /// <param name="offsetPx">V 方向の開始オフセット (px)。</param>
        /// <param name="yShearPx">Y 方向のシアー量 (px)。U 位置に応じて V 周期の位相をずらす。</param>
        public MarkingPattern(IShapePrimitive primitive, float sizePx, float gapPx, float offsetPx, float yShearPx = 0f)
        {
            _primitive = primitive;
            _sizePx = sizePx;
            _periodPx = sizePx + gapPx;
            _offsetPx = offsetPx;
            _yShearPx = yShearPx;
        }

        public int GetSlantPad(int halfWidthPx)
        {
            return Mathf.CeilToInt((_primitive.MaxUExtent - 1f) * halfWidthPx);
        }

        public bool CanSkipRow(int y)
        {
            if (_yShearPx != 0f)
            {
                return false;
            }
            if (_periodPx <= 0.5f)
            {
                return true;
            }
            var p = (y - _offsetPx) % _periodPx;
            if (p < 0)
            {
                p += _periodPx;
            }
            return p >= _sizePx;
        }

        public bool TestPixel(int x, int y, int xCenter, int halfWidthPx, out float du, out bool isVEdge)
        {
            du = 0f;
            isVEdge = false;
            if (_periodPx <= 0.5f)
            {
                return false;
            }

            var uNorm = (x - xCenter) / (float)halfWidthPx;

            var yEff = y - uNorm * 0.5f * _yShearPx;
            var p = (yEff - _offsetPx) % _periodPx;
            if (p < 0)
            {
                p += _periodPx;
            }
            if (p >= _sizePx)
            {
                return false;
            }

            var v = p / _sizePx;

            if (!_primitive.Contains(uNorm, v, out var duNorm))
            {
                return false;
            }

            du = duNorm * halfWidthPx;
            var edgeWidth = Mathf.Min(1f, _sizePx * 0.5f);
            isVEdge = p <= edgeWidth || p >= _sizePx - edgeWidth;
            return true;
        }

        public bool HasDiagonalEdges => _yShearPx != 0f || _primitive.HasDiagonalEdges;
    }
}
