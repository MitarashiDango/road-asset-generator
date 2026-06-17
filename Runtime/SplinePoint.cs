using System;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>道路スプラインの制御点。座標は <see cref="RoadSegment"/> のローカル座標。</summary>
    [Serializable]
    public class SplinePoint
    {
        public Vector3 position;

        public SplinePoint()
        {
        }

        public SplinePoint(Vector3 position)
        {
            this.position = position;
        }
    }
}
