using System;
using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>道路区間へ埋め込まれる路面スタイル。</summary>
    [Serializable]
    public class RoadSurfaceStyle
    {
        public const float DefaultTextureLengthMeters = 10f;

        public string styleName = "Default";
        public string pavementName = "Asphalt";
        public Material material;
        [Min(0.1f)] public float textureLengthMeters = DefaultTextureLengthMeters;

        public RoadSurfaceStyle Clone()
        {
            return new RoadSurfaceStyle
            {
                styleName = styleName,
                pavementName = pavementName,
                material = material,
                textureLengthMeters = textureLengthMeters,
            };
        }

        public static RoadSurfaceStyle CreateDefault()
        {
            return new RoadSurfaceStyle();
        }

        public static RoadSurfaceStyle FromNetworkDefaults(RoadNetwork network)
        {
            var style = CreateDefault();
            if (network == null)
            {
                return style;
            }

            style.material = network.surfaceMaterial;
            style.textureLengthMeters = Mathf.Max(0.1f, network.textureLengthMeters);
            return style;
        }

        public static float ResolveTextureLengthMeters(RoadSegment segment, RoadNetwork network)
        {
            if (HasSegmentStyle(segment))
            {
                return Mathf.Max(0.1f, segment.surfaceStyle.textureLengthMeters);
            }

            if (network != null)
            {
                return Mathf.Max(0.1f, network.textureLengthMeters);
            }

            return DefaultTextureLengthMeters;
        }

        public static Material ResolveMaterial(RoadSegment segment, RoadNetwork network)
        {
            if (HasSegmentStyle(segment) && segment.surfaceStyle.material != null)
            {
                return segment.surfaceStyle.material;
            }

            return network != null ? network.surfaceMaterial : null;
        }

        public static bool HasSegmentStyle(RoadSegment segment)
        {
            return segment != null && segment.useSurfaceStyle && segment.surfaceStyle != null;
        }
    }
}
