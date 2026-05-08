using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    internal static class PolygonMath
    {
        /// <summary>
        /// 全リングの Winding Number の合計を返す。
        /// CCW 外周は +1、CW 穴は -1 を寄与するため、
        /// 外周内部かつ穴の外部 → 合計 ≠ 0、穴の内部 → 合計 0。
        /// </summary>
        public static int WindingNumber(Vector2[][] rings, float px, float py)
        {
            var total = 0;
            for (var r = 0; r < rings.Length; r++)
            {
                total += WindingNumberSingleRing(rings[r], px, py);
            }
            return total;
        }

        /// <summary>
        /// 単一リングの Winding Number を計算する。
        /// 点が閉じたポリゴンの各辺に対してどちら側にあるかを累積する。
        /// </summary>
        public static int WindingNumberSingleRing(Vector2[] ring, float px, float py)
        {
            if (ring == null || ring.Length < 3)
            {
                return 0;
            }

            var wn = 0;
            var n = ring.Length;
            for (var i = 0; i < n; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % n];

                if (a.y <= py)
                {
                    if (b.y > py)
                    {
                        if (IsLeft(a, b, px, py) > 0f)
                        {
                            wn++;
                        }
                    }
                }
                else
                {
                    if (b.y <= py)
                    {
                        if (IsLeft(a, b, px, py) < 0f)
                        {
                            wn--;
                        }
                    }
                }
            }
            return wn;
        }

        /// <summary>
        /// 辺 (a→b) に対する点 (px, py) の左右判定。
        /// 正 = 左側、負 = 右側、0 = 辺上。
        /// </summary>
        private static float IsLeft(Vector2 a, Vector2 b, float px, float py)
        {
            return (b.x - a.x) * (py - a.y) - (px - a.x) * (b.y - a.y);
        }

        /// <summary>
        /// 符号付き面積を計算する。正 = CCW (反時計回り)、負 = CW (時計回り)。
        /// </summary>
        public static float SignedArea(Vector2[] ring)
        {
            if (ring == null || ring.Length < 3)
            {
                return 0f;
            }

            var area = 0f;
            var n = ring.Length;
            for (var i = 0; i < n; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % n];
                area += (a.x * b.y) - (b.x * a.y);
            }
            return area * 0.5f;
        }

        public static bool IsCCW(Vector2[] ring)
        {
            return SignedArea(ring) > 0f;
        }

        /// <summary>
        /// 全リングの AABB を計算する。リングが空の場合はゼロ範囲を返す。
        /// </summary>
        public static void ComputeAABB(Vector2[][] rings, out float minU, out float maxU, out float minV, out float maxV)
        {
            minU = float.MaxValue;
            maxU = float.MinValue;
            minV = float.MaxValue;
            maxV = float.MinValue;

            var any = false;
            for (var r = 0; r < rings.Length; r++)
            {
                var ring = rings[r];
                for (var i = 0; i < ring.Length; i++)
                {
                    var p = ring[i];
                    if (p.x < minU) minU = p.x;
                    if (p.x > maxU) maxU = p.x;
                    if (p.y < minV) minV = p.y;
                    if (p.y > maxV) maxV = p.y;
                    any = true;
                }
            }

            if (!any)
            {
                minU = maxU = minV = maxV = 0f;
            }
        }
    }
}
