using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>テクスチャ生成および色変換のユーティリティ。</summary>
    internal static class TextureUtils
    {
        /// <summary>線形空間で読まれる Texture2D を生成する (ミップマップ有り、Wrap Mode は Repeat)。</summary>
        public static Texture2D MakeLinear(Color32[] pixels, int W, int H)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true, false);
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        /// <summary>0..1 の Color を Color32 に変換する (各成分を 0..255 にクランプ)。</summary>
        public static Color32 ToColor32(Color c)
        {
            return new Color32(
                (byte)Mathf.Clamp(c.r * 255f, 0, 255),
                (byte)Mathf.Clamp(c.g * 255f, 0, 255),
                (byte)Mathf.Clamp(c.b * 255f, 0, 255),
                (byte)Mathf.Clamp(c.a * 255f, 0, 255));
        }
    }
}
