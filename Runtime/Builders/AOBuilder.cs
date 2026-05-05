using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>AO (アンビエントオクルージョン) マップピクセルを構築する。</summary>
    internal static class AOBuilder
    {
        public static Color32[] Build(int W, int H, int seed)
        {
            var pixels = new Color32[W * H];
            var noise = RoadNoise.GaussianBlurWrap(RoadNoise.WhiteNoise(W, H, seed + 700), W, H, 4f);
            for (var i = 0; i < pixels.Length; i++)
            {
                var v = 248 + (int)(noise[i] * 5f);
                v = Mathf.Clamp(v, 0, 255);
                pixels[i] = new Color32((byte)v, (byte)v, (byte)v, 255);
            }
            return pixels;
        }
    }
}
