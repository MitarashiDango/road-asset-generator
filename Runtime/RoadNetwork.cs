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
        [Tooltip("Distance in meters to lift generated road markings from the road surface along the road normal.")]
        [Min(0f)] public float markingVertexOffsetMeters = 0.005f;
        [Min(0.25f)] public float maxSurfaceSampleLengthMeters = 1f;
        [Range(1f, 45f)] public float maxSurfaceSampleAngleDegrees = 4f;
        public bool generateSurfaceColliders = true;
        [Range(0, 31)] public int defaultGeneratedSurfaceLayer;
        [Range(0, 31)] public int defaultGeneratedMarkingLayer;

        public Material surfaceMaterial;
        public Material markingMaterial;

        public RoadSurfaceStyleAsset defaultSurfaceStyleTemplate;
        public RoadProfileTemplateAsset defaultProfileTemplate;

        /// <summary>このネットワーク直下に属する道路区間を子階層から収集する。</summary>
        public void CollectSegments(List<RoadSegment> results)
        {
            if (results == null)
            {
                return;
            }

            GetComponentsInChildren(true, results);
        }

        public RoadSurfaceStyle CreateDefaultSurfaceStyleCopy()
        {
            return defaultSurfaceStyleTemplate != null
                ? defaultSurfaceStyleTemplate.CreateStyleCopy()
                : RoadSurfaceStyle.FromNetworkDefaults(this);
        }

        public RoadProfile CreateDefaultProfileCopy()
        {
            return defaultProfileTemplate != null && defaultProfileTemplate.profile != null
                ? defaultProfileTemplate.profile.Clone()
                : RoadProfile.CreateDefaultTwoLane();
        }

        public void ApplyNewSegmentDefaults(RoadSegment segment)
        {
            if (segment == null)
            {
                return;
            }

            segment.useSurfaceStyle = true;
            segment.surfaceStyle = CreateDefaultSurfaceStyleCopy();
            segment.profileKeys = new[]
            {
                new RoadProfileKey { profile = CreateDefaultProfileCopy() },
            };
        }
    }

    /// <summary>生成済み路面 / 標示 GameObject の Unity Layer 解決。</summary>
    public static class RoadGeneratedLayerSettings
    {
        public const int DefaultLayer = 0;

        public static int ResolveSurfaceLayer(RoadSegment segment, RoadNetwork network)
        {
            if (segment != null && segment.overrideGeneratedSurfaceLayer)
            {
                return NormalizeLayer(segment.generatedSurfaceLayer);
            }

            return NormalizeLayer(network != null ? network.defaultGeneratedSurfaceLayer : DefaultLayer);
        }

        public static int ResolveMarkingLayer(RoadSegment segment, RoadNetwork network)
        {
            if (segment != null && segment.overrideGeneratedMarkingLayer)
            {
                return NormalizeLayer(segment.generatedMarkingLayer);
            }

            return NormalizeLayer(network != null ? network.defaultGeneratedMarkingLayer : DefaultLayer);
        }

        public static int NormalizeLayer(int layer)
        {
            return Mathf.Clamp(layer, 0, 31);
        }
    }

    /// <summary>生成済み路面 GameObject の MeshCollider 生成設定解決。</summary>
    public static class RoadSurfaceColliderSettings
    {
        public const bool DefaultGenerateSurfaceColliders = true;

        public static bool ResolveGenerateSurfaceColliders(RoadSegment segment, RoadNetwork network)
        {
            if (segment != null && segment.overrideSurfaceColliderSettings)
            {
                return segment.generateSurfaceColliders;
            }

            return network != null
                ? network.generateSurfaceColliders
                : DefaultGenerateSurfaceColliders;
        }
    }
}
