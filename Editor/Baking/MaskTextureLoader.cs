using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>Texture2D からグレースケールマスク値を読み出す共通ローダー。</summary>
    internal static class MaskTextureLoader
    {
        public static (float[] pixels, int width, int height) LoadGrayscale(Texture2D tex)
        {
            if (tex == null)
            {
                return (null, 0, 0);
            }

            Texture2D readable = null;
            try
            {
                readable = EnsureReadable(tex);
                var pixels = ExtractGrayscalePixels(readable);
                return (pixels, tex.width, tex.height);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RoadAssetGenerator] Failed to read wear mask '{tex.name}': {e.Message}");
                return (null, 0, 0);
            }
            finally
            {
                if (readable != null && readable != tex)
                {
                    Object.DestroyImmediate(readable);
                }
            }
        }

        private static float[] ExtractGrayscalePixels(Texture2D tex)
        {
            var colors = tex.GetPixels();
            var result = new float[colors.Length];
            for (var i = 0; i < colors.Length; i++)
            {
                result[i] = colors[i].grayscale;
            }
            return result;
        }

        private static Texture2D EnsureReadable(Texture2D tex)
        {
            if (tex.isReadable)
            {
                return tex;
            }

            var tmp = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(tex, tmp);
                RenderTexture.active = tmp;

                var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                readable.Apply();
                return readable;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(tmp);
            }
        }
    }
}
