using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>高さマップから法線マップピクセルを構築する。</summary>
    internal static class NormalBuilder
    {
        public static Color32[] Build(float[] heightMap, int W, int H)
        {
            var pixels = new Color32[W * H];
            // 高さマップは概ね [-30, 30] の範囲。法線の見た目が自然になるようチューニング済みの強度。
            const float strength = 4.5f / 255f;
            for (var y = 0; y < H; y++)
            {
                var yp = (y + 1) % H;
                var yn = (y - 1 + H) % H;
                for (var x = 0; x < W; x++)
                {
                    var xp = (x + 1) % W;
                    var xn = (x - 1 + W) % W;
                    var dx = (heightMap[y * W + xp] - heightMap[y * W + xn]) * 0.5f * strength;
                    var dy = (heightMap[yp * W + x] - heightMap[yn * W + x]) * 0.5f * strength;
                    const float nz = 1f;
                    var len = Mathf.Sqrt(dx * dx + dy * dy + nz * nz);
                    var nx = -dx / len;
                    var ny = -dy / len;
                    var nzn = nz / len;
                    var rByte = (byte)Mathf.Clamp((nx + 1f) * 0.5f * 255f, 0, 255);
                    var gByte = (byte)Mathf.Clamp((-ny + 1f) * 0.5f * 255f, 0, 255);
                    var bByte = (byte)Mathf.Clamp((nzn + 1f) * 0.5f * 255f, 0, 255);
                    pixels[y * W + x] = new Color32(rByte, gByte, bByte, 255);
                }
            }
            return pixels;
        }
    }
}
