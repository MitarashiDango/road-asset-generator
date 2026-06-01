using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// <see cref="RoadTextureBaker"/> が利用する、タイルとして繰り返せるプロシージャルノイズのユーティリティ群。
    /// すべての処理で境界を折り返すため、生成されたマップはシームレスにタイリング可能となる。
    /// </summary>
    public static class RoadNoise
    {
        /// <summary>各サンプルが [-1, 1] の一様分布となるホワイトノイズ。</summary>
        public static float[] WhiteNoise(int width, int height, int seed)
        {
            var rng = new System.Random(seed);
            var arr = new float[width * height];
            for (var i = 0; i < arr.Length; i++)
            {
                arr[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            }
            return arr;
        }

        /// <summary>
        /// 境界を折り返す分離型ガウスぼかし。入力をタイルとして繰り返せる画像として扱う。
        /// <paramref name="sigma"/> が 0.4 未満の場合は入力のコピーをそのまま返す。
        /// </summary>
        public static float[] GaussianBlurWrap(float[] input, int width, int height, float sigma)
        {
            if (sigma < 0.4f)
            {
                return (float[])input.Clone();
            }

            var radius = Mathf.CeilToInt(sigma * 3f);
            var kernel = BuildGaussianKernel(radius, sigma);

            var horizontal = new float[width * height];
            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    var v = 0f;
                    for (var k = -radius; k <= radius; k++)
                    {
                        v += input[row + WrapIndex(x + k, width)] * kernel[k + radius];
                    }
                    horizontal[row + x] = v;
                }
            }

            var output = new float[width * height];
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var v = 0f;
                    for (var k = -radius; k <= radius; k++)
                    {
                        v += horizontal[WrapIndex(y + k, height) * width + x] * kernel[k + radius];
                    }
                    output[y * width + x] = v;
                }
            }
            return output;
        }

        /// <summary>
        /// 大きいテクスチャ向けの折り返しガウスぼかし近似。小さなホワイトノイズをぼかしてから
        /// バイリニアにアップサンプリングするため、巨大半径の直接ぼかしより高速。
        /// </summary>
        public static float[] LargeScaleNoise(int width, int height, float effectiveSigma, int seed)
        {
            const float targetSigma = 2.5f;
            var factor = Mathf.Max(1, Mathf.RoundToInt(effectiveSigma / targetSigma));
            var smallW = Mathf.Max(8, width / factor);
            var smallH = Mathf.Max(8, height / factor);
            var small = WhiteNoise(smallW, smallH, seed);
            var smoothed = GaussianBlurWrap(small, smallW, smallH, targetSigma);
            return BilinearUpsample(smoothed, smallW, smallH, width, height);
        }

        /// <summary>境界を折り返す、タイルとして繰り返せるバイリニアアップサンプル。</summary>
        public static float[] BilinearUpsample(float[] src, int srcW, int srcH, int dstW, int dstH)
        {
            var dst = new float[dstW * dstH];
            for (var y = 0; y < dstH; y++)
            {
                var fy = (float)y / dstH * srcH;
                var y0 = Mathf.FloorToInt(fy);
                var y1 = (y0 + 1) % srcH;
                var ty = fy - y0;
                y0 = ((y0 % srcH) + srcH) % srcH;
                for (var x = 0; x < dstW; x++)
                {
                    var fx = (float)x / dstW * srcW;
                    var x0 = Mathf.FloorToInt(fx);
                    var x1 = (x0 + 1) % srcW;
                    var tx = fx - x0;
                    x0 = ((x0 % srcW) + srcW) % srcW;
                    var c00 = src[y0 * srcW + x0];
                    var c10 = src[y0 * srcW + x1];
                    var c01 = src[y1 * srcW + x0];
                    var c11 = src[y1 * srcW + x1];
                    var c0 = Mathf.Lerp(c00, c10, tx);
                    var c1 = Mathf.Lerp(c01, c11, tx);
                    dst[y * dstW + x] = Mathf.Lerp(c0, c1, ty);
                }
            }
            return dst;
        }

        /// <summary>
        /// 帯域制限したノイズ層の総和。<paramref name="scales"/> はピクセル単位のシグマ、
        /// <paramref name="weights"/> は各帯域の振幅。
        /// </summary>
        public static float[] MultiScale(int width, int height, float[] scales, float[] weights, int seed)
        {
            var result = new float[width * height];
            for (var s = 0; s < scales.Length; s++)
            {
                var sigma = scales[s];
                float[] layer;
                if (sigma <= 4f)
                {
                    var noise = WhiteNoise(width, height, seed + s * 17);
                    layer = GaussianBlurWrap(noise, width, height, sigma);
                }
                else
                {
                    layer = LargeScaleNoise(width, height, sigma, seed + s * 17);
                }
                var w = weights[s];
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] += layer[i] * w;
                }
            }
            return result;
        }

        /// <summary>選択されたアスファルトの <see cref="NoiseStyle"/> プリセットでノイズを生成する。</summary>
        public static float[] StyleNoise(int width, int height, NoiseStyle style, float intensity, int seed)
        {
            var (scales, weights) = GetStyleProfile(style);
            for (var i = 0; i < weights.Length; i++)
            {
                weights[i] *= intensity;
            }
            return MultiScale(width, height, scales, weights, seed);
        }

        private static float[] BuildGaussianKernel(int radius, float sigma)
        {
            var len = radius * 2 + 1;
            var kernel = new float[len];
            var sum = 0f;
            var twoSigSq = 2f * sigma * sigma;
            for (var i = -radius; i <= radius; i++)
            {
                kernel[i + radius] = Mathf.Exp(-(i * i) / twoSigSq);
                sum += kernel[i + radius];
            }
            for (var i = 0; i < len; i++)
            {
                kernel[i] /= sum;
            }
            return kernel;
        }

        private static int WrapIndex(int index, int length)
        {
            // 負方向や length 超の大きなオフセットでも安全な剰余計算。
            return ((index % length) + length) % length;
        }

        private static (float[] scales, float[] weights) GetStyleProfile(NoiseStyle style)
        {
            switch (style)
            {
                case NoiseStyle.Smooth:
                    return (new[] { 1.5f, 4f, 12f }, new[] { 6f, 8f, 5f });
                case NoiseStyle.Aggregate:
                    // Standard に約 1.5 px の細かい帯域を 1 つ足した設定。骨材
                    // の粒感がやや強調される。
                    return (new[] { 0.6f, 1.5f, 3f, 10f, 40f }, new[] { 26f, 16f, 13f, 10f, 7f });
                case NoiseStyle.Coarse:
                    return (new[] { 0.8f, 3f, 12f, 50f }, new[] { 24f, 18f, 12f, 8f });
                case NoiseStyle.Worn:
                    return (new[] { 0.6f, 2f, 8f, 40f }, new[] { 28f, 18f, 14f, 12f });
                case NoiseStyle.Concrete:
                    return (new[] { 0.5f, 1.5f, 6f, 30f }, new[] { 14f, 10f, 6f, 4f });
                default:
                    return (new[] { 0.6f, 2.5f, 10f, 40f }, new[] { 22f, 14f, 10f, 7f });
            }
        }
    }
}
