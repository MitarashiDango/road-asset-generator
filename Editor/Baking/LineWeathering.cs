using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>Stroke 単位の劣化値を解決する共通ヘルパ。</summary>
    internal static class LineWeathering
    {
        public const float UseGlobal = -1f;

        public static float ResolveWear(in LineStroke stroke, float globalWear)
        {
            return Mathf.Clamp01(stroke.wearOverride >= 0f ? stroke.wearOverride : globalWear);
        }

        public static float ResolveFade(in LineStroke stroke, float globalFade)
        {
            return Mathf.Clamp01(stroke.fadeOverride >= 0f ? stroke.fadeOverride : globalFade);
        }

        public static float ResolveEffectiveWear(in LineStroke stroke, float globalWear)
        {
            return ResolveWear(in stroke, globalWear);
        }
    }
}
