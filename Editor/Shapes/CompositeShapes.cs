using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>複数の形状の和集合。いずれかの形状に含まれれば形状内とみなす。</summary>
    public sealed class UnionShape : IMarkingShape
    {
        private readonly IMarkingShape[] _shapes;

        public UnionShape(params IMarkingShape[] shapes)
        {
            _shapes = shapes;
        }

        public int GetSlantPad(int halfWidthPx)
        {
            var max = 0;
            foreach (var s in _shapes)
            {
                var pad = s.GetSlantPad(halfWidthPx);
                if (pad > max)
                {
                    max = pad;
                }
            }
            return max;
        }

        public bool CanSkipRow(int y)
        {
            foreach (var s in _shapes)
            {
                if (!s.CanSkipRow(y))
                {
                    return false;
                }
            }
            return true;
        }

        public bool TestPixel(int x, int y, int xCenter, int halfWidthPx, out float du, out bool isVEdge)
        {
            du = 0f;
            isVEdge = false;
            var minAbsDu = float.MaxValue;
            var hit = false;
            foreach (var s in _shapes)
            {
                if (s.TestPixel(x, y, xCenter, halfWidthPx, out var childDu, out var childIsVEdge))
                {
                    hit = true;
                    isVEdge |= childIsVEdge;
                    var abs = Mathf.Abs(childDu);
                    if (abs < minAbsDu)
                    {
                        minAbsDu = abs;
                        du = childDu;
                    }
                }
            }
            return hit;
        }

        public bool HasDiagonalEdges
        {
            get
            {
                foreach (var s in _shapes)
                {
                    if (s.HasDiagonalEdges)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    /// <summary>複数の形状の積集合。すべての形状に含まれる場合のみ形状内とみなす。</summary>
    public sealed class IntersectionShape : IMarkingShape
    {
        private readonly IMarkingShape[] _shapes;

        public IntersectionShape(params IMarkingShape[] shapes)
        {
            _shapes = shapes;
        }

        public int GetSlantPad(int halfWidthPx)
        {
            var max = 0;
            foreach (var s in _shapes)
            {
                var pad = s.GetSlantPad(halfWidthPx);
                if (pad > max)
                {
                    max = pad;
                }
            }
            return max;
        }

        public bool CanSkipRow(int y)
        {
            foreach (var s in _shapes)
            {
                if (s.CanSkipRow(y))
                {
                    return true;
                }
            }
            return false;
        }

        public bool TestPixel(int x, int y, int xCenter, int halfWidthPx, out float du, out bool isVEdge)
        {
            du = 0f;
            isVEdge = false;
            var maxAbsDu = 0f;
            foreach (var s in _shapes)
            {
                if (!s.TestPixel(x, y, xCenter, halfWidthPx, out var childDu, out var childIsVEdge))
                {
                    return false;
                }
                isVEdge |= childIsVEdge;
                var abs = Mathf.Abs(childDu);
                if (abs > maxAbsDu)
                {
                    maxAbsDu = abs;
                    du = childDu;
                }
            }
            return true;
        }

        public bool HasDiagonalEdges
        {
            get
            {
                foreach (var s in _shapes)
                {
                    if (s.HasDiagonalEdges)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    /// <summary>ベース形状からマスク形状を差し引く。ベースに含まれ、マスクに含まれない場合だけ形状内とみなす。</summary>
    public sealed class SubtractShape : IMarkingShape
    {
        private readonly IMarkingShape _base;
        private readonly IMarkingShape _mask;

        public SubtractShape(IMarkingShape baseShape, IMarkingShape mask)
        {
            _base = baseShape;
            _mask = mask;
        }

        public int GetSlantPad(int halfWidthPx) => _base.GetSlantPad(halfWidthPx);
        public bool CanSkipRow(int y) => _base.CanSkipRow(y);

        public bool TestPixel(int x, int y, int xCenter, int halfWidthPx, out float du, out bool isVEdge)
        {
            if (!_base.TestPixel(x, y, xCenter, halfWidthPx, out du, out isVEdge))
            {
                return false;
            }
            return !_mask.TestPixel(x, y, xCenter, halfWidthPx, out _, out _);
        }

        public bool HasDiagonalEdges => _base.HasDiagonalEdges;
    }
}
