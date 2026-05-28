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
        public float wearOverride;
        public float fadeOverride;
        public float[] wearMaskPixels;
        public int wearMaskW;
        public int wearMaskH;
        public float wearMaskStrength;
        public WearMaskTiling wearMaskTiling;
        public float wearMaskTileLengthPx;
        public int seed;

        public bool HasWearMask => wearMaskPixels != null
            && wearMaskW > 0
            && wearMaskH > 0
            && wearMaskStrength > 0f;
    }
}
