using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>ストローク単位の劣化値を解決する共通ヘルパー。</summary>
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

        public static float ResolveEffectiveWear(in LineStroke stroke, float globalWear, float duFromCenter, int y, int textureHeight)
        {
            var wear = ResolveWear(in stroke, globalWear);
            if (!stroke.HasWearMask)
            {
                return wear;
            }

            var mask = SampleWearMask(in stroke, duFromCenter, y, textureHeight);
            return Mathf.Clamp01(wear + mask * Mathf.Clamp01(stroke.wearMaskStrength));
        }

        private static float SampleWearMask(in LineStroke stroke, float duFromCenter, int y, int textureHeight)
        {
            var u = stroke.halfWidthPx > 0
                ? Mathf.Clamp01(duFromCenter / (stroke.halfWidthPx * 2f) + 0.5f)
                : 0.5f;

            float v;
            var repeatV = stroke.wearMaskTiling == WearMaskTiling.RepeatAlongV;
            if (repeatV)
            {
                var tileLength = Mathf.Max(0.0001f, stroke.wearMaskTileLengthPx);
                v = Mathf.Repeat(y / tileLength, 1f);
            }
            else
            {
                v = textureHeight > 1 ? Mathf.Clamp01(y / (textureHeight - 1f)) : 0f;
            }

            var tx = u * (stroke.wearMaskW - 1);
            var ty = repeatV ? v * stroke.wearMaskH : v * (stroke.wearMaskH - 1);
            var x0 = Mathf.FloorToInt(tx);
            var x1 = Mathf.Min(x0 + 1, stroke.wearMaskW - 1);
            x0 = Mathf.Max(x0, 0);
            var y0Raw = Mathf.FloorToInt(ty);
            var y0 = repeatV ? y0Raw % stroke.wearMaskH : Mathf.Max(y0Raw, 0);
            var y1 = repeatV ? (y0 + 1) % stroke.wearMaskH : Mathf.Min(y0 + 1, stroke.wearMaskH - 1);

            var fx = tx - Mathf.Floor(tx);
            var fy = ty - Mathf.Floor(ty);

            var v00 = stroke.wearMaskPixels[y0 * stroke.wearMaskW + x0];
            var v10 = stroke.wearMaskPixels[y0 * stroke.wearMaskW + x1];
            var v01 = stroke.wearMaskPixels[y1 * stroke.wearMaskW + x0];
            var v11 = stroke.wearMaskPixels[y1 * stroke.wearMaskW + x1];

            var a = v00 + (v10 - v00) * fx;
            var b = v01 + (v11 - v01) * fx;
            return Mathf.Clamp01(a + (b - a) * fy);
        }
    }
}
