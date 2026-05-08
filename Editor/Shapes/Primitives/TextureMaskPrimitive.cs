using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    public enum TextureMaskSampling { Bilinear, Bicubic }

    /// <summary>
    /// グレースケールテクスチャで定義される任意形状プリミティブ。
    /// コンストラクタで受け取った float[] をバイリニアまたはバイキュービック補間でサンプリングし、
    /// しきい値以上なら内部と判定する。
    /// </summary>
    public sealed class TextureMaskPrimitive : IShapePrimitive
    {
        private readonly float[] _pixels;
        private readonly int _width;
        private readonly int _height;
        private readonly float _threshold;
        private readonly bool _useBicubic;
        private readonly float _maxUExtent;

        /// <param name="pixels">グレースケール値 [0,1] の配列。行優先、下から上 (Unity GetPixels 準拠)。</param>
        /// <param name="width">テクスチャ幅。</param>
        /// <param name="height">テクスチャ高さ。</param>
        /// <param name="threshold">内外判定のしきい値。</param>
        /// <param name="sampling">補間アルゴリズム。</param>
        public TextureMaskPrimitive(float[] pixels, int width, int height, float threshold = 0.5f, TextureMaskSampling sampling = TextureMaskSampling.Bilinear)
        {
            _pixels = pixels;
            _width = width;
            _height = height;
            _threshold = threshold;
            _useBicubic = sampling == TextureMaskSampling.Bicubic;
            _maxUExtent = ComputeMaxUExtent();
        }

        /// <summary>
        /// <see cref="Texture2D"/> から <see cref="TextureMaskPrimitive"/> を生成するファクトリメソッド。
        /// テクスチャが読み取り不可の場合は RenderTexture 経由で一時コピーを作成する。
        /// </summary>
        public static TextureMaskPrimitive FromTexture(Texture2D tex, float threshold = 0.5f, TextureMaskSampling sampling = TextureMaskSampling.Bilinear)
        {
            var readable = EnsureReadable(tex);
            var pixels = ExtractGrayscalePixels(readable);
            if (readable != tex)
            {
                Object.DestroyImmediate(readable);
            }
            return new TextureMaskPrimitive(pixels, tex.width, tex.height, threshold, sampling);
        }

        public bool Contains(float u, float v, out float duNorm)
        {
            duNorm = u;
            var tx = (u * 0.5f + 0.5f) * (_width - 1);
            var ty = v * (_height - 1);
            var value = _useBicubic ? SampleBicubic(tx, ty) : SampleBilinear(tx, ty);
            return value >= _threshold;
        }

        public float MaxUExtent => _maxUExtent;
        public bool HasDiagonalEdges => true;

        private float SampleBilinear(float tx, float ty)
        {
            var x0 = Mathf.FloorToInt(tx);
            var y0 = Mathf.FloorToInt(ty);
            var x1 = Mathf.Min(x0 + 1, _width - 1);
            var y1 = Mathf.Min(y0 + 1, _height - 1);
            x0 = Mathf.Max(x0, 0);
            y0 = Mathf.Max(y0, 0);

            var fx = tx - Mathf.Floor(tx);
            var fy = ty - Mathf.Floor(ty);

            var v00 = _pixels[y0 * _width + x0];
            var v10 = _pixels[y0 * _width + x1];
            var v01 = _pixels[y1 * _width + x0];
            var v11 = _pixels[y1 * _width + x1];

            var a = v00 + (v10 - v00) * fx;
            var b = v01 + (v11 - v01) * fx;
            return a + (b - a) * fy;
        }

        private float SampleBicubic(float tx, float ty)
        {
            var ix = Mathf.FloorToInt(tx);
            var iy = Mathf.FloorToInt(ty);
            var fx = tx - ix;
            var fy = ty - iy;

            var result = 0f;
            for (var j = -1; j <= 2; j++)
            {
                var py = Mathf.Clamp(iy + j, 0, _height - 1);
                var wy = CatmullRomWeight(fy - j);
                for (var i = -1; i <= 2; i++)
                {
                    var px = Mathf.Clamp(ix + i, 0, _width - 1);
                    var wx = CatmullRomWeight(fx - i);
                    result += _pixels[py * _width + px] * wx * wy;
                }
            }
            return Mathf.Clamp01(result);
        }

        private static float CatmullRomWeight(float t)
        {
            var abs = Mathf.Abs(t);
            if (abs <= 1f)
            {
                return 1.5f * abs * abs * abs - 2.5f * abs * abs + 1f;
            }
            if (abs <= 2f)
            {
                return -0.5f * abs * abs * abs + 2.5f * abs * abs - 4f * abs + 2f;
            }
            return 0f;
        }

        private float ComputeMaxUExtent()
        {
            var maxCol = 0;
            var center = (_width - 1) * 0.5f;
            for (var x = _width - 1; x >= 0; x--)
            {
                var found = false;
                for (var y = 0; y < _height; y++)
                {
                    if (_pixels[y * _width + x] >= _threshold)
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    var dist = Mathf.Abs(x - center);
                    maxCol = Mathf.Max(maxCol, Mathf.CeilToInt(dist));
                    break;
                }
            }
            for (var x = 0; x < _width; x++)
            {
                var found = false;
                for (var y = 0; y < _height; y++)
                {
                    if (_pixels[y * _width + x] >= _threshold)
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    var dist = Mathf.Abs(x - center);
                    if (Mathf.CeilToInt(dist) > maxCol)
                    {
                        maxCol = Mathf.CeilToInt(dist);
                    }
                    break;
                }
            }
            return center > 0f ? maxCol / center : 1f;
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
            Graphics.Blit(tex, tmp);
            var prev = RenderTexture.active;
            RenderTexture.active = tmp;
            var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(tmp);
            return readable;
        }
    }
}
