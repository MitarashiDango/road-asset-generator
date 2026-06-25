using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// 道路ネットワーク生成機能のルート。MVP では配下の <see cref="RoadSegment"/> のみ生成対象となる。
    /// </summary>
    [DisallowMultipleComponent]
    public class RoadNetwork : MonoBehaviour
    {
        [Min(0.1f)] public float textureLengthMeters = 10f;
        [Min(1f)] public float meshSegmentLengthMeters = 100f;
        [Min(0f)] public float markingVertexOffsetMeters = 0.005f;
        [Min(0.25f)] public float maxSurfaceSampleLengthMeters = 1f;
        [Range(1f, 45f)] public float maxSurfaceSampleAngleDegrees = 4f;

        public Material surfaceMaterial;
        public Material markingMaterial;

        /// <summary>このネットワーク直下に属する道路区間を子階層から収集する。</summary>
        public void CollectSegments(List<RoadSegment> results)
        {
            if (results == null)
            {
                return;
            }

            GetComponentsInChildren(true, results);
        }
    }
}
