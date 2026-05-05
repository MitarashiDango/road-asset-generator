using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>1 本の線ストロークをピクセル空間に解決した中間表現。</summary>
    internal struct LineStroke
    {
        public int xCenter;
        public int halfWidthPx;
        public IMarkingShape shape;
        public Color color;
        public float paintHeightFactor;
        public int seed;
    }
}
