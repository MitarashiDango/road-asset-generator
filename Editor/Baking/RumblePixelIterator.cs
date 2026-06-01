using UnityEngine;

namespace MitarashiDango.RoadAssetGenerator
{
    /// <summary>
    /// <see cref="RumblePixelIterator.ForEach"/> のコールバック。
    /// <paramref name="alpha"/> は 0..1 のフェザリング値で、帯の内側で 1 に近づき
    /// V 軸の縁付近で 0 に向かって減衰する。
    /// </summary>
    internal delegate void RumblePixelAction(int x, int y, int idx, float alpha);

    /// <summary><see cref="RumbleStripParams"/> に従って減速帯の有効ピクセルを走査するイテレーター。</summary>
    internal static class RumblePixelIterator
    {
        public static void ForEach(in RumbleStripParams p, int W, int H, RumblePixelAction action)
        {
            for (var y = 0; y < H; y++)
            {
                var t = (y - p.offsetPx) % p.periodPx;
                if (t < 0)
                {
                    t += p.periodPx;
                }
                if (t >= p.widthPx)
                {
                    continue;
                }
                var edgeT = Mathf.Min(t, p.widthPx - t);
                var alpha = Mathf.Clamp01(edgeT);
                for (var x = p.xStart; x < p.xEnd; x++)
                {
                    action(x, y, y * W + x, alpha);
                }
            }
        }
    }
}
